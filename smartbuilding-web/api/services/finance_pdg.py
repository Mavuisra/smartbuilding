"""Dépenses en attente de validation PDG — ORM + magasin sync."""

from __future__ import annotations

from dataclasses import dataclass
from decimal import Decimal
from typing import Any
from uuid import UUID

from django.db.models import Sum
from django.utils import timezone

from api.models import FinancialTransaction, SyncedEntityStore
from api.module_data_utils import iso, money, pick_sync_value
from api.sync.utils import parse_bool, parse_decimal, pick

# Aligné sur FinanceLedgerService (desktop)
STATUS_PENDING_PDG = "En attente validation PDG"
STATUS_PAID = "Payé"
STATUS_REJECTED = "Rejeté"

_PENDING_STATUS_FRAGMENTS = (
    "en attente validation",
    "en attente",
    "attente",
    "pending",
    "à valider",
    "a valider",
)

_FINAL_STATUS_FRAGMENTS = (
    "payé",
    "paye",
    "approuvé",
    "approuve",
    "validé",
    "valide",
    "rejeté",
    "rejete",
    "refusé",
    "refuse",
)


@dataclass
class ExpenseRow:
    id: str
    transaction_date: Any
    reference: str
    category: str
    description: str
    recorded_by: str
    amount: Decimal
    status: str
    requires_pdg: bool
    source: str

    def to_validation_dict(self) -> dict[str, Any]:
        return {
            "id": self.id,
            "Date": iso(self.transaction_date),
            "Référence": self.reference or f"DEP-{self.id[:8].upper()}",
            "Catégorie": self.category or "—",
            "Description": self.description or "—",
            "Demandeur": self.recorded_by or "Comptable",
            "Source": self.source or "—",
            "Montant": money(self.amount),
            "Statut": self.status or STATUS_PENDING_PDG,
            "_actions": ["approve", "reject"],
            "_priority": "high" if self.requires_pdg else "normal",
        }

    def to_ledger_dict(self) -> dict[str, Any]:
        return {
            "Date": iso(self.transaction_date),
            "Type": "Dépense",
            "Catégorie": self.category or "—",
            "Description": self.description or "—",
            "Montant": money(self.amount),
            "Statut": self.status or "—",
            "Référence": self.reference or "—",
        }


def _normalize_status(value: Any) -> str:
    return str(value or "").strip()


def _is_expense_type(raw: Any) -> bool:
    if raw in (2, "2", FinancialTransaction.TxType.DEPENSE):
        return True
    if isinstance(raw, str) and "dep" in raw.lower():
        return True
    return False


def is_pending_expense(status: str, requires_pdg: bool = False) -> bool:
    """True si la dépense nécessite encore une décision PDG."""
    st = _normalize_status(status).lower()
    if not st and requires_pdg:
        return True
    if any(f in st for f in _FINAL_STATUS_FRAGMENTS):
        return False
    if requires_pdg:
        return True
    return any(f in st for f in _PENDING_STATUS_FRAGMENTS)


def _expense_from_orm(tx: FinancialTransaction) -> ExpenseRow:
    return ExpenseRow(
        id=str(tx.id),
        transaction_date=tx.transaction_date,
        reference=tx.reference or "",
        category=tx.category or "",
        description=tx.description or "",
        recorded_by=tx.recorded_by or "",
        amount=tx.amount,
        status=tx.status or "",
        requires_pdg=bool(tx.requires_pdg_approval),
        source="",
    )


def _expense_from_sync(payload: dict, entity_id: UUID) -> ExpenseRow | None:
    raw_type = pick(payload, "Type", "type")
    if raw_type is None or not _is_expense_type(raw_type):
        return None
    status = _normalize_status(pick(payload, "Status", "status") or "")
    requires = parse_bool(pick(payload, "RequiresPdgApproval", "requiresPdgApproval"), False)
    if not is_pending_expense(status, requires):
        return None
    amount = parse_decimal(pick_sync_value(payload, "Amount", "amount", default=0), 0)
    return ExpenseRow(
        id=str(entity_id),
        transaction_date=pick_sync_value(payload, "TransactionDate", "transactionDate"),
        reference=str(pick_sync_value(payload, "Reference", "reference", default="") or ""),
        category=str(pick_sync_value(payload, "Category", "category", default="") or ""),
        description=str(pick_sync_value(payload, "Description", "description", default="") or ""),
        recorded_by=str(pick_sync_value(payload, "RecordedBy", "recordedBy", default="") or ""),
        amount=amount,
        status=status or STATUS_PENDING_PDG,
        requires_pdg=requires,
        source=str(pick_sync_value(payload, "Source", "source", default="") or ""),
    )


def collect_pending_expenses(limit: int = 200) -> list[ExpenseRow]:
    """Dépenses non validées — ORM prioritaire, complété par le magasin sync."""
    seen: set[str] = set()
    rows: list[ExpenseRow] = []

    orm_qs = (
        FinancialTransaction.objects.filter(
            deleted_at__isnull=True,
            type=FinancialTransaction.TxType.DEPENSE,
        )
        .order_by("-transaction_date", "-updated_at")
    )
    for tx in orm_qs[: limit * 2]:
        if not is_pending_expense(tx.status or "", tx.requires_pdg_approval):
            continue
        rid = str(tx.id)
        if rid in seen:
            continue
        seen.add(rid)
        rows.append(_expense_from_orm(tx))
        if len(rows) >= limit:
            return rows

    for store in (
        SyncedEntityStore.objects.filter(
            entity_type="FinancialTransactions", deleted_at__isnull=True
        )
        .order_by("-updated_at")[: limit * 3]
    ):
        payload = store.json_data if isinstance(store.json_data, dict) else {}
        parsed = _expense_from_sync(payload, store.id)
        if parsed is None or parsed.id in seen:
            continue
        seen.add(parsed.id)
        rows.append(parsed)
        if len(rows) >= limit:
            break

    rows.sort(
        key=lambda r: (
            0 if r.requires_pdg else 1,
            str(r.transaction_date or ""),
        ),
        reverse=True,
    )
    return rows[:limit]


def pending_validation_summary(rows: list[ExpenseRow]) -> dict[str, Any]:
    total = sum((r.amount for r in rows), Decimal("0"))
    pdg_flagged = sum(1 for r in rows if r.requires_pdg)
    return {
        "count": len(rows),
        "totalAmount": float(total),
        "pdgRequiredCount": pdg_flagged,
        "totalAmountLabel": money(total),
    }


def apply_pdg_validation(
    expense_id: UUID,
    action: str,
    username: str,
) -> tuple[FinancialTransaction | None, str | None]:
    """
    Approuve ou rejette une dépense.
    Retourne (transaction, message_erreur).
    """
    try:
        tx = FinancialTransaction.objects.get(
            id=expense_id,
            deleted_at__isnull=True,
            type=FinancialTransaction.TxType.DEPENSE,
        )
    except FinancialTransaction.DoesNotExist:
        tx = None

    if tx is None:
        store = SyncedEntityStore.objects.filter(
            id=expense_id, entity_type="FinancialTransactions"
        ).first()
        if store is None:
            return None, "Dépense introuvable."
        from api.sync.materializers import materialize_finance
        from api.sync.utils import inject_entity_id

        payload = inject_entity_id(
            store.json_data if isinstance(store.json_data, dict) else {},
            store.id,
        )
        materialize_finance(payload)
        try:
            tx = FinancialTransaction.objects.get(
                id=expense_id,
                deleted_at__isnull=True,
                type=FinancialTransaction.TxType.DEPENSE,
            )
        except FinancialTransaction.DoesNotExist:
            return None, "Impossible de matérialiser la dépense."

    if not is_pending_expense(tx.status or "", tx.requires_pdg_approval):
        return None, "Cette dépense n'est plus en attente de validation."

    now = timezone.now()
    if action == "approve":
        tx.status = STATUS_PAID
        tx.requires_pdg_approval = False
        tx.approved_at = now
        tx.approved_by = username or "PDG"
    elif action == "reject":
        tx.status = STATUS_REJECTED
        tx.requires_pdg_approval = False
        tx.approved_at = now
        tx.approved_by = username or "PDG"
    else:
        return None, "Action invalide."

    tx.updated_at = now
    tx.is_synced = False
    tx.save(
        update_fields=[
            "status",
            "requires_pdg_approval",
            "approved_at",
            "approved_by",
            "updated_at",
            "is_synced",
        ]
    )
    _mirror_validation_to_sync_store(tx)
    return tx, None


def _mirror_validation_to_sync_store(tx: FinancialTransaction) -> None:
    try:
        store = SyncedEntityStore.objects.get(
            id=tx.id, entity_type="FinancialTransactions"
        )
    except SyncedEntityStore.DoesNotExist:
        return
    data = dict(store.json_data) if isinstance(store.json_data, dict) else {}
    data["Status"] = tx.status
    data["RequiresPdgApproval"] = tx.requires_pdg_approval
    data["ApprovedAt"] = tx.approved_at.isoformat() if tx.approved_at else None
    data["ApprovedBy"] = tx.approved_by or ""
    data["UpdatedAt"] = tx.updated_at.isoformat()
    store.json_data = data
    store.updated_at = tx.updated_at
    store.save(update_fields=["json_data", "updated_at"])


def ledger_income_expense_totals() -> tuple[Decimal, Decimal]:
    from api.services.sync_metrics import filter_to_synced

    income = (
        filter_to_synced(
            FinancialTransaction.objects.filter(
                deleted_at__isnull=True,
                type=FinancialTransaction.TxType.RECETTE,
            ),
            "FinancialTransactions",
        ).aggregate(t=Sum("amount"))["t"]
        or Decimal("0")
    )
    expenses = (
        filter_to_synced(
            FinancialTransaction.objects.filter(
                deleted_at__isnull=True,
                type=FinancialTransaction.TxType.DEPENSE,
            ),
            "FinancialTransactions",
        ).aggregate(t=Sum("amount"))["t"]
        or Decimal("0")
    )
    return income, expenses
