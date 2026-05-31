"""Tests maintenance sync post-push."""

import uuid
from decimal import Decimal

from django.test import TestCase
from django.utils import timezone

from api.models import FinancialTransaction, SyncedEntityStore
from api.sync.maintenance import dedupe_financial_sync_and_orm
from api.sync.registry import apply_push


class SyncMaintenanceTests(TestCase):
    def test_apply_push_skips_applied_when_materializer_would_fail(self):
        """JSON invalide pour un champ requis → entité non comptée comme appliquée."""
        pay_id = uuid.uuid4()
        applied = apply_push(
            "RentPayments",
            [
                {
                    "id": str(pay_id),
                    "updatedAt": timezone.now().isoformat(),
                    "jsonData": "not-json",
                }
            ],
        )
        self.assertEqual(applied, 0)
        self.assertFalse(SyncedEntityStore.objects.filter(id=pay_id).exists())

    def test_dedupe_on_financial_push(self):
        rel_id = str(uuid.uuid4())
        for _ in range(3):
            apply_push(
                "FinancialTransactions",
                [
                    {
                        "id": str(uuid.uuid4()),
                        "updatedAt": timezone.now().isoformat(),
                        "jsonData": (
                            f'{{"Type": 1, "Category": "Loyers", '
                            f'"Description": "Loyer test", "Amount": 1000, '
                            f'"RelatedEntityId": "{rel_id}", '
                            f'"Reference": "REV-202605-0001", '
                            f'"TransactionDate": "2026-05-30T00:00:00Z"}}'
                        ),
                    }
                ],
            )
        store_count = SyncedEntityStore.objects.filter(
            entity_type="FinancialTransactions", deleted_at__isnull=True
        ).count()
        self.assertEqual(store_count, 1)

    def test_dedupe_financial_sync_and_orm(self):
        desc = "Loyer 06/2026 — dup"
        for _ in range(4):
            FinancialTransaction.objects.create(
                type=FinancialTransaction.TxType.RECETTE,
                category="Loyers",
                description=desc,
                amount=Decimal("5000"),
                reference="REV-202606-0001",
                transaction_date=timezone.now(),
            )
        _, orm_deleted = dedupe_financial_sync_and_orm()
        self.assertEqual(orm_deleted, 3)
        self.assertEqual(
            FinancialTransaction.objects.filter(deleted_at__isnull=True).count(),
            1,
        )
