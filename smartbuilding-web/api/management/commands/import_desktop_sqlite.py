"""Importe les données SQLite du Desktop WPF vers la base Django (dev local, sans HTTP)."""

from __future__ import annotations

import json
import os
import sqlite3
from pathlib import Path

from django.core.management.base import BaseCommand
from django.utils import timezone

from api.sync.registry import SYNC_ENTITY_TYPES, apply_push, is_syncable

# Tables EF Core → types de sync SBMS (ordre = dépendances métier légères)
TABLE_TO_ENTITY = {
    "Users": "Users",
    "Employees": "Employees",
    "Attendances": "Attendances",
    "SalaryPayments": "SalaryPayments",
    "DisciplinaryNotes": "DisciplinaryNotes",
    "BuildingInfos": "BuildingInfos",
    "Buildings": "Buildings",
    "Landlords": "Landlords",
    "LandlordActivities": "LandlordActivities",
    "PropertyFloors": "PropertyFloors",
    "PropertyApartments": "PropertyApartments",
    "PropertyRooms": "PropertyRooms",
    "Premises": "Premises",
    "Tenants": "Tenants",
    "TenantDependents": "TenantDependents",
    "LeaseContracts": "LeaseContracts",
    "RentPayments": "RentPayments",
    "TenantActivities": "TenantActivities",
    "LeaseGuarantees": "LeaseGuarantees",
    "FinancialTransactions": "FinancialTransactions",
    "Suppliers": "Suppliers",
    "SupplierContracts": "SupplierContracts",
    "SupplierPayments": "SupplierPayments",
    "Equipment": "Equipment",
    "MaintenanceRecords": "MaintenanceRecords",
    "RepairRecords": "RepairRecords",
    "TechnicalAlerts": "TechnicalAlerts",
    "Incidents": "Incidents",
    "IncidentInterventions": "IncidentInterventions",
    "ConsumptionRecords": "ConsumptionRecords",
    "Visitors": "Visitors",
    "VisitorAppointments": "VisitorAppointments",
    "InventoryItems": "InventoryItems",
    "InventoryMaintenanceRecords": "InventoryMaintenanceRecords",
}


def default_desktop_db() -> Path:
    local = os.environ.get("LOCALAPPDATA", "")
    return Path(local) / "SBMS" / "data" / "smartbuilding.db"


def row_to_payload(row: sqlite3.Row) -> dict:
    data = {k: row[k] for k in row.keys()}
    entity_id = data.get("Id")
    updated = data.get("UpdatedAt") or data.get("updatedAt")
    deleted = data.get("DeletedAt") or data.get("deletedAt")
    if isinstance(updated, str):
        updated_at = updated
    elif updated is not None:
        updated_at = str(updated)
    else:
        updated_at = timezone.now().isoformat()
    deleted_at = deleted if deleted else None
    # Conserver Id dans json_data pour matérialisation + inject_entity_id en secours
    if entity_id is not None:
        data["Id"] = str(entity_id)
        data["id"] = str(entity_id)
    return {
        "id": str(entity_id) if entity_id else "",
        "updatedAt": updated_at,
        "deletedAt": deleted_at,
        "jsonData": json.dumps(data, default=str),
    }


class Command(BaseCommand):
    help = (
        "Lit la base SQLite du Desktop (%LOCALAPPDATA%\\SBMS\\data\\smartbuilding.db) "
        "et applique apply_push pour remplir le portail web sans passer par HTTP."
    )

    def add_arguments(self, parser):
        parser.add_argument(
            "--db-path",
            type=str,
            default="",
            help="Chemin vers smartbuilding.db (défaut: AppData SBMS)",
        )
        parser.add_argument(
            "--rebuild",
            action="store_true",
            help="Exécute rebuild_from_sync_store après import",
        )
        parser.add_argument(
            "--audit",
            action="store_true",
            help="Affiche le décompte SQLite vs types sync",
        )

    def handle(self, *args, **options):
        db_path = Path(options["db_path"] or default_desktop_db())
        if not db_path.is_file():
            self.stderr.write(self.style.ERROR(f"Base introuvable: {db_path}"))
            return

        self.stdout.write(f"Lecture {db_path} …")
        conn = sqlite3.connect(db_path)
        conn.row_factory = sqlite3.Row

        if options["audit"]:
            self._audit(conn)
            conn.close()
            return

        total = 0
        imported_types = 0

        for table, entity_type in TABLE_TO_ENTITY.items():
            if not is_syncable(entity_type):
                continue
            try:
                rows = conn.execute(f'SELECT * FROM "{table}"').fetchall()
            except sqlite3.OperationalError:
                continue
            if not rows:
                continue
            entities = [row_to_payload(r) for r in rows if r["Id"]]
            if not entities:
                continue
            try:
                applied = apply_push(entity_type, entities)
                total += applied
                imported_types += 1
                self.stdout.write(f"  {entity_type}: {applied} enregistrement(s)")
            except Exception as ex:
                self.stderr.write(self.style.WARNING(f"  {entity_type}: {ex}"))

        conn.close()
        self.stdout.write(
            self.style.SUCCESS(
                f"Import terminé — {total} enregistrement(s), {imported_types} type(s)."
            )
        )
        missing = set(SYNC_ENTITY_TYPES) - set(TABLE_TO_ENTITY.values())
        if missing:
            self.stdout.write(
                self.style.WARNING(f"Types sync sans table SQLite dédiée: {', '.join(sorted(missing))}")
            )

        if options["rebuild"]:
            from django.core.management import call_command

            call_command("rebuild_from_sync_store")
            self.stdout.write(self.style.SUCCESS("rebuild_from_sync_store terminé."))

    def _audit(self, conn: sqlite3.Connection) -> None:
        self.stdout.write(self.style.MIGRATE_HEADING("Audit SQLite Desktop"))
        for table, entity_type in TABLE_TO_ENTITY.items():
            try:
                n = conn.execute(f'SELECT COUNT(*) FROM "{table}"').fetchone()[0]
            except sqlite3.OperationalError:
                n = -1
            flag = ""
            if n > 0 and entity_type not in SYNC_ENTITY_TYPES:
                flag = " (non syncable)"
            elif n > 0:
                flag = " ✓"
            if n != 0:
                self.stdout.write(f"  {n:5} {table:30} → {entity_type}{flag}")
