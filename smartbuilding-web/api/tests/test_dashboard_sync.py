"""Tests cohérence dashboard web ↔ magasin sync (données Desktop)."""

import uuid
from datetime import date
from decimal import Decimal

from django.test import TestCase
from django.utils import timezone

from api.models import FinancialTransaction, Premise, RentPayment, SyncedEntityStore
from api.services.dashboard import _resolve_month_rent, get_executive_summary
from api.services.sync_metrics import (
    calendar_month_starts,
    rent_from_orm,
    revenue_chart_from_orm,
    synced_id_set,
)
from api.sync.registry import apply_push, rematerialize_entity_type


class DashboardSyncAlignmentTests(TestCase):
    def test_filter_to_synced_excludes_seed_not_in_store(self):
        seed_id = uuid.uuid4()
        synced_id = uuid.uuid4()
        RentPayment.objects.create(
            id=seed_id,
            year=2026,
            month=5,
            amount_due=Decimal("100"),
            amount_paid=Decimal("0"),
        )
        SyncedEntityStore.objects.create(
            id=synced_id,
            entity_type="RentPayments",
            json_data={
                "Year": 2026,
                "Month": 5,
                "AmountDue": 500,
                "AmountPaid": 400,
            },
            updated_at=timezone.now(),
        )
        RentPayment.objects.create(
            id=synced_id,
            year=2026,
            month=5,
            amount_due=Decimal("500"),
            amount_paid=Decimal("400"),
        )
        collected, planned, _, _ = rent_from_orm(2026, 5, synced_only=True)
        self.assertEqual(collected, Decimal("400"))
        self.assertEqual(planned, Decimal("500"))
        self.assertIsNotNone(synced_id_set("RentPayments"))

    def test_executive_summary_uses_sync_store_when_orm_empty(self):
        pay_id = uuid.uuid4()
        SyncedEntityStore.objects.create(
            id=pay_id,
            entity_type="RentPayments",
            json_data={
                "Year": timezone.localdate().year,
                "Month": timezone.localdate().month,
                "AmountDue": 1000,
                "AmountPaid": 750,
                "IsLate": False,
            },
            updated_at=timezone.now(),
        )
        summary = get_executive_summary()
        self.assertEqual(summary["rentCollected"], 750.0)
        self.assertEqual(summary["rentPlanned"], 1000.0)

    def test_resolve_month_rent_prefers_sync_store(self):
        today = timezone.localdate()
        SyncedEntityStore.objects.create(
            id=uuid.uuid4(),
            entity_type="RentPayments",
            json_data={
                "Year": today.year,
                "Month": today.month,
                "AmountDue": 2000,
                "AmountPaid": 1500,
            },
            updated_at=timezone.now(),
        )
        collected, planned, _, _ = _resolve_month_rent(today.year, today.month)
        self.assertEqual(collected, Decimal("1500"))
        self.assertEqual(planned, Decimal("2000"))

    def test_apply_push_materializes_rent_payment(self):
        pay_id = uuid.uuid4()
        applied = apply_push(
            "RentPayments",
            [
                {
                    "id": str(pay_id),
                    "updatedAt": timezone.now().isoformat(),
                    "jsonData": (
                        '{"Year": 2026, "Month": 6, "AmountDue": 200, '
                        '"AmountPaid": 150, "IsLate": false}'
                    ),
                }
            ],
        )
        self.assertEqual(applied, 1)
        row = RentPayment.objects.get(id=pay_id)
        self.assertEqual(row.amount_paid, Decimal("150"))
        self.assertEqual(row.year, 2026)
        self.assertEqual(row.month, 6)

    def test_calendar_month_starts_six_months(self):
        starts = calendar_month_starts(date(2026, 5, 15), months=6)
        self.assertEqual(len(starts), 6)
        self.assertEqual(starts[-1], date(2026, 5, 1))

    def test_revenue_chart_from_orm_filtered(self):
        pay_id = uuid.uuid4()
        SyncedEntityStore.objects.create(
            id=pay_id,
            entity_type="RentPayments",
            json_data={},
            updated_at=timezone.now(),
        )
        RentPayment.objects.create(
            id=pay_id,
            year=2026,
            month=3,
            amount_due=Decimal("100"),
            amount_paid=Decimal("80"),
        )
        chart = revenue_chart_from_orm([date(2026, 3, 1)])
        self.assertEqual(chart[0]["value"], 80.0)

    def test_rematerialize_entity_type(self):
        tx_id = uuid.uuid4()
        SyncedEntityStore.objects.create(
            id=tx_id,
            entity_type="FinancialTransactions",
            json_data={
                "Type": 2,
                "Category": "Test",
                "Description": "Dépense test",
                "Amount": 42,
                "TransactionDate": timezone.now().isoformat(),
            },
            updated_at=timezone.now(),
        )
        n = rematerialize_entity_type("FinancialTransactions")
        self.assertEqual(n, 1)
        tx = FinancialTransaction.objects.get(id=tx_id)
        self.assertEqual(tx.type, FinancialTransaction.TxType.DEPENSE)
        self.assertEqual(tx.amount, Decimal("42"))

    def test_ledger_sync_store_rent_fallback(self):
        today = timezone.localdate()
        SyncedEntityStore.objects.create(
            id=uuid.uuid4(),
            entity_type="FinancialTransactions",
            json_data={
                "Type": 1,
                "Category": "Loyers",
                "Amount": 888,
                "TransactionDate": today.isoformat(),
            },
            updated_at=timezone.now(),
        )
        collected, _, _, _ = _resolve_month_rent(today.year, today.month)
        self.assertEqual(collected, Decimal("888"))
