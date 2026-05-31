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


def rent_from_orm(year: int, month: int, *, synced_only: bool = False) -> tuple[Decimal, Decimal, int, Decimal]:
    base = RentPayment.objects.filter(deleted_at__isnull=True)
    qs = filter_to_synced(base, "RentPayments") if synced_only else base
    month_rents = list(qs.filter(year=year, month=month))
    if not month_rents:
        return Decimal("0"), Decimal("0"), 0, Decimal("0")
    collected = sum((r.amount_paid for r in month_rents), Decimal("0"))
    planned = sum((r.amount_due for r in month_rents), Decimal("0"))
    late_count = sum(1 for r in month_rents if r.is_late or r.amount_paid < r.amount_due)
    late_amount = sum(
        (r.amount_due - r.amount_paid for r in month_rents if r.amount_paid < r.amount_due),
        Decimal("0"),
    )
    return collected, planned, late_count, late_amount


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
    base = RentPayment.objects.filter(deleted_at__isnull=True)
    qs = filter_to_synced(base, "RentPayments") if synced_only else base
    chart = []
    for month_start in month_starts:
        y, m = month_start.year, month_start.month
        val = qs.filter(year=y, month=m).aggregate(t=Sum("amount_paid"))["t"] or 0
        chart.append(
            {
                "label": month_start.strftime("%b %Y"),
                "value": float(val),
            }
        )
    return chart


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
    for item in diag.get("mismatches") or []:
        et = item.get("entityType")
        if et in DASHBOARD_ENTITY_TYPES:
            rebuilt += rematerialize_entity_type(et)
    return rebuilt
