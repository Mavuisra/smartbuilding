"""Tests connexion cloud pour utilisateurs synchronisés depuis le desktop."""

import uuid

from django.test import TestCase, override_settings

from api.models import Organization, SyncedEntityStore, User
from api.organization_context import resolve_user_organizations
from api.services.cloud_login import resolve_cloud_login_user


def _bcrypt_hash(password: str) -> str:
    probe = User(username="_probe")
    probe.set_password(password)
    return probe.password_hash_sync


class CloudTenantUserLoginTests(TestCase):
    def setUp(self):
        self.org = Organization.objects.create(
            id=uuid.uuid4(),
            name="Résidence Blooom",
            slug="blooom",
            database_name="sbms_blooom",
            is_active=True,
        )
        self.desktop_user_id = uuid.uuid4()

    def _store_user(self, username: str, password: str, *, user_id=None):
        password_hash = _bcrypt_hash(password)
        SyncedEntityStore.objects.create(
            id=user_id or self.desktop_user_id,
            entity_type="Users",
            organization_id=self.org.id,
            json_data={
                "Username": username,
                "PasswordHash": password_hash,
                "FullName": username.title(),
                "Role": 2,
                "IsActive": True,
            },
        )

    @override_settings(DEBUG=False)
    def test_login_materializes_user_from_sync_store_only(self):
        self._store_user("comptable_blooom", "Secret@2026")
        self.assertFalse(User.objects.filter(username__iexact="comptable_blooom").exists())

        res = self.client.post(
            "/api/auth/login/",
            {"username": "comptable_blooom", "password": "Secret@2026"},
            format="json",
        )
        self.assertEqual(res.status_code, 200, res.content)
        body = res.json()
        self.assertTrue(body["success"])
        self.assertFalse(body["data"]["canSwitchOrganizations"])
        self.assertEqual(body["data"]["organizationId"], str(self.org.id))

    def test_org_resolution_uses_username_when_uuid_differs(self):
        self._store_user("gestionnaire1", "Pass@2026")
        cloud_user = User.objects.create_user(
            username="gestionnaire1",
            password="ignored",
            role=User.Role.GESTIONNAIRE,
        )
        cloud_user.password_hash_sync = ""
        cloud_user.save()

        orgs = resolve_user_organizations(cloud_user)
        self.assertEqual(len(orgs), 1)
        self.assertEqual(orgs[0].id, self.org.id)

    def test_resolve_cloud_login_user_finds_sync_only_account(self):
        self._store_user("tech_tenant", "Tech@2026")
        user = resolve_cloud_login_user("tech_tenant")
        self.assertIsNotNone(user)
        self.assertEqual(user.username.lower(), "tech_tenant")
