"""Tests connexion cloud — modèle standard multi-tenant (database_name + organization_id)."""

import uuid

from django.test import TestCase, override_settings

from api.models import Organization, SyncedEntityStore, User
from api.organization_context import resolve_user_organizations
from api.services.cloud_login import (
    ensure_user_tenant_membership,
    organization_ids_for_database_names,
    repair_orphan_user_sync_links,
    resolve_cloud_login_user,
)


def _bcrypt_hash(password: str) -> str:
    probe = User(username="_probe")
    probe.set_password(password)
    return probe.password_hash_sync


class StandardTenantMembershipTests(TestCase):
    def _create_org(self, name: str, database_name: str) -> Organization:
        slug = database_name.replace("sbms_", "").replace("_", "-")
        return Organization.objects.create(
            id=uuid.uuid4(),
            name=name,
            slug=slug,
            database_name=database_name,
            is_active=True,
        )

    def _store_user(
        self,
        org: Organization,
        username: str,
        password: str,
        *,
        user_id=None,
        tag_organization: bool = True,
    ):
        password_hash = _bcrypt_hash(password)
        SyncedEntityStore.objects.create(
            id=user_id or uuid.uuid4(),
            entity_type="Users",
            organization_id=org.id if tag_organization else None,
            json_data={
                "Username": username,
                "PasswordHash": password_hash,
                "FullName": username.title(),
                "Role": 2,
                "IsActive": True,
            },
        )

    def test_organization_ids_for_database_names_is_generic(self):
        org_a = self._create_org("Tenant A", "sbms_tenant_a")
        org_b = self._create_org("Tenant B", "sbms_tenant_b")
        ids = organization_ids_for_database_names(["sbms_tenant_a", "sbms_unknown"])
        self.assertEqual(ids, [org_a.id])
        self.assertNotIn(org_b.id, ids)

    @override_settings(DEBUG=False)
    def test_login_works_for_any_registered_tenant_database(self):
        org = self._create_org("Immeuble Alpha", "sbms_alpha")
        self._store_user(org, "comptable_alpha", "Secret@2026")

        res = self.client.post(
            "/api/auth/login/",
            {"username": "comptable_alpha", "password": "Secret@2026"},
            format="json",
        )
        self.assertEqual(res.status_code, 200, res.content)
        body = res.json()
        self.assertEqual(body["data"]["organizationId"], str(org.id))

    def test_repair_orphan_user_links_when_single_tenant_has_sync_data(self):
        org = self._create_org("Immeuble Beta", "sbms_beta")
        self._store_user(org, "gestionnaire_beta", "Pass@2026", tag_organization=False)
        SyncedEntityStore.objects.create(
            id=uuid.uuid4(),
            entity_type="Tenants",
            organization_id=org.id,
            json_data={"Name": "Locataire beta"},
        )

        repaired = repair_orphan_user_sync_links("gestionnaire_beta")
        self.assertEqual(repaired, 1)
        user = resolve_cloud_login_user("gestionnaire_beta")
        self.assertIsNotNone(user)
        ensure_user_tenant_membership(user)
        orgs = resolve_user_organizations(user)
        self.assertEqual(len(orgs), 1)
        self.assertEqual(orgs[0].database_name, "sbms_beta")

    def test_resolve_cloud_login_user_any_tenant(self):
        org = self._create_org("Immeuble Gamma", "sbms_gamma")
        self._store_user(org, "tech_gamma", "Tech@2026")
        user = resolve_cloud_login_user("tech_gamma")
        self.assertIsNotNone(user)
        self.assertEqual(resolve_user_organizations(user)[0].id, org.id)

    def test_super_admin_modules_respect_selected_organization(self):
        from api.services.web_desktop_modules import load_rapports, load_users

        org_a = self._create_org("Tenant A", "sbms_tenant_a")
        org_b = self._create_org("Tenant B", "sbms_tenant_b")
        self._store_user(org_a, "user_a", "Pass@2026")
        self._store_user(org_b, "user_b", "Pass@2026")

        SyncedEntityStore.objects.create(
            id=uuid.uuid4(),
            entity_type="Tenants",
            organization_id=org_a.id,
            json_data={"Name": "Locataire A seul"},
        )
        SyncedEntityStore.objects.create(
            id=uuid.uuid4(),
            entity_type="Tenants",
            organization_id=org_b.id,
            json_data={"Name": "Locataire B seul"},
        )

        users_a = load_users(organization_id=org_a.id)
        users_b = load_users(organization_id=org_b.id)
        self.assertEqual(users_a["totalCount"], 1)
        self.assertEqual(users_b["totalCount"], 1)
        self.assertEqual(users_a["users"][0]["username"], "user_a")
        self.assertEqual(users_b["users"][0]["username"], "user_b")

        rapports_a = load_rapports(organization_id=org_a.id)
        rapports_b = load_rapports(organization_id=org_b.id)
        sections_a = {s["label"]: s["rows"] for s in rapports_a["sections"]}
        sections_b = {s["label"]: s["rows"] for s in rapports_b["sections"]}
        self.assertEqual(len(sections_a.get("Loyers", [])), 0)
        self.assertEqual(len(sections_b.get("Loyers", [])), 0)
        self.assertEqual(rapports_a["financierSummary"]["loyersEncaisses"], 0.0)
        self.assertEqual(rapports_b["financierSummary"]["loyersEncaisses"], 0.0)
