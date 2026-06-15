"""Tests cohérence dashboard web ↔ magasin sync (données Desktop)."""

import uuid
from datetime import date
from decimal import Decimal
from unittest.mock import patch

from django.test import TestCase
from django.utils import timezone

from api.models import FinancialTransaction, Premise, RentPayment, SyncedEntityStore, User
from api.services.dashboard import get_executive_summary
from api.services.sync_metrics import (
    calendar_month_starts,
    ensure_dashboard_orm_materialized,
    rent_from_orm,
    rent_month_totals,
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

    def test_rent_month_totals_dedupes_duplicate_sync_rows(self):
        today = timezone.localdate()
        lease_id = str(uuid.uuid4())
        for _ in range(7):
            SyncedEntityStore.objects.create(
                id=uuid.uuid4(),
                entity_type="RentPayments",
                json_data={
                    "Year": today.year,
                    "Month": today.month,
                    "AmountDue": 10000,
                    "AmountPaid": 10000,
                    "LeaseContractId": lease_id,
                },
                updated_at=timezone.now(),
            )
        collected, planned, _, _ = rent_month_totals(today.year, today.month)
        self.assertEqual(collected, Decimal("10000"))
        self.assertEqual(planned, Decimal("10000"))

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

    def test_apply_push_rent_payment_missing_lease_no_fk_error(self):
        """Bail absent en ORM → paiement accepté sans FK (plus de HTTP 500)."""
        pay_id = uuid.uuid4()
        missing_lease = uuid.uuid4()
        applied = apply_push(
            "RentPayments",
            [
                {
                    "id": str(pay_id),
                    "updatedAt": timezone.now().isoformat(),
                    "jsonData": (
                        f'{{"Year": 2026, "Month": 6, "AmountDue": 500, '
                        f'"AmountPaid": 500, "LeaseContractId": "{missing_lease}"}}'
                    ),
                }
            ],
        )
        self.assertEqual(applied, 1)
        row = RentPayment.objects.get(id=pay_id)
        self.assertIsNone(row.lease_contract_id)
        self.assertEqual(str(row.lease_contract_id_sync), str(missing_lease))

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

    def test_rent_month_uses_deduped_ledger_when_no_rent_payments(self):
        from datetime import date

        today = timezone.localdate()
        prev_month = today.month - 1 if today.month > 1 else 12
        prev_year = today.year if today.month > 1 else today.year - 1
        for desc, y, m in (
            ("Loyer 05/2026 — CC", today.year, today.month),
            ("Loyer 06/2026 — CC", prev_year, prev_month),
        ):
            for _ in range(4):
                SyncedEntityStore.objects.create(
                    id=uuid.uuid4(),
                    entity_type="FinancialTransactions",
                    json_data={
                        "Type": 1,
                        "Category": "Loyers",
                        "Description": desc,
                        "Amount": 10000,
                        "TransactionDate": date(y, m, 15).isoformat(),
                    },
                    updated_at=timezone.now(),
                )
        collected, _, _, _ = rent_month_totals(today.year, today.month)
        self.assertEqual(collected, Decimal("10000"))

    def test_ensure_dashboard_skips_rebuild_when_orm_matches_sync(self):
        pay_id = uuid.uuid4()
        SyncedEntityStore.objects.create(
            id=pay_id,
            entity_type="RentPayments",
            json_data={"Year": 2026, "Month": 6, "AmountDue": 100, "AmountPaid": 100},
            updated_at=timezone.now(),
        )
        RentPayment.objects.create(
            id=pay_id,
            year=2026,
            month=6,
            amount_due=Decimal("100"),
            amount_paid=Decimal("100"),
        )
        with patch("api.sync.registry.rematerialize_entity_type") as mock_rebuild:
            rebuilt = ensure_dashboard_orm_materialized()
        mock_rebuild.assert_not_called()
        self.assertEqual(rebuilt, 0)

    def test_ensure_dashboard_rebuilds_only_mismatched_entity_types(self):
        pay_id = uuid.uuid4()
        SyncedEntityStore.objects.create(
            id=pay_id,
            entity_type="RentPayments",
            json_data={"Year": 2026, "Month": 6, "AmountDue": 100, "AmountPaid": 100},
            updated_at=timezone.now(),
        )
        with patch("api.sync.registry.rematerialize_entity_type", return_value=1) as mock_rebuild:
            rebuilt = ensure_dashboard_orm_materialized()
        mock_rebuild.assert_called_once_with("RentPayments", None)
        self.assertEqual(rebuilt, 1)


class DashboardModuleApiTests(TestCase):
    def setUp(self):
        self.user = User.objects.create_user(
            username="Jessica",
            password="Admin@2026",
            role=User.Role.PDG,
            full_name="Jessica",
            is_staff=True,
            is_superuser=True,
        )

    def test_dashboard_module_returns_json_envelope(self):
        self.client.force_login(self.user)
        res = self.client.get("/api/executive/modules/dashboard/")
        self.assertEqual(res.status_code, 200)
        self.assertEqual(res["Content-Type"], "application/json")
        body = res.json()
        self.assertTrue(body["success"])
        self.assertIn("summary", body["data"])

    def test_fmt_datetime_accepts_iso_strings(self):
        from api.services.web_desktop_modules import _fmt_datetime

        result = _fmt_datetime("2026-06-15T10:30:00+00:00")
        self.assertIn("15/06/2026", result)
        self.assertRegex(result, r"\d{2}:\d{2}:\d{2}")
