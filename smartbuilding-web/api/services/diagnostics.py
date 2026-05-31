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
from api.sync.registry import SYNC_ENTITY_TYPES


def get_data_pipeline_diagnostics() -> dict:
    """Indique pourquoi le dashboard peut afficher des zéros."""
    orm = {
        "premises": Premise.objects.filter(deleted_at__isnull=True).count(),
        "tenants": Tenant.objects.filter(deleted_at__isnull=True).count(),
        "leases": LeaseContract.objects.filter(deleted_at__isnull=True).count(),
        "rentPayments": RentPayment.objects.filter(deleted_at__isnull=True).count(),
        "employees": Employee.objects.filter(deleted_at__isnull=True).count(),
        "transactions": FinancialTransaction.objects.filter(deleted_at__isnull=True).count(),
        "buildings": Building.objects.filter(deleted_at__isnull=True).count(),
        "suppliers": Supplier.objects.filter(deleted_at__isnull=True).count(),
    }
    store_qs = SyncedEntityStore.objects.filter(deleted_at__isnull=True)
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

    last_event = ServerSyncEvent.objects.order_by("-created_at").first()
    orm_business = sum(orm.values()) - orm.get("employees", 0)
    has_orm = orm_business > 0
    has_store = store_total > 0

    if has_orm:
        status = "ok"
        hint_fr = "Données métier présentes dans PostgreSQL/SQLite."
    elif has_store:
        status = "sync_store_only"
        hint_fr = (
            "Des données existent dans le magasin sync mais pas dans les tables métier. "
            "Exécutez : python manage.py rebuild_from_sync_store"
        )
    else:
        status = "empty"
        hint_fr = (
            "Aucune donnée sur ce serveur. Depuis le Desktop : Paramètres → Synchronisation "
            "(ou bouton sync), avec Api:BaseUrl pointant vers CE serveur. "
            "En local : appsettings.Development.json → http://127.0.0.1:8000/"
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
