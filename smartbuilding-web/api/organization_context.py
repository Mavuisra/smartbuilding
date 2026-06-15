"""Contexte organisation (multi-tenant) pour le portail web et la sync cloud."""

from __future__ import annotations

import contextvars
import re
from uuid import UUID

from django.db import models
from django.utils import timezone
from rest_framework.exceptions import PermissionDenied

from api.models import Organization

DEFAULT_ORG_SLUG = "organisation-principale"
DEFAULT_ORG_ID = UUID("00000000-0000-0000-0000-000000000001")
SLUG_RE = re.compile(r"^[a-z0-9](?:[a-z0-9-]{0,78}[a-z0-9])?$")

# Seul ce compte peut parcourir tous les tenants sur le portail web.
TENANT_SUPER_ADMIN_USERNAMES = frozenset({"jessica"})

_current_org_id: contextvars.ContextVar[UUID | None] = contextvars.ContextVar(
    "current_org_id", default=None
)


def set_request_organization_id(organization_id: UUID | None):
    return _current_org_id.set(organization_id)


def reset_request_organization_id(token) -> None:
    _current_org_id.reset(token)


def get_request_organization_id() -> UUID | None:
    return _current_org_id.get()


def allows_legacy_orm_fallback(organization_id: UUID | None) -> bool:
    """ORM complet sans filtre sync — uniquement pour l'organisation par défaut (données héritées)."""
    return organization_id is None or organization_id == DEFAULT_ORG_ID


def get_default_organization() -> Organization:
    org, _ = Organization.objects.get_or_create(
        id=DEFAULT_ORG_ID,
        defaults={
            "slug": DEFAULT_ORG_SLUG,
            "name": "Organisation principale",
            "database_name": "sbms_local",
            "is_active": True,
            "created_at": timezone.now(),
            "updated_at": timezone.now(),
        },
    )
    return org


def parse_organization_id(value) -> UUID | None:
    if not value:
        return None
    try:
        return UUID(str(value))
    except (ValueError, TypeError, AttributeError):
        return None


def user_is_tenant_super_admin(user) -> bool:
    """Super administrateur multi-tenant (navigation entre tous les tenants)."""
    if not user or not getattr(user, "is_authenticated", False):
        return False
    username = (getattr(user, "username", "") or "").strip().lower()
    return username in TENANT_SUPER_ADMIN_USERNAMES


def user_can_list_all_organizations(user) -> bool:
    return user_is_tenant_super_admin(user)


def user_can_access_organization(user, organization_id: UUID) -> bool:
    """Jessica : toutes les orgs. Autres : orgs assignées via sync ou propriétaire."""
    if not user or not getattr(user, "is_authenticated", False):
        return False
    if user_can_list_all_organizations(user):
        return Organization.objects.filter(
            id=organization_id, deleted_at__isnull=True, is_active=True
        ).exists()

    allowed_ids = {o.id for o in resolve_user_organizations(user)}
    return organization_id in allowed_ids


def assert_organization_access(user, organization_id: UUID) -> None:
    if not user_can_access_organization(user, organization_id):
        raise PermissionDenied("Accès refusé à cette organisation.")


def resolve_organization_id(request, *, require_explicit: bool = False) -> UUID:
    """
    Résout l'organisation active et vérifie les droits.
    require_explicit=True pour sync push/pull (header obligatoire sauf défaut explicite).
    """
    user = getattr(request, "user", None)
    raw = None
    for source in (
        request.headers.get("X-Organization-Id"),
        getattr(request, "query_params", {}).get("organizationId"),
        getattr(request, "data", {}).get("organizationId") if hasattr(request, "data") else None,
    ):
        parsed = parse_organization_id(source)
        if parsed:
            raw = parsed
            break

    if raw is None:
        if require_explicit and user and user_is_tenant_super_admin(user):
            raise PermissionDenied(
                "Header X-Organization-Id requis pour cette opération."
            )
        if user and getattr(user, "is_authenticated", False):
            org_id = default_organization_for_user(user).id
        else:
            org_id = get_default_organization().id
    else:
        org_id = raw

    if user and getattr(user, "is_authenticated", False):
        assert_organization_access(user, org_id)

    if not Organization.objects.filter(id=org_id, deleted_at__isnull=True, is_active=True).exists():
        raise PermissionDenied("Organisation introuvable ou inactive.")

    return org_id


def get_organization_or_default(organization_id: UUID | None) -> Organization:
    if organization_id:
        org = Organization.objects.filter(
            id=organization_id, deleted_at__isnull=True, is_active=True
        ).first()
        if org:
            return org
    return get_default_organization()


def scope_sync_store(qs, organization_id: UUID | None):
    if organization_id is None:
        return qs
    if organization_id == DEFAULT_ORG_ID:
        return qs.filter(
            models.Q(organization_id=organization_id) | models.Q(organization_id__isnull=True)
        )
    return qs.filter(organization_id=organization_id)


def scope_sync_events(qs, organization_id: UUID | None):
    if organization_id is None:
        return qs
    return qs.filter(organization_id=organization_id)


def normalize_slug(slug: str) -> str:
    slug = (slug or "").strip().lower()
    if not slug or not SLUG_RE.match(slug):
        raise ValueError("Slug invalide (lettres minuscules, chiffres et tirets).")
    return slug


def organization_to_dict(org: Organization) -> dict:
    return {
        "id": str(org.id),
        "name": org.name,
        "slug": org.slug,
        "databaseName": org.database_name,
        "city": org.city,
        "description": org.description,
        "isActive": org.is_active,
        "createdAt": org.created_at.isoformat() if org.created_at else None,
        "updatedAt": org.updated_at.isoformat() if org.updated_at else None,
    }


def resolve_user_organizations(user) -> list[Organization]:
    """
    Organisations visibles pour l'utilisateur.
    Jessica (super admin) : toutes. Autres : uniquement le(s) tenant(s) sync où ils existent.
    """
    from api.models import SyncedEntityStore

    if not user or not getattr(user, "is_authenticated", False):
        return []

    if user_is_tenant_super_admin(user):
        return list(
            Organization.objects.filter(deleted_at__isnull=True, is_active=True).order_by("name")
        )

    org_ids = (
        SyncedEntityStore.objects.filter(
            entity_type="Users",
            id=user.id,
            deleted_at__isnull=True,
        )
        .values_list("organization_id", flat=True)
        .distinct()
    )

    orgs = list(
        Organization.objects.filter(
            id__in=org_ids, deleted_at__isnull=True, is_active=True
        ).order_by("name")
    )
    if orgs:
        return orgs

    owner_orgs = list(
        Organization.objects.filter(
            created_by_username__iexact=getattr(user, "username", "") or "",
            deleted_at__isnull=True,
            is_active=True,
        ).order_by("name")
    )
    return owner_orgs


def default_organization_for_user(user) -> Organization:
    orgs = resolve_user_organizations(user)
    if not orgs:
        raise PermissionDenied(
            "Aucune organisation n'est associée à ce compte. "
            "Synchronisez l'utilisateur depuis le desktop ou contactez Jessica."
        )
    return orgs[0]
