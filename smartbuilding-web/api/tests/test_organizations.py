"""Tests multi-tenant — organisations et isolation sync."""

import uuid

from django.test import TestCase
from rest_framework.test import APIClient

from api.models import Organization, SyncedEntityStore, User
from api.organization_context import get_default_organization, resolve_organization_id
from api.sync.registry import apply_push, get_changes_since


class OrganizationApiTests(TestCase):
    def setUp(self):
        self.client = APIClient()
        self.admin = User.objects.create_user(
            username="admin",
            password="Admin@2026",
            role=User.Role.ADMIN,
            is_staff=True,
        )
        self.client.force_authenticate(user=self.admin)
        self.org_a = Organization.objects.create(
            id=uuid.uuid4(),
            name="Résidence Kinshasa",
            slug="kinshasa",
            database_name="sbms_kinshasa",
            is_active=True,
        )
        self.org_b = Organization.objects.create(
            id=uuid.uuid4(),
            name="Résidence Gombe",
            slug="gombe",
            database_name="sbms_gombe",
            is_active=True,
        )

    def test_list_organizations_pdg_only(self):
        res = self.client.get("/api/organizations/")
        self.assertEqual(res.status_code, 200)
        body = res.json()
        self.assertTrue(body["success"])
        self.assertGreaterEqual(len(body["data"]), 2)

    def test_register_organization_from_desktop(self):
        new_id = uuid.uuid4()
        res = self.client.post(
            "/api/organizations/register/",
            {
                "id": str(new_id),
                "name": "Immeuble Matadi",
                "slug": "matadi",
                "databaseName": "sbms_matadi",
                "city": "Matadi",
            },
            format="json",
            HTTP_X_ORGANIZATION_ID=str(new_id),
        )
        self.assertEqual(res.status_code, 200)
        self.assertTrue(Organization.objects.filter(id=new_id).exists())

    def test_sync_push_scoped_by_organization(self):
        entity_id = uuid.uuid4()
        applied = apply_push(
            "Tenants",
            [
                {
                    "id": str(entity_id),
                    "updatedAt": "2026-06-15T10:00:00+00:00",
                    "jsonData": '{"Name": "Locataire A"}',
                }
            ],
            organization_id=self.org_a.id,
        )
        self.assertEqual(applied, 1)
        store = SyncedEntityStore.objects.get(id=entity_id)
        self.assertEqual(store.organization_id, self.org_a.id)

        changes_b = get_changes_since("Tenants", "1970-01-01T00:00:00Z", organization_id=self.org_b.id)
        self.assertEqual(len(changes_b), 0)

        changes_a = get_changes_since("Tenants", "1970-01-01T00:00:00Z", organization_id=self.org_a.id)
        self.assertEqual(len(changes_a), 1)

    def test_default_organization_exists(self):
        default = get_default_organization()
        self.assertIsNotNone(default.id)

    def test_resolve_organization_from_header(self):
        from django.test import RequestFactory

        factory = RequestFactory()
        request = factory.get("/", HTTP_X_ORGANIZATION_ID=str(self.org_b.id))
        request.user = self.admin
        resolved = resolve_organization_id(request)
        self.assertEqual(resolved, self.org_b.id)

    def test_cross_tenant_header_denied_for_regular_user(self):
        from django.test import RequestFactory
        from rest_framework.exceptions import PermissionDenied

        user = User.objects.create_user(
            username="comptable",
            password="secret",
            role=User.Role.COMPTABLE,
        )
        factory = RequestFactory()
        request = factory.get("/", HTTP_X_ORGANIZATION_ID=str(self.org_b.id))
        request.user = user
        with self.assertRaises(PermissionDenied):
            resolve_organization_id(request)

    def test_dashboard_isolated_by_organization(self):
        from api.services.dashboard import get_executive_summary
        from api.sync.registry import apply_push

        tenant_a = uuid.uuid4()
        tenant_b = uuid.uuid4()
        apply_push(
            "Tenants",
            [
                {
                    "id": str(tenant_a),
                    "updatedAt": "2026-06-15T10:00:00+00:00",
                    "jsonData": '{"Name": "Tenant Org A"}',
                }
            ],
            organization_id=self.org_a.id,
        )
        apply_push(
            "Tenants",
            [
                {
                    "id": str(tenant_b),
                    "updatedAt": "2026-06-15T10:00:00+00:00",
                    "jsonData": '{"Name": "Tenant Org B"}',
                }
            ],
            organization_id=self.org_b.id,
        )
        summary_a = get_executive_summary(organization_id=self.org_a.id)
        summary_b = get_executive_summary(organization_id=self.org_b.id)
        self.assertEqual(summary_a["totalTenants"], 1)
        self.assertEqual(summary_b["totalTenants"], 1)

    def test_dashboard_uses_orm_when_sync_not_tagged_for_org(self):
        """Les onglets lisent l'ORM complet ; le dashboard doit faire de même sans sync tagué."""
        from decimal import Decimal

        from api.models import RentPayment, Tenant
        from api.services.dashboard import get_executive_summary

        Tenant.objects.create(
            id=uuid.uuid4(),
            name="Locataire ORM seul",
            rental_status="Actif",
        )
        RentPayment.objects.create(
            year=2026,
            month=6,
            amount_due=Decimal("500"),
            amount_paid=Decimal("500"),
            is_late=False,
        )
        summary = get_executive_summary(organization_id=self.org_a.id)
        self.assertGreaterEqual(summary["totalTenants"], 1)
        self.assertGreater(summary["rentCollectedTotal"], 0)
