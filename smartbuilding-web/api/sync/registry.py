import json
from datetime import datetime
from typing import Any, Callable

from django.db import transaction
from django.utils import timezone

from api.models import (
    Building,
    ConsumptionRecord,
    Employee,
    Equipment,
    FinancialTransaction,
    Incident,
    InventoryItem,
    LeaseContract,
    Premise,
    RentPayment,
    Supplier,
    SyncedEntityStore,
    Tenant,
    User,
    Visitor,
)
from api.sync.utils import parse_datetime, parse_uuid

SYNC_ENTITY_TYPES = [
    "Users",
    "Employees",
    "Attendances",
    "SalaryPayments",
    "DisciplinaryNotes",
    "Buildings",
    "RentPayments",
    "TenantActivities",
    "LeaseGuarantees",
    "Equipment",
    "Premises",
    "Tenants",
    "LeaseContracts",
    "FinancialTransactions",
    "Suppliers",
    "Incidents",
    "ConsumptionRecords",
    "Visitors",
    "VisitorAppointments",
    "InventoryItems",
]

_HANDLERS: dict[str, Callable[[dict], None]] = {}


def register(entity_type: str):
    def decorator(fn):
        _HANDLERS[entity_type] = fn
        return fn

    return decorator


def register_all():
    """Enregistre les matérialiseurs métier (import side-effect)."""
    from api.sync import materializers  # noqa: F401

    materializers.register_handlers()


def is_syncable(entity_type: str) -> bool:
    return entity_type in SYNC_ENTITY_TYPES


@transaction.atomic
def apply_push(entity_type: str, entities: list[dict]) -> int:
    if not is_syncable(entity_type):
        return 0

    applied = 0
    for payload in entities:
        entity_id = parse_uuid(payload.get("id") or payload.get("Id"))
        if not entity_id:
            continue

        updated_at = parse_datetime(
            payload.get("updatedAt") or payload.get("UpdatedAt")
        ) or timezone.now()
        deleted_at = parse_datetime(
            payload.get("deletedAt") or payload.get("DeletedAt")
        )
        json_raw = payload.get("jsonData") or payload.get("JsonData") or "{}"
        if isinstance(json_raw, str):
            try:
                data = json.loads(json_raw)
            except json.JSONDecodeError:
                continue
        else:
            data = json_raw

        try:
            store = SyncedEntityStore.objects.get(id=entity_id)
            if store.updated_at > updated_at:
                continue
        except SyncedEntityStore.DoesNotExist:
            store = SyncedEntityStore(id=entity_id, entity_type=entity_type)

        store.entity_type = entity_type
        store.json_data = data
        store.updated_at = updated_at
        store.deleted_at = deleted_at
        if not store.created_at:
            store.created_at = timezone.now()
        store.save()

        handler = _HANDLERS.get(entity_type)
        if handler:
            handler(data)

        applied += 1

    return applied


def get_changes_since(
    entity_type: str, since: datetime, limit: int = 200
) -> list[dict[str, Any]]:
    if not is_syncable(entity_type):
        return []

    qs = (
        SyncedEntityStore.objects.filter(
            entity_type=entity_type, updated_at__gt=since
        )
        .order_by("updated_at")[:limit]
    )

    return [
        {
            "id": row.id,
            "updatedAt": row.updated_at.isoformat().replace("+00:00", "Z"),
            "deletedAt": (
                row.deleted_at.isoformat().replace("+00:00", "Z")
                if row.deleted_at
                else None
            ),
            "jsonData": json.dumps(row.json_data, ensure_ascii=False),
        }
        for row in qs
    ]
