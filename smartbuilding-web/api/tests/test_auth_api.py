"""Tests authentification API (format erreurs + session)."""

from django.test import TestCase


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
