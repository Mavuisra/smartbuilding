"""Maintenance post-push : déduplication et alignement ORM ↔ magasin sync."""

from __future__ import annotations

import logging

from django.utils import timezone

from api.models import FinancialTransaction, SyncedEntityStore
from api.module_data_utils import pick_sync_value
from api.services.finance_ledger import financial_dedupe_key

logger = logging.getLogger(__name__)


def dedupe_financial_sync_and_orm() -> tuple[int, int]:
    """
    Conserve une seule FinancialTransaction par clé métier (RelatedEntityId ou référence).
    Retourne (supprimés magasin sync, supprimés ORM).
    """
    store_rows = list(
        SyncedEntityStore.objects.filter(
            entity_type="FinancialTransactions", deleted_at__isnull=True
        )
    )
    store_best: dict[tuple, SyncedEntityStore] = {}
    for row in store_rows:
        data = row.json_data if isinstance(row.json_data, dict) else {}
        key = financial_dedupe_key(
            reference=str(
                pick_sync_value(data, "Reference", "reference", default="") or ""
            ),
            description=str(
                pick_sync_value(data, "Description", "description", default="") or ""
            ),
            amount=pick_sync_value(data, "Amount", "amount", default=0),
            transaction_date=pick_sync_value(
                data, "TransactionDate", "transactionDate"
            ),
            tx_type=pick_sync_value(data, "Type", "type", default=1),
            related_entity_id=str(
                pick_sync_value(data, "RelatedEntityId", "relatedEntityId") or ""
            )
            or None,
        )
        prev = store_best.get(key)
        if prev is None or row.updated_at >= prev.updated_at:
            store_best[key] = row

    store_deleted = 0
    keep_ids = {row.id for row in store_best.values()}
    for row in store_rows:
        if row.id not in keep_ids:
            row.delete()
            store_deleted += 1

    orm_rows = list(
        FinancialTransaction.objects.filter(deleted_at__isnull=True)
    )
    orm_best: dict[tuple, FinancialTransaction] = {}
    for tx in orm_rows:
        key = financial_dedupe_key(
            reference=tx.reference or "",
            description=tx.description or "",
            amount=tx.amount,
            transaction_date=tx.transaction_date,
            tx_type=tx.type,
        )
        prev = orm_best.get(key)
        if prev is None or (tx.updated_at or tx.transaction_date) >= (
            prev.updated_at or prev.transaction_date
        ):
            orm_best[key] = tx

    orm_deleted = 0
    orm_keep = {tx.id for tx in orm_best.values()}
    for tx in orm_rows:
        if tx.id not in orm_keep:
            tx.deleted_at = timezone.now()
            tx.is_synced = True
            tx.save(update_fields=["deleted_at", "is_synced", "updated_at"])
            orm_deleted += 1

    if store_deleted or orm_deleted:
        logger.info(
            "Dédup FinancialTransactions : %s magasin, %s ORM",
            store_deleted,
            orm_deleted,
        )
    return store_deleted, orm_deleted
