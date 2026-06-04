"""Réinitialisation complète de la base Django (SQLite locale ou PostgreSQL Render)."""

from __future__ import annotations

import os

from django.apps import apps
from django.conf import settings
from django.contrib.auth import get_user_model
from django.core.management import call_command
from django.db import connection, transaction

User = get_user_model()

CONFIRM_PHRASE = "REINITIALISER BLOOM"

# Modèles métier à vider (ordre : enfants d'abord)
_RESET_ORDER = [
    "ServerSyncEvent",
    "ExecutiveNotification",
    "SyncedEntityStore",
    "RentPayment",
    "LeaseContract",
    "FinancialTransaction",
    "ConsumptionRecord",
    "Incident",
    "Equipment",
    "InventoryItem",
    "Visitor",
    "Employee",
    "Supplier",
    "Premise",
    "Tenant",
    "Building",
    "User",
]


def is_render_host() -> bool:
    return os.getenv("RENDER", "").strip().lower() in ("1", "true", "yes")


def database_info() -> dict:
    engine = connection.settings_dict.get("ENGINE", "")
    name = connection.settings_dict.get("NAME", "")
    is_sqlite = "sqlite" in engine
    return {
        "engine": "sqlite" if is_sqlite else "postgresql",
        "engineLabel": "SQLite (fichier local)" if is_sqlite else "PostgreSQL (cloud)",
        "name": str(name),
        "isRender": is_render_host(),
        "canResetRemote": not is_render_host(),
        "remoteApiUrl": (os.getenv("SBMS_REMOTE_API_URL") or "https://smartbuilding-0kbk.onrender.com").rstrip("/"),
        "confirmPhrase": CONFIRM_PHRASE,
    }


def _model_counts() -> dict[str, int]:
    counts = {}
    for label in _RESET_ORDER:
        try:
            model = apps.get_model("api", label)
            counts[label] = model.objects.count()
        except LookupError:
            continue
    return counts


@transaction.atomic
def reset_application_database(*, reseed_accounts: bool = True) -> dict:
    """
    Supprime toutes les données applicatives et recrée les comptes par défaut.
  """
    before = _model_counts()
    deleted_total = 0

    for label in _RESET_ORDER:
        try:
            model = apps.get_model("api", label)
        except LookupError:
            continue
        if label == "User" and not reseed_accounts:
            deleted, _ = model.objects.all().delete()
        else:
            deleted, _ = model.objects.all().delete()
        deleted_total += deleted

    if reseed_accounts:
        os.environ["SBMS_RUN_SEED"] = "1"
        try:
            call_command("seed_smartbuilding", verbosity=0)
        finally:
            os.environ.pop("SBMS_RUN_SEED", None)

    after = _model_counts()
    return {
        "deletedRecords": deleted_total,
        "countsBefore": before,
        "countsAfter": after,
        "reseeded": reseed_accounts,
        "engine": database_info()["engine"],
    }


def user_may_reset_database(user) -> bool:
    if not user or not user.is_authenticated:
        return False
    if getattr(user, "is_superuser", False):
        return True
    role = getattr(user, "role", "") or ""
    return role in (User.Role.ADMIN, User.Role.PDG, "Administrateur", "PDG")
