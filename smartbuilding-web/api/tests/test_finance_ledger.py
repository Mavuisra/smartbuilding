"""Tests déduplication journal financier."""

import uuid
from decimal import Decimal

from django.test import TestCase
from django.utils import timezone

from api.models import FinancialTransaction, SyncedEntityStore
from api.services.finance_ledger import (
    dedupe_financial_transactions,
    dedupe_sync_financial_rows,
)


class FinanceLedgerDedupeTests(TestCase):
    def test_dedupe_orm_keeps_one_per_description(self):
        desc = "Loyer 06/2026 — CC — XXX"
        for _ in range(5):
            FinancialTransaction.objects.create(
                type=FinancialTransaction.TxType.RECETTE,
                category="Loyers",
                description=desc,
                amount=Decimal("10000"),
                reference="REV-202605-0002",
                transaction_date=timezone.now(),
            )
        txs = list(FinancialTransaction.objects.all())
        self.assertEqual(len(dedupe_financial_transactions(txs)), 1)

    def test_dedupe_sync_store_by_business_key(self):
        desc = "Loyer 05/2026 — CC — XXX"
        for _ in range(4):
            SyncedEntityStore.objects.create(
                id=uuid.uuid4(),
                entity_type="FinancialTransactions",
                json_data={
                    "Description": desc,
                    "Amount": 10000,
                    "Type": 1,
                    "Reference": "REV-202605-0002",
                    "TransactionDate": "2026-05-30T00:00:00Z",
                },
                updated_at=timezone.now(),
            )

        rows = dedupe_sync_financial_rows(
            lambda d: {"Description": d.get("Description"), "Date": d.get("TransactionDate")},
            limit=50,
        )
        self.assertEqual(len(rows), 1)
