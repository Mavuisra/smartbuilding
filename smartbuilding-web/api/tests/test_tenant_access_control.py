"""Contrôle d'accès multi-tenant — Jessica super admin, isolation stricte pour les autres."""

import uuid

from django.test import RequestFactory, TestCase
from rest_framework.exceptions import PermissionDenied
from rest_framework.test import APIClient

from api.models import Organization, SyncedEntityStore, User
from api.organization_context import (
    default_organization_for_user,
    resolve_organization_id,
    resolve_user_organizations,
    user_is_tenant_super_admin,
)
from api.services.dashboard import get_executive_summary
from api.sync.registry import apply_push


def link_user_to_org(user: User, org: Organization) -> None:
    SyncedEntityStore.objects.update_or_create(
        id=user.id,
        defaults={
            "entity_type": "Users",
            "organization_id": org.id,
            "json_data": f'{{"Username": "{user.username}"}}',
        },
    )


class TenantAccessControlTests(TestCase):
    def setUp(self):
        self.client = APIClient()
        self.factory = RequestFactory()

        self.jessica = User.objects.create_user(
            username="Jessica",
            password="Admin@2026",
            role=User.Role.PDG,
            is_staff=True,
            is_superuser=True,
        )
        self.comptable = User.objects.create_user(
            username="comptable_kin",
            password="secret",
            role=User.Role.COMPTABLE,
        )
        self.admin = User.objects.create_user(
            username="admin",
            password="Admin@2026",
            role=User.Role.ADMIN,
            is_staff=True,
        )

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

        link_user_to_org(self.comptable, self.org_a)

    def test_jessica_is_only_tenant_super_admin(self):
        self.assertTrue(user_is_tenant_super_admin(self.jessica))
        self.assertFalse(user_is_tenant_super_admin(self.admin))
        self.assertFalse(user_is_tenant_super_admin(self.comptable))

    def test_jessica_sees_all_organizations(self):
        orgs = resolve_user_organizations(self.jessica)
        ids = {o.id for o in orgs}
        self.assertIn(self.org_a.id, ids)
        self.assertIn(self.org_b.id, ids)

    def test_comptable_sees_only_assigned_organization(self):
        orgs = resolve_user_organizations(self.comptable)
        self.assertEqual(len(orgs), 1)
        self.assertEqual(orgs[0].id, self.org_a.id)

    def test_admin_without_sync_has_no_organizations(self):
        self.assertEqual(resolve_user_organizations(self.admin), [])

    def test_login_jessica_returns_can_switch_organizations(self):
        res = self.client.post(
            "/api/auth/login/",
            {"username": "Jessica", "password": "Admin@2026"},
            format="json",
        )
        self.assertEqual(res.status_code, 200)
        body = res.json()
        self.assertTrue(body["success"])
        self.assertTrue(body["data"]["canSwitchOrganizations"])
        self.assertGreaterEqual(len(body["data"]["organizations"]), 2)

    def test_login_comptable_cannot_switch_organizations(self):
        res = self.client.post(
            "/api/auth/login/",
            {"username": "comptable_kin", "password": "secret"},
            format="json",
        )
        self.assertEqual(res.status_code, 200)
        body = res.json()
        self.assertTrue(body["success"])
        self.assertFalse(body["data"]["canSwitchOrganizations"])
        self.assertEqual(len(body["data"]["organizations"]), 1)
        self.assertEqual(body["data"]["organizationId"], str(self.org_a.id))

    def test_list_organizations_api_jessica_vs_comptable(self):
        self.client.force_authenticate(user=self.jessica)
        res_j = self.client.get("/api/organizations/")
        self.assertEqual(res_j.status_code, 200)
        self.assertGreaterEqual(len(res_j.json()["data"]), 2)

        self.client.force_authenticate(user=self.comptable)
        res_c = self.client.get("/api/organizations/")
        self.assertEqual(res_c.status_code, 200)
        data_c = res_c.json()["data"]
        self.assertEqual(len(data_c), 1)
        self.assertEqual(data_c[0]["id"], str(self.org_a.id))

    def test_comptable_default_org_without_header(self):
        request = self.factory.get("/")
        request.user = self.comptable
        resolved = resolve_organization_id(request)
        self.assertEqual(resolved, self.org_a.id)

    def test_comptable_cannot_access_other_tenant_via_header(self):
        request = self.factory.get("/", HTTP_X_ORGANIZATION_ID=str(self.org_b.id))
        request.user = self.comptable
        with self.assertRaises(PermissionDenied):
            resolve_organization_id(request)

    def test_jessica_can_access_any_tenant_via_header(self):
        request = self.factory.get("/", HTTP_X_ORGANIZATION_ID=str(self.org_b.id))
        request.user = self.jessica
        resolved = resolve_organization_id(request)
        self.assertEqual(resolved, self.org_b.id)

    def test_admin_cannot_access_unassigned_tenant(self):
        request = self.factory.get("/", HTTP_X_ORGANIZATION_ID=str(self.org_a.id))
        request.user = self.admin
        with self.assertRaises(PermissionDenied):
            resolve_organization_id(request)

    def test_dashboard_data_isolated_between_tenants(self):
        tenant_a = uuid.uuid4()
        tenant_b = uuid.uuid4()
        apply_push(
            "Tenants",
            [
                {
                    "id": str(tenant_a),
                    "updatedAt": "2026-06-15T10:00:00+00:00",
                    "jsonData": '{"Name": "Locataire Kin"}',
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
                    "jsonData": '{"Name": "Locataire Gombe"}',
                }
            ],
            organization_id=self.org_b.id,
        )

        summary_a = get_executive_summary(organization_id=self.org_a.id)
        summary_b = get_executive_summary(organization_id=self.org_b.id)
        self.assertEqual(summary_a["totalTenants"], 1)
        self.assertEqual(summary_b["totalTenants"], 1)

    def test_comptable_dashboard_api_scoped_to_own_tenant(self):
        apply_push(
            "Tenants",
            [
                {
                    "id": str(uuid.uuid4()),
                    "updatedAt": "2026-06-15T10:00:00+00:00",
                    "jsonData": '{"Name": "Kin seul"}',
                }
            ],
            organization_id=self.org_a.id,
        )
        apply_push(
            "Tenants",
            [
                {
                    "id": str(uuid.uuid4()),
                    "updatedAt": "2026-06-15T10:00:00+00:00",
                    "jsonData": '{"Name": "Gombe seul"}',
                }
            ],
            organization_id=self.org_b.id,
        )

        self.client.force_authenticate(user=self.comptable)
        res_ok = self.client.get(
            "/api/dashboard/summary/",
            HTTP_X_ORGANIZATION_ID=str(self.org_a.id),
        )
        self.assertEqual(res_ok.status_code, 200)
        self.assertEqual(res_ok.json()["data"]["totalTenants"], 1)

        res_denied = self.client.get(
            "/api/dashboard/summary/",
            HTTP_X_ORGANIZATION_ID=str(self.org_b.id),
        )
        self.assertEqual(res_denied.status_code, 403)

    def test_comptable_default_org_for_user(self):
        org = default_organization_for_user(self.comptable)
        self.assertEqual(org.id, self.org_a.id)
