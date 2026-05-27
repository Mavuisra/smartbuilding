from datetime import datetime, timezone as dt_timezone
from typing import Any


MIN_SYNC_DATETIME = datetime(1970, 1, 1, tzinfo=dt_timezone.utc)


def pick(data: dict, *keys, default=None):
    for key in keys:
        if key in data:
            return data[key]
    return default


def parse_uuid(value) -> str | None:
    if value is None:
        return None
    return str(value)


def parse_datetime(value) -> datetime | None:
    if value is None:
        return None
    if isinstance(value, datetime):
        return value
    text = str(value).replace("Z", "+00:00")
    try:
        return datetime.fromisoformat(text)
    except ValueError:
        return None


def normalize_sync_datetime(value, default=None) -> datetime | None:
    dt = parse_datetime(value)
    if dt is None:
        return default

    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=dt_timezone.utc)
    else:
        dt = dt.astimezone(dt_timezone.utc)

    if dt < MIN_SYNC_DATETIME:
        return default if default is not None else MIN_SYNC_DATETIME

    return dt


def parse_date(value):
    dt = normalize_sync_datetime(value)
    return dt.date() if dt else None


def parse_decimal(value, default=0):
    if value is None:
        return default
    try:
        return value
    except (TypeError, ValueError):
        return default


def parse_bool(value, default=False) -> bool:
    if isinstance(value, bool):
        return value
    if value is None:
        return default
    if isinstance(value, str):
        return value.lower() in ("true", "1", "yes")
    return bool(value)


def parse_int(value, default=0) -> int:
    try:
        return int(value)
    except (TypeError, ValueError):
        return default


def merge_sync_payload(existing: dict | None, incoming: dict) -> dict:
    """Fusionne un push sans écraser les champs déjà renseignés par des valeurs vides."""
    if not existing:
        return dict(incoming)

    merged = {**existing, **incoming}
    for key, value in incoming.items():
        if _is_empty_sync_value(value) and not _is_empty_sync_value(existing.get(key)):
            merged[key] = existing[key]
    return merged


def _is_empty_sync_value(value) -> bool:
    if value is None:
        return True
    if isinstance(value, str):
        return value.strip() == ""
    if isinstance(value, (list, tuple, dict)):
        return len(value) == 0
    return False


def map_base_fields(instance, data: dict[str, Any]):
    """Champs communs BaseEntity (PascalCase ou camelCase)."""
    if uid := pick(data, "Id", "id"):
        instance.id = parse_uuid(uid)
    if created := pick(data, "CreatedAt", "createdAt"):
        instance.created_at = normalize_sync_datetime(created, instance.created_at) or instance.created_at
    if updated := pick(data, "UpdatedAt", "updatedAt"):
        instance.updated_at = normalize_sync_datetime(updated, instance.updated_at) or instance.updated_at
    deleted = pick(data, "DeletedAt", "deletedAt")
    instance.deleted_at = normalize_sync_datetime(deleted)
    instance.is_synced = True
