from datetime import datetime
from typing import Any


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


def parse_date(value):
    dt = parse_datetime(value)
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


def map_base_fields(instance, data: dict[str, Any]):
    """Champs communs BaseEntity (PascalCase ou camelCase)."""
    if uid := pick(data, "Id", "id"):
        instance.id = parse_uuid(uid)
    if created := pick(data, "CreatedAt", "createdAt"):
        instance.created_at = parse_datetime(created) or instance.created_at
    if updated := pick(data, "UpdatedAt", "updatedAt"):
        instance.updated_at = parse_datetime(updated) or instance.updated_at
    deleted = pick(data, "DeletedAt", "deletedAt")
    instance.deleted_at = parse_datetime(deleted)
    instance.is_synced = True
