import json
import logging
from datetime import datetime
from typing import Any, Callable

from django.db import transaction
from django.utils import timezone

logger = logging.getLogger(__name__)

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
from api.sync.utils import (
    MIN_SYNC_DATETIME,
    inject_entity_id,
    merge_sync_payload,
    normalize_sync_datetime,
    parse_uuid,
    pick,
)

SYNC_ENTITY_TYPES = [
    "Users",
    "Employees",
    "Attendances",
    "SalaryPayments",
    "DisciplinaryNotes",
    "Buildings",
    "BuildingInfos",
    "Landlords",
    "LandlordActivities",
    "PropertyFloors",
    "PropertyApartments",
    "PropertyRooms",
    "Premises",
    "Tenants",
    "TenantDependents",
    "LeaseContracts",
    "RentPayments",
    "TenantActivities",
    "LeaseGuarantees",
    "Equipment",
    "MaintenanceRecords",
    "RepairRecords",
    "TechnicalAlerts",
    "FinancialTransactions",
    "Suppliers",
    "SupplierContracts",
    "SupplierPayments",
    "Incidents",
    "IncidentInterventions",
    "ConsumptionRecords",
    "Visitors",
    "VisitorAppointments",
    "InventoryItems",
    "InventoryMaintenanceRecords",
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


def get_registered_handlers() -> dict[str, Callable[[dict], None]]:
    register_all()
    return dict(_HANDLERS)


def is_syncable(entity_type: str) -> bool:
    return entity_type in SYNC_ENTITY_TYPES


def _parse_push_json(payload: dict) -> dict | None:
    json_raw = payload.get("jsonData") or payload.get("JsonData") or "{}"
    if isinstance(json_raw, str):
        try:
            return json.loads(json_raw)
        except json.JSONDecodeError:
            return None
    if isinstance(json_raw, dict):
        return json_raw
    return None


def _preflight_rent_payment_parents(entities: list[dict]) -> None:
    """Matérialise locataires, locaux et baux référencés avant les paiements."""
    from api.sync.materializers import ensure_entity_materialized

    lease_ids: set[str] = set()
    for payload in entities:
        data = _parse_push_json(payload)
        if not data:
            continue
        lid = pick(data, "LeaseContractId", "leaseContractId")
        if lid:
            lease_ids.add(str(lid))

    for lid in lease_ids:
        if LeaseContract.objects.filter(id=lid).exists():
            continue
        ensure_entity_materialized("LeaseContracts", lid)
        if LeaseContract.objects.filter(id=lid).exists():
            lease = LeaseContract.objects.filter(id=lid).first()
            if lease and lease.premise_id_sync:
                ensure_entity_materialized("Premises", lease.premise_id_sync)
            if lease and lease.tenant_id_sync:
                ensure_entity_materialized("Tenants", lease.tenant_id_sync)
            ensure_entity_materialized("LeaseContracts", lid)


def _apply_single_push(entity_type: str, payload: dict) -> bool:
    """Applique un enregistrement (magasin sync + ORM). Retourne True si OK."""
    entity_id = parse_uuid(payload.get("id") or payload.get("Id"))
    if not entity_id:
        return False

    updated_at = normalize_sync_datetime(
        payload.get("updatedAt") or payload.get("UpdatedAt")
    ) or timezone.now()
    deleted_at = normalize_sync_datetime(
        payload.get("deletedAt") or payload.get("DeletedAt")
    )
    data = _parse_push_json(payload)
    if data is None:
        return False

    try:
        store = SyncedEntityStore.objects.get(id=entity_id)
        data = merge_sync_payload(store.json_data, data)
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
        handler(inject_entity_id(data, entity_id))

    return True


def apply_push(entity_type: str, entities: list[dict]) -> int:
    if not is_syncable(entity_type):
        return 0

    register_all()

    if entity_type == "RentPayments":
        _preflight_rent_payment_parents(entities)

    applied = 0
    for payload in entities:
        try:
            with transaction.atomic():
                if _apply_single_push(entity_type, payload):
                    applied += 1
        except Exception:
            logger.exception(
                "Push échoué pour %s/%s",
                entity_type,
                payload.get("id") or payload.get("Id"),
            )

    if applied > 0 and entity_type == "FinancialTransactions":
        from api.sync.maintenance import dedupe_financial_sync_and_orm

        dedupe_financial_sync_and_orm()

    return applied


def rematerialize_entity_type(entity_type: str) -> int:
    """Rejoue les handlers ORM pour toutes les lignes du magasin sync (type donné)."""
    register_all()
    handler = _HANDLERS.get(entity_type)
    if not handler:
        return 0
    done = 0
    for row in SyncedEntityStore.objects.filter(
        entity_type=entity_type, deleted_at__isnull=True
    ).iterator():
        payload = row.json_data if isinstance(row.json_data, dict) else {}
        if not payload:
            continue
        try:
            handler(inject_entity_id(payload, row.id))
            done += 1
        except Exception:
            logger.exception(
                "Rematérialisation échouée pour %s/%s", entity_type, row.id
            )
    return done


def get_changes_since(entity_type: str, since) -> list[dict[str, Any]]:
    if not is_syncable(entity_type):
        return []

    since = normalize_sync_datetime(since, MIN_SYNC_DATETIME) or MIN_SYNC_DATETIME

    rows = (
        SyncedEntityStore.objects.filter(
            entity_type=entity_type, updated_at__gt=since
        )
        .order_by("updated_at")[:500]
    )

    return [
        {
            "id": str(row.id),
            "updatedAt": row.updated_at.isoformat(),
            "deletedAt": row.deleted_at.isoformat() if row.deleted_at else None,
            "jsonData": json.dumps(row.json_data, ensure_ascii=False),
        }
        for row in rows
    ]
