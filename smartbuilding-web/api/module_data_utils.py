from __future__ import annotations

from typing import Any, Callable

from api.models import SyncedEntityStore


def money(value) -> float:
    return float(value or 0)


def iso(value):
    if value is None or value == "":
        return None
    if hasattr(value, "isoformat"):
        return value.isoformat()
    return str(value)


def module_payload(
    title,
    rows,
    kpis=None,
    actions=None,
    *,
    pending_validation=None,
    sections=None,
):
    body = {
        "title": title,
        "kpis": kpis or [],
        "rows": rows,
        "actions": actions or [],
    }
    if pending_validation is not None:
        body["pendingValidation"] = pending_validation
    if sections is not None:
        body["sections"] = sections
    return body


def pick_sync_value(data: dict[str, Any], *keys: str, default: Any = "—") -> Any:
    for key in keys:
        if key in data and data[key] not in (None, ""):
            return data[key]
    return default


def rows_from_sync_store(
    entity_types: list[str],
    mapper: Callable[[dict[str, Any]], dict[str, Any] | None],
    limit: int = 300,
    organization_id=None,
) -> list[dict[str, Any]]:
    from api.organization_context import get_request_organization_id, scope_sync_store

    if organization_id is None:
        organization_id = get_request_organization_id()

    rows = []
    qs = SyncedEntityStore.objects.filter(
        entity_type__in=entity_types, deleted_at__isnull=True
    )
    stores = scope_sync_store(qs, organization_id).order_by("-updated_at")[:limit]
    for s in stores:
        payload = s.json_data if isinstance(s.json_data, dict) else {}
        mapped = mapper(payload)
        if mapped:
            rows.append(mapped)
    return rows
