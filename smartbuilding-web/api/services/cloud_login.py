"""Appartenance tenant standard — toute organisation enregistrée (database_name + UUID)."""

from __future__ import annotations

from uuid import UUID

from django.db.models import Count, Q

from api.models import Organization, SyncedEntityStore, User
from api.organization_context import DEFAULT_ORG_ID
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


def normalize_database_name(value: str) -> str:
    return (value or "").strip().lower()


def organizations_with_sync_activity():
    """Organisations actives ayant déjà reçu des données desktop."""
    return (
        Organization.objects.filter(deleted_at__isnull=True, is_active=True)
        .annotate(sync_rows=Count("synced_entities", filter=Q(synced_entities__deleted_at__isnull=True)))
        .filter(sync_rows__gt=0)
        .order_by("-sync_rows", "name")
    )


def infer_organization_id_for_username(username: str) -> UUID | None:
    """
    Déduit le tenant d'un utilisateur (règles génériques, aucun nom codé en dur).
    Priorité : sync Users déjà tagué → créateur de l'org → org unique avec données.
    """
    normalized = (username or "").strip()
    if not normalized:
        return None

    tagged_ids = list(
        find_sync_user_stores(normalized)
        .exclude(organization_id__isnull=True)
        .values_list("organization_id", flat=True)
        .distinct()
    )
    if len(tagged_ids) == 1:
        return tagged_ids[0]

    creator_org = (
        Organization.objects.filter(
            created_by_username__iexact=normalized,
            deleted_at__isnull=True,
            is_active=True,
        )
        .order_by("name")
        .first()
    )
    if creator_org:
        return creator_org.id

    active = list(organizations_with_sync_activity().exclude(id=DEFAULT_ORG_ID))
    if len(active) == 1:
        return active[0].id

    org_ids_with_sync = list(
        SyncedEntityStore.objects.filter(deleted_at__isnull=True)
        .exclude(organization_id__isnull=True)
        .values_list("organization_id", flat=True)
        .distinct()
    )
    if len(org_ids_with_sync) == 1:
        return org_ids_with_sync[0]

    return None


def repair_orphan_user_sync_links(username: str) -> int:
    """Rattache les entrées Users sync sans organization_id au tenant déduit."""
    org_id = infer_organization_id_for_username(username)
    if not org_id:
        return 0
    return find_sync_user_stores(username).filter(organization_id__isnull=True).update(
        organization_id=org_id
    )


def ensure_user_tenant_membership(user: User) -> None:
    """Garantit le lien utilisateur ↔ tenant(s) avant d'émettre le JWT."""
    username = (getattr(user, "username", "") or "").strip()
    if not username:
        return
    repair_orphan_user_sync_links(username)


def sync_store_organization_ids_for_user(user) -> list:
    """UUIDs des organisations où l'utilisateur est membre (via magasin sync Users)."""
    if not user:
        return []

    username = (getattr(user, "username", "") or "").strip()
    if username:
        repair_orphan_user_sync_links(username)

    stores = SyncedEntityStore.objects.filter(
        entity_type="Users",
        deleted_at__isnull=True,
        id=user.id,
    )
    if not stores.exists() and username:
        stores = find_sync_user_stores(username)

    return list(
        stores.exclude(organization_id__isnull=True)
        .values_list("organization_id", flat=True)
        .distinct()
    )


def resolve_cloud_login_user(username: str) -> User | None:
    """
    Compte cloud pour connexion :
    1. ORM actif
    2. Matérialisation depuis le magasin sync desktop (tous tenants).
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
        ensure_user_tenant_membership(user)
        return user

    stores = find_sync_user_stores(normalized)
    if not stores.exists():
        return None

    user = materialize_user_from_sync_stores(stores)
    if user:
        ensure_user_tenant_membership(user)
    return user


def organization_ids_for_database_names(database_names: list[str]) -> list[UUID]:
    """Résolution standard org ↔ base MySQL desktop (ex. sbms_*)."""
    normalized = {normalize_database_name(n) for n in database_names if normalize_database_name(n)}
    if not normalized:
        return []
    orgs = Organization.objects.filter(deleted_at__isnull=True, is_active=True)
    matched = []
    for org in orgs:
        if normalize_database_name(org.database_name) in normalized:
            matched.append(org.id)
    return matched
