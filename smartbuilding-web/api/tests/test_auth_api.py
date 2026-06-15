"""Tests authentification API (format erreurs + session)."""

import uuid

from django.test import Client, TestCase

from api.models import Organization, SyncedEntityStore, User


class AuthApiFormatTests(TestCase):
    def test_overview_without_auth_returns_json_envelope(self):
        res = self.client.get("/api/executive/overview/")
        self.assertEqual(res.status_code, 401)
        body = res.json()
        self.assertFalse(body["success"])
        self.assertIn("authentification", body["message"].lower())

    def test_session_check_anonymous(self):
        res = self.client.get("/api/auth/session/")
        self.assertEqual(res.status_code, 200)
        self.assertTrue(res.json()["success"])
        self.assertFalse(res.json()["data"]["authenticated"])

    def test_login_without_csrf_token_succeeds_in_production_mode(self):
        """Le POST login ne doit pas exiger CSRF (endpoint JWT, pas SessionAuthentication)."""
        org = Organization.objects.create(
            id=uuid.uuid4(),
            name="Test Org",
            slug="test-org",
            database_name="sbms_test",
            is_active=True,
        )
        user = User.objects.create_user(
            username="comptable_csrf",
            password="secret",
            role=User.Role.COMPTABLE,
        )
        SyncedEntityStore.objects.create(
            id=user.id,
            entity_type="Users",
            organization_id=org.id,
            json_data='{"Username": "comptable_csrf"}',
        )
        client = Client(enforce_csrf_checks=True)
        res = client.post(
            "/api/auth/login/",
            {"username": "comptable_csrf", "password": "secret"},
            content_type="application/json",
        )
        self.assertEqual(res.status_code, 200, res.content)
        body = res.json()
        self.assertTrue(body["success"])
        self.assertIn("token", body["data"])

    def test_login_page_sets_csrf_cookie(self):
        res = self.client.get("/login/")
        self.assertEqual(res.status_code, 200)
        self.assertContains(res, "csrfmiddlewaretoken")
        self.assertIn("csrftoken", res.cookies)
