"""Agrégation métriques alignée Desktop (SQLite) ↔ magasin sync ↔ ORM."""

from __future__ import annotations

from calendar import monthrange
from datetime import date
from decimal import Decimal

from django.db.models import QuerySet, Sum
from django.utils import timezone

from api.models import (
    FinancialTransaction,
    LeaseContract,
    Premise,
    RentPayment,
    SyncedEntityStore,
    Tenant,
)
from api.module_data_utils import pick_sync_value
from api.services.finance_ledger import dedupe_financial_transactions, financial_dedupe_key


def sync_store_count(entity_type: str) -> int:
    return SyncedEntityStore.objects.filter(
        entity_type=entity_type, deleted_at__isnull=True
    ).count()


def has_sync_store_data() -> bool:
    return SyncedEntityStore.objects.filter(deleted_at__isnull=True).exists()


def synced_id_set(entity_type: str) -> set | None:
    """IDs connus du desktop pour ce type ; None si le magasin sync est vide pour ce type."""
    ids = SyncedEntityStore.objects.filter(
        entity_type=entity_type, deleted_at__isnull=True
    ).values_list("id", flat=True)
    id_list = list(ids)
    return set(id_list) if id_list else None


def filter_to_synced(qs: QuerySet, entity_type: str) -> QuerySet:
    """Limite l'ORM aux enregistrements poussés par le Desktop (exclut le seed démo)."""
    ids = synced_id_set(entity_type)
    if ids is not None:
        return qs.filter(id__in=ids)
    return qs


def orm_without_sync_filter(qs: QuerySet) -> QuerySet:
    """ORM complet (modules liste) quand le magasin sync est vide pour ce type."""
    return qs


def prefer_sync_store(entity_type: str, orm_count: int) -> bool:
    """Utilise le JSON sync si le magasin est plus complet que l'ORM filtré."""
    store_count = sync_store_count(entity_type)
    if store_count == 0:
        return False
    if orm_count == 0:
        return True
    return store_count > orm_count


def calendar_month_starts(today: date, months: int = 6) -> list[date]:
    """Six derniers mois calendaires (aligné DashboardService desktop)."""
    first = today.replace(day=1)
    out: list[date] = []
    y, m = first.year, first.month
    for _ in range(months):
        out.append(date(y, m, 1))
        m -= 1
        if m < 1:
            m = 12
            y -= 1
    out.reverse()
    return out


def _is_active_lease_status(status) -> bool:
    if status is None:
        return False
    if isinstance(status, int):
        return status in (1, 2)
    text = str(status).lower()
    return "actif" in text or "active" in text


def _dedupe_rent_payments_orm(payments: list[RentPayment]) -> list[RentPayment]:
    """Une ligne par contrat et période (évite 14× le même loyer)."""
    best: dict[tuple, RentPayment] = {}
    for payment in sorted(payments, key=lambda p: (p.updated_at, p.amount_paid)):
        key = (
            payment.year,
            payment.month,
            str(payment.lease_contract_id or payment.id),
        )
        prev = best.get(key)
        if prev is None or payment.amount_paid >= prev.amount_paid:
            best[key] = payment
    return list(best.values())


def _sum_rent_payments(payments: list[RentPayment]) -> tuple[Decimal, Decimal, int, Decimal]:
    if not payments:
        return Decimal("0"), Decimal("0"), 0, Decimal("0")
    collected = sum((p.amount_paid for p in payments), Decimal("0"))
    planned = sum((p.amount_due for p in payments), Decimal("0"))
    late_count = sum(1 for p in payments if p.is_late or p.amount_paid < p.amount_due)
    late_amount = sum(
        (p.amount_due - p.amount_paid for p in payments if p.amount_paid < p.amount_due),
        Decimal("0"),
    )
    return collected, planned, late_count, late_amount


def rent_from_sync_store(year: int, month: int) -> tuple[Decimal, Decimal, int, Decimal]:
    """Paiements loyer dédupliqués depuis le magasin sync."""
    best: dict[tuple, dict] = {}
    for row in SyncedEntityStore.objects.filter(
        entity_type="RentPayments", deleted_at__isnull=True
    ).iterator():
        payload = row.json_data if isinstance(row.json_data, dict) else {}
        y = int(pick_sync_value(payload, "Year", "year", default=0) or 0)
        m = int(pick_sync_value(payload, "Month", "month", default=0) or 0)
        if y != year or m != month:
            continue
        lease = str(
            pick_sync_value(payload, "LeaseContractId", "leaseContractId") or row.id
        )
        key = (y, m, lease)
        paid = Decimal(str(pick_sync_value(payload, "AmountPaid", "amountPaid", default=0) or 0))
        prev = best.get(key)
        if prev is None or paid >= prev["paid"]:
            best[key] = {
                "due": Decimal(
                    str(pick_sync_value(payload, "AmountDue", "amountDue", default=0) or 0)
                ),
                "paid": paid,
                "late": pick_sync_value(payload, "IsLate", "isLate", default=False)
                in (True, "true", "True", 1, "1"),
            }
    if not best:
        return Decimal("0"), Decimal("0"), 0, Decimal("0")
    collected = sum(v["paid"] for v in best.values())
    planned = sum(v["due"] for v in best.values())
    late_count = sum(
        1 for v in best.values() if v["late"] or v["paid"] < v["due"]
    )
    late_amount = sum(
        max(v["due"] - v["paid"], Decimal("0"))
        for v in best.values()
        if v["paid"] < v["due"]
    )
    return collected, planned, late_count, late_amount


def rent_from_ledger_deduped(
    year: int, month: int, *, orm_only: bool = False
) -> tuple[Decimal, Decimal, int, Decimal]:
    """Recettes « Loyers » dédupliquées (secours si RentPayments absent)."""
    from api.sync.utils import normalize_sync_datetime

    best: dict[tuple, Decimal] = {}

    def consider(
        *,
        description: str,
        amount: Any,
        tx_date,
        reference: str = "",
        related_entity_id: str | None = None,
    ) -> None:
        dt = normalize_sync_datetime(tx_date)
        if not dt or dt.year != year or dt.month != month:
            return
        amt = Decimal(str(amount or 0))
        if amt <= 0:
            return
        key = financial_dedupe_key(
            reference=reference,
            description=description,
            amount=amt,
            transaction_date=dt,
            tx_type=1,
            related_entity_id=related_entity_id,
        )
        best[key] = amt

    if not orm_only:
        for row in SyncedEntityStore.objects.filter(
            entity_type="FinancialTransactions", deleted_at__isnull=True
        ).iterator():
            payload = row.json_data if isinstance(row.json_data, dict) else {}
            raw_type = pick_sync_value(payload, "Type", "type", default=1)
            is_income = raw_type in (1, "1", "Recette") or (
                isinstance(raw_type, str) and "rec" in str(raw_type).lower()
            )
            if not is_income:
                continue
            cat = str(pick_sync_value(payload, "Category", "category", default="")).lower()
            desc = str(pick_sync_value(payload, "Description", "description", default=""))
            if "loyer" not in cat and "loyer" not in desc.lower() and "rent" not in cat:
                continue
            rel = pick_sync_value(payload, "RelatedEntityId", "relatedEntityId")
            consider(
                description=desc or cat,
                amount=pick_sync_value(payload, "Amount", "amount", default=0),
                tx_date=pick_sync_value(payload, "TransactionDate", "transactionDate"),
                reference=str(pick_sync_value(payload, "Reference", "reference", default="")),
                related_entity_id=str(rel) if rel else None,
            )

    qs = FinancialTransaction.objects.filter(
        deleted_at__isnull=True,
        type=FinancialTransaction.TxType.RECETTE,
    )
    ids = synced_id_set("FinancialTransactions")
    if ids is not None:
        qs = qs.filter(id__in=ids)
    for tx in dedupe_financial_transactions(list(qs)):
        cat = (tx.category or "").lower()
        desc = (tx.description or "").lower()
        if "loyer" not in cat and "loyer" not in desc and "rent" not in cat:
            continue
        consider(
            description=tx.description or tx.category,
            amount=tx.amount,
            tx_date=tx.transaction_date,
            reference=tx.reference or "",
        )

    if not best:
        return Decimal("0"), Decimal("0"), 0, Decimal("0")
    collected = sum(best.values(), Decimal("0"))
    return collected, collected, 0, Decimal("0")


def rent_all_time_collected() -> Decimal:
    """Somme de tous les loyers encaissés (toutes périodes)."""
    base = RentPayment.objects.filter(deleted_at__isnull=True)
    ids = synced_id_set("RentPayments")
    orm_list = list(base.filter(id__in=ids)) if ids is not None else list(base)
    if not orm_list and ids is not None:
        orm_list = list(base)
    if orm_list:
        return _sum_rent_payments(_dedupe_rent_payments_orm(orm_list))[0]

    if sync_store_count("RentPayments") > 0:
        best: dict[tuple, Decimal] = {}
        for row in SyncedEntityStore.objects.filter(
            entity_type="RentPayments", deleted_at__isnull=True
        ).iterator():
            payload = row.json_data if isinstance(row.json_data, dict) else {}
            y = int(pick_sync_value(payload, "Year", "year", default=0) or 0)
            m = int(pick_sync_value(payload, "Month", "month", default=0) or 0)
            lease = str(
                pick_sync_value(payload, "LeaseContractId", "leaseContractId") or row.id
            )
            key = (y, m, lease)
            paid = Decimal(
                str(pick_sync_value(payload, "AmountPaid", "amountPaid", default=0) or 0)
            )
            prev = best.get(key)
            if prev is None or paid >= prev:
                best[key] = paid
        if best:
            return sum(best.values(), Decimal("0"))

    # Secours : recettes loyer du journal (toutes dates)
    from api.sync.utils import normalize_sync_datetime

    ledger_best: dict[tuple, Decimal] = {}
    for row in SyncedEntityStore.objects.filter(
        entity_type="FinancialTransactions", deleted_at__isnull=True
    ).iterator():
        payload = row.json_data if isinstance(row.json_data, dict) else {}
        raw_type = pick_sync_value(payload, "Type", "type", default=1)
        is_receipt = raw_type in (1, "1", "Recette") or (
            isinstance(raw_type, str) and "rec" in str(raw_type).lower()
        )
        if not is_receipt:
            continue
        cat = str(pick_sync_value(payload, "Category", "category", default="") or "")
        if "loyer" not in cat.lower() and "rent" not in cat.lower():
            continue
        dt = normalize_sync_datetime(
            pick_sync_value(payload, "TransactionDate", "transactionDate")
        )
        amount = Decimal(str(pick_sync_value(payload, "Amount", "amount", default=0) or 0))
        ref = str(pick_sync_value(payload, "Reference", "reference", default="") or "")
        key = (ref, dt.date() if dt else None, amount)
        ledger_best[key] = amount
    return sum(ledger_best.values(), Decimal("0"))


def rent_month_totals(year: int, month: int) -> tuple[Decimal, Decimal, int, Decimal]:
    """
    Loyers du mois : RentPayments (priorité Desktop), puis journal Loyers dédupliqué.
    """
    base = RentPayment.objects.filter(
        deleted_at__isnull=True, year=year, month=month
    )
    ids = synced_id_set("RentPayments")
    orm_list = list(base.filter(id__in=ids)) if ids is not None else list(base)
    if not orm_list and ids is not None:
        orm_list = list(base)
    if orm_list:
        totals = _sum_rent_payments(_dedupe_rent_payments_orm(orm_list))
        if totals[0] > 0 or totals[1] > 0:
            return totals

    if sync_store_count("RentPayments") > 0:
        totals = rent_from_sync_store(year, month)
        if totals[0] > 0 or totals[1] > 0:
            return totals

    return rent_from_ledger_deduped(year, month)


def rent_from_orm(year: int, month: int, *, synced_only: bool = False) -> tuple[Decimal, Decimal, int, Decimal]:
    return rent_month_totals(year, month)


def _expenses_totals(*, month_start: date | None = None) -> Decimal:
    """Dépenses dédupliquées (mois ou cumul total)."""
    base = FinancialTransaction.objects.filter(
        deleted_at__isnull=True,
        type=FinancialTransaction.TxType.DEPENSE,
    ).exclude(status="En attente validation PDG")
    if month_start is not None:
        base = base.filter(transaction_date__date__gte=month_start)

    ids = synced_id_set("FinancialTransactions")
    qs = base.filter(id__in=ids) if ids is not None else base
    deduped = dedupe_financial_transactions(list(qs))
    if not deduped and ids is not None:
        deduped = dedupe_financial_transactions(list(base))
    if deduped:
        return sum((t.amount for t in deduped), Decimal("0"))

    seen: dict[tuple, Decimal] = {}
    for row in SyncedEntityStore.objects.filter(
        entity_type="FinancialTransactions", deleted_at__isnull=True
    ).iterator():
        payload = row.json_data if isinstance(row.json_data, dict) else {}
        raw_type = pick_sync_value(payload, "Type", "type", default=1)
        is_expense = raw_type in (2, "2", "Depense", "Dépense") or (
            isinstance(raw_type, str) and "dep" in str(raw_type).lower()
        )
        if not is_expense:
            continue
        status = str(pick_sync_value(payload, "Status", "status", default="") or "")
        if status == "En attente validation PDG":
            continue
        from api.sync.utils import normalize_sync_datetime

        dt = normalize_sync_datetime(
            pick_sync_value(payload, "TransactionDate", "transactionDate")
        )
        if month_start is not None and dt and dt.date() < month_start:
            continue
        rel = pick_sync_value(payload, "RelatedEntityId", "relatedEntityId")
        key = financial_dedupe_key(
            reference=pick_sync_value(payload, "Reference", "reference", default=""),
            description=pick_sync_value(payload, "Description", "description", default=""),
            amount=pick_sync_value(payload, "Amount", "amount", default=0),
            transaction_date=dt,
            tx_type=2,
            related_entity_id=str(rel) if rel else None,
        )
        seen[key] = Decimal(str(pick_sync_value(payload, "Amount", "amount", default=0) or 0))
    return sum(seen.values(), Decimal("0"))


def expenses_month_totals(month_start: date) -> Decimal:
    """Dépenses du mois, écritures dédupliquées."""
    return _expenses_totals(month_start=month_start)


def expenses_all_time_totals() -> Decimal:
    """Dépenses engagées cumulées (toutes périodes)."""
    return _expenses_totals(month_start=None)


def occupancy_totals() -> tuple[int, int]:
    """Locaux total / occupés — ORM puis magasin sync."""
    total, occupied = occupancy_from_orm(synced_only=False)
    if total > 0:
        return total, occupied

    total = occupied = 0
    for row in SyncedEntityStore.objects.filter(
        entity_type="Premises", deleted_at__isnull=True
    ).iterator():
        payload = row.json_data if isinstance(row.json_data, dict) else {}
        total += 1
        if pick_sync_value(payload, "IsOccupied", "isOccupied", default=False) in (
            True,
            "true",
            "True",
            1,
            "1",
        ):
            occupied += 1
    if total > 0:
        return total, occupied

    active = count_active_leases()
    if active > 0:
        return active, active
    return 0, 0


def count_active_leases() -> int:
    active = sum(
        1
        for c in LeaseContract.objects.filter(deleted_at__isnull=True).iterator()
        if _is_active_lease_status(c.status)
    )
    if active > 0:
        return active

    count = 0
    for row in SyncedEntityStore.objects.filter(
        entity_type="LeaseContracts", deleted_at__isnull=True
    ).iterator():
        payload = row.json_data if isinstance(row.json_data, dict) else {}
        if _is_active_lease_status(pick_sync_value(payload, "Status", "status")):
            count += 1
    return count


def count_tenants_total() -> int:
    n = Tenant.objects.filter(deleted_at__isnull=True).count()
    if n > 0:
        return n
    return SyncedEntityStore.objects.filter(
        entity_type="Tenants", deleted_at__isnull=True
    ).count()


def revenue_chart_totals(month_starts: list[date]) -> list[dict]:
    return [
        {
            "label": ms.strftime("%b %Y"),
            "value": float(rent_month_totals(ms.year, ms.month)[0]),
        }
        for ms in month_starts
    ]


def expenses_from_orm(month_start: date, *, synced_only: bool = False) -> Decimal:
    base = FinancialTransaction.objects.filter(
        deleted_at__isnull=True,
        type=FinancialTransaction.TxType.DEPENSE,
        transaction_date__date__gte=month_start,
    )
    qs = filter_to_synced(base, "FinancialTransactions") if synced_only else base
    return qs.aggregate(t=Sum("amount"))["t"] or Decimal("0")


def occupancy_from_orm(*, synced_only: bool = False) -> tuple[int, int]:
    base = Premise.objects.filter(deleted_at__isnull=True)
    qs = filter_to_synced(base, "Premises") if synced_only else base
    total = qs.count()
    occupied = qs.filter(is_occupied=True).count()
    return total, occupied


def revenue_chart_from_orm(month_starts: list[date], *, synced_only: bool = False) -> list[dict]:
    return revenue_chart_totals(month_starts)


def recent_movements_from_orm(limit: int, *, synced_only: bool = False) -> list[dict]:
    base = FinancialTransaction.objects.filter(deleted_at__isnull=True)
    qs = filter_to_synced(base, "FinancialTransactions") if synced_only else base
    return list(
        qs.order_by("-transaction_date")[:limit].values(
            "transaction_date", "type", "category", "description", "amount", "reference"
        )
    )


def count_leases_orm(*, synced_only: bool = False) -> int:
    base = LeaseContract.objects.filter(deleted_at__isnull=True, status__icontains="Actif")
    qs = filter_to_synced(base, "LeaseContracts") if synced_only else base
    return qs.count()


def count_tenants_orm(*, synced_only: bool = False) -> int:
    base = Tenant.objects.filter(deleted_at__isnull=True)
    qs = filter_to_synced(base, "Tenants") if synced_only else base
    return qs.count()


DASHBOARD_ENTITY_TYPES = (
    "RentPayments",
    "Premises",
    "LeaseContracts",
    "Tenants",
    "FinancialTransactions",
)


def ensure_dashboard_orm_materialized() -> int:
    """Rejoue les matérialiseurs si le magasin sync est en avance sur l'ORM."""
    from api.services.diagnostics import get_data_pipeline_diagnostics
    from api.sync.registry import rematerialize_entity_type

    diag = get_data_pipeline_diagnostics()
    rebuilt = 0
    targets = list(DASHBOARD_ENTITY_TYPES)
    if diag.get("syncStoreTotal", 0) > 0:
        for et in targets:
            if sync_store_count(et) > 0:
                rebuilt += rematerialize_entity_type(et)
    for item in diag.get("mismatches") or []:
        et = item.get("entityType")
        if et in targets:
            rebuilt += rematerialize_entity_type(et)
    return rebuilt
