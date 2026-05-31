"""Journal financier — déduplication des écritures synchronisées en double."""

from __future__ import annotations

from datetime import date, datetime
from decimal import Decimal
from typing import Any

from django.db.models import QuerySet
from django.utils import timezone

from api.models import FinancialTransaction, SyncedEntityStore
from api.module_data_utils import pick_sync_value


def _tx_date_key(value) -> str:
    if value is None:
        return ""
    if isinstance(value, datetime):
        return value.date().isoformat()
    if isinstance(value, date):
        return value.isoformat()
    text = str(value)
    return text[:10] if len(text) >= 10 else text


def financial_dedupe_key(
    *,
    reference: str = "",
    description: str = "",
    amount: Any = 0,
    transaction_date: Any = None,
    tx_type: Any = 1,
    related_entity_id: str | None = None,
) -> tuple:
    """Clé métier stable : une ligne par paiement loyer / écriture unique."""
    ref = (reference or "").strip()
    desc = (description or "").strip().lower()
    amt = str(Decimal(str(amount or 0)).quantize(Decimal("0.01")))
    if related_entity_id:
        return ("rel", str(related_entity_id).lower(), amt, int(tx_type) if tx_type else 1)
    return (
        "row",
        ref.lower(),
        desc,
        amt,
        _tx_date_key(transaction_date),
        int(tx_type) if tx_type not in (None, "") else 1,
    )


def dedupe_financial_transactions(
    transactions: list[FinancialTransaction],
) -> list[FinancialTransaction]:
    """Garde l'écriture la plus récente par clé métier."""
    best: dict[tuple, FinancialTransaction] = {}
    for tx in sorted(
        transactions,
        key=lambda t: (t.updated_at or t.transaction_date, t.transaction_date),
    ):
        key = financial_dedupe_key(
            reference=tx.reference or "",
            description=tx.description or "",
            amount=tx.amount,
            transaction_date=tx.transaction_date,
            tx_type=tx.type,
        )
        best[key] = tx
    return sorted(
        best.values(),
        key=lambda t: t.transaction_date or timezone.now(),
        reverse=True,
    )


def queryset_to_deduped_list(qs: QuerySet) -> list[FinancialTransaction]:
    return dedupe_financial_transactions(list(qs[:500]))


def dedupe_sync_financial_rows(
    mapper,
    limit: int = 300,
) -> list[dict[str, Any]]:
    """Déduplique le magasin sync avant affichage (plusieurs UUID, même écriture)."""
    best: dict[tuple, tuple[Any, dict]] = {}
    stores = (
        SyncedEntityStore.objects.filter(
            entity_type="FinancialTransactions", deleted_at__isnull=True
        )
        .order_by("-updated_at")[: limit * 3]
    )
    for store in stores:
        payload = store.json_data if isinstance(store.json_data, dict) else {}
        mapped = mapper(payload)
        if not mapped:
            continue
        rel = pick_sync_value(payload, "RelatedEntityId", "relatedEntityId")
        key = financial_dedupe_key(
            reference=pick_sync_value(payload, "Reference", "reference", default=""),
            description=pick_sync_value(payload, "Description", "description", default=""),
            amount=pick_sync_value(payload, "Amount", "amount", default=0),
            transaction_date=pick_sync_value(
                payload, "TransactionDate", "transactionDate"
            ),
            tx_type=pick_sync_value(payload, "Type", "type", default=1),
            related_entity_id=str(rel) if rel else None,
        )
        prev = best.get(key)
        if prev is None or store.updated_at >= prev[0]:
            best[key] = (store.updated_at, mapped)
    rows = [pair[1] for pair in best.values()]
    rows.sort(key=lambda r: str(r.get("Date") or ""), reverse=True)
    return rows[:limit]
