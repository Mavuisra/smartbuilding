"""Résolution des comptes cloud à partir de l'ORM et du magasin sync desktop."""

from __future__ import annotations

from django.db.models import Q

from api.models import SyncedEntityStore, User
from api.sync.materializers import materialize_user
from api.sync.utils import inject_entity_id, pick


def _username_from_sync_payload(data: dict) -> str:
    return (pick(data, "Username", "username") or "").strip()


def find_sync_user_stores(username: str):
    """Entrées Users du magasin sync correspondant au nom d'utilisateur."""
    normalized = (username or "").strip()
    if not normalized:
        return SyncedEntityStore.objects.none()

    qs = SyncedEntityStore.objects.filter(
        entity_type="Users",
        deleted_at__isnull=True,
    )
    direct = qs.filter(
        Q(json_data__Username=normalized)
        | Q(json_data__username=normalized)
        | Q(json_data__Username__iexact=normalized)
        | Q(json_data__username__iexact=normalized)
    )
    if direct.exists():
        return direct

    lower = normalized.lower()
    matched_ids = []
    for row in qs.only("id", "json_data").iterator():
        data = row.json_data if isinstance(row.json_data, dict) else {}
        if _username_from_sync_payload(data).lower() == lower:
            matched_ids.append(row.id)
    if not matched_ids:
        return SyncedEntityStore.objects.none()
    return SyncedEntityStore.objects.filter(id__in=matched_ids)


def materialize_user_from_sync_stores(stores) -> User | None:
    for row in stores.order_by("-updated_at"):
        data = row.json_data if isinstance(row.json_data, dict) else {}
        if not data:
            continue
        materialize_user(inject_entity_id(data, row.id))
        username = _username_from_sync_payload(data)
        if not username:
            continue
        user = (
            User.objects.filter(
                username__iexact=username,
                is_active=True,
                deleted_at__isnull=True,
            )
            .order_by("-updated_at")
            .first()
        )
        if user:
            return user
    return None


def resolve_cloud_login_user(username: str) -> User | None:
    """
    Trouve un utilisateur pour la connexion web :
    1. ORM actif
    2. Sinon matérialisation depuis le magasin sync (push desktop).
    """
    normalized = (username or "").strip()
    if not normalized:
        return None

    user = (
        User.objects.filter(
            username__iexact=normalized,
            is_active=True,
            deleted_at__isnull=True,
        )
        .order_by("-updated_at")
        .first()
    )
    if user:
        return user

    stores = find_sync_user_stores(normalized)
    if not stores.exists():
        return None
    return materialize_user_from_sync_stores(stores)


def sync_store_organization_ids_for_user(user) -> list:
    """Organisations où l'utilisateur apparaît dans le magasin sync Users."""
    if not user:
        return []

    stores = SyncedEntityStore.objects.filter(
        entity_type="Users",
        deleted_at__isnull=True,
        id=user.id,
    )
    if not stores.exists():
        username = (getattr(user, "username", "") or "").strip()
        if username:
            stores = find_sync_user_stores(username)

    return list(
        stores.exclude(organization_id__isnull=True)
        .values_list("organization_id", flat=True)
        .distinct()
    )
