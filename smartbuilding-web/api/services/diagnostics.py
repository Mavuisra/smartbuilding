"""Diagnostics tableau de bord — état BDD ORM vs magasin sync."""

from __future__ import annotations

from django.utils import timezone

from api.models import (
    Building,
    Employee,
    FinancialTransaction,
    LeaseContract,
    Premise,
    RentPayment,
    ServerSyncEvent,
    SyncedEntityStore,
    Supplier,
    Tenant,
)
from api.organization_context import scope_sync_events, scope_sync_store
from api.services.sync_metrics import filter_to_synced
from api.sync.registry import SYNC_ENTITY_TYPES


def _orm_counts(organization_id=None) -> dict:
    if organization_id is None:
        return {
            "premises": Premise.objects.filter(deleted_at__isnull=True).count(),
            "tenants": Tenant.objects.filter(deleted_at__isnull=True).count(),
            "leases": LeaseContract.objects.filter(deleted_at__isnull=True).count(),
            "rentPayments": RentPayment.objects.filter(deleted_at__isnull=True).count(),
            "employees": Employee.objects.filter(deleted_at__isnull=True).count(),
            "transactions": FinancialTransaction.objects.filter(deleted_at__isnull=True).count(),
            "buildings": Building.objects.filter(deleted_at__isnull=True).count(),
            "suppliers": Supplier.objects.filter(deleted_at__isnull=True).count(),
        }

    return {
        "premises": filter_to_synced(
            Premise.objects.filter(deleted_at__isnull=True), "Premises", organization_id
        ).count(),
        "tenants": filter_to_synced(
            Tenant.objects.filter(deleted_at__isnull=True), "Tenants", organization_id
        ).count(),
        "leases": filter_to_synced(
            LeaseContract.objects.filter(deleted_at__isnull=True), "LeaseContracts", organization_id
        ).count(),
        "rentPayments": filter_to_synced(
            RentPayment.objects.filter(deleted_at__isnull=True), "RentPayments", organization_id
        ).count(),
        "employees": filter_to_synced(
            Employee.objects.filter(deleted_at__isnull=True), "Employees", organization_id
        ).count(),
        "transactions": filter_to_synced(
            FinancialTransaction.objects.filter(deleted_at__isnull=True),
            "FinancialTransactions",
            organization_id,
        ).count(),
        "buildings": Building.objects.filter(deleted_at__isnull=True).count(),
        "suppliers": Supplier.objects.filter(deleted_at__isnull=True).count(),
    }


def get_data_pipeline_diagnostics(organization_id=None) -> dict:
    """Indique pourquoi le dashboard peut afficher des zéros."""
    orm = _orm_counts(organization_id)
    store_qs = scope_sync_store(
        SyncedEntityStore.objects.filter(deleted_at__isnull=True), organization_id
    )
    store_total = store_qs.count()
    store_by_type = {}
    for et in SYNC_ENTITY_TYPES:
        c = store_qs.filter(entity_type=et).count()
        if c:
            store_by_type[et] = c

    orm_by_type = {
        "RentPayments": orm["rentPayments"],
        "Premises": orm["premises"],
        "FinancialTransactions": orm["transactions"],
        "Tenants": orm["tenants"],
        "LeaseContracts": orm["leases"],
        "Employees": orm["employees"],
    }
    mismatches = []
    for et, orm_n in orm_by_type.items():
        store_n = store_by_type.get(et, 0)
        if store_n > 0 and orm_n < store_n:
            mismatches.append(
                {
                    "entityType": et,
                    "ormCount": orm_n,
                    "syncStoreCount": store_n,
                    "hint": (
                        "ORM incomplet — le dashboard utilise le magasin sync. "
                        "Lancez : python manage.py rebuild_from_sync_store "
                        f"--entity-type={et}"
                    ),
                }
            )

    event_qs = scope_sync_events(ServerSyncEvent.objects.all(), organization_id)
    last_event = event_qs.order_by("-created_at").first()
    orm_business = sum(orm.values()) - orm.get("employees", 0)
    has_orm = orm_business > 0
    has_store = store_total > 0
    business_types = (
        "RentPayments",
        "Premises",
        "LeaseContracts",
        "Tenants",
        "FinancialTransactions",
        "Buildings",
    )
    business_in_store = sum(store_by_type.get(t, 0) for t in business_types)

    if has_orm and business_in_store > 0:
        status = "ok"
        hint_fr = "Données métier présentes (ORM + sync Desktop)."
    elif has_orm:
        status = "ok"
        hint_fr = "Données métier présentes dans PostgreSQL."
    elif business_in_store > 0:
        status = "sync_store_only"
        hint_fr = (
            "Données reçues depuis le bureau local — affichage en cours. "
            "Cette page se met à jour automatiquement."
        )
    elif has_store:
        status = "sync_partial"
        hint_fr = (
            "Premières données reçues depuis le bureau local. "
            "Les modules Locations et Finances apparaîtront dès leur envoi — actualisation automatique."
        )
    else:
        status = "empty"
        hint_fr = (
            "En attente des données du bureau local (application desktop). "
            "Dès qu'un poste est connecté à Internet, les chiffres s'affichent ici sans action de votre part."
        )

    return {
        "status": status,
        "hint": hint_fr,
        "ormCounts": orm,
        "syncStoreTotal": store_total,
        "syncStoreByType": store_by_type,
        "mismatches": mismatches,
        "lastSyncEvent": (
            {
                "username": last_event.username,
                "entityType": last_event.entity_type,
                "direction": last_event.direction,
                "recordsCount": last_event.records_count,
                "success": last_event.success,
                "createdAt": last_event.created_at.isoformat(),
            }
            if last_event
            else None
        ),
        "serverTime": timezone.now().isoformat(),
    }
