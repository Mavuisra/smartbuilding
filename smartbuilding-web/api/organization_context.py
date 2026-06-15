"""Contexte organisation (multi-tenant) pour le portail web et la sync cloud."""

from __future__ import annotations

import uuid
from uuid import UUID

from django.utils import timezone

from api.models import Organization

DEFAULT_ORG_SLUG = "organisation-principale"


def get_default_organization() -> Organization:
    org, _ = Organization.objects.get_or_create(
        slug=DEFAULT_ORG_SLUG,
        defaults={
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


def resolve_organization_id(request) -> UUID:
    """Organisation active pour la requête (header, query ou défaut)."""
    for source in (
        request.headers.get("X-Organization-Id"),
        getattr(request, "query_params", {}).get("organizationId"),
        getattr(request, "data", {}).get("organizationId") if hasattr(request, "data") else None,
    ):
        parsed = parse_organization_id(source)
        if parsed:
            return parsed
    return get_default_organization().id


def get_organization_or_default(organization_id: UUID | None) -> Organization:
    if organization_id:
        org = Organization.objects.filter(id=organization_id, deleted_at__isnull=True).first()
        if org:
            return org
    return get_default_organization()


def scope_sync_store(qs, organization_id: UUID | None):
    if organization_id is None:
        return qs
    return qs.filter(organization_id=organization_id)


def user_can_list_all_organizations(user) -> bool:
    if not user or not getattr(user, "is_authenticated", False):
        return False
    if getattr(user, "is_superuser", False):
        return True
    role = getattr(user, "role", "") or ""
    return role in ("Administrateur", "PDG", "ADMIN")


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
