"""
Handlers de données par module — parité lecture avec le desktop (données sync + ORM).
"""

from __future__ import annotations

from django.db.models import Sum
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
    ServerSyncEvent,
    Supplier,
    SyncedEntityStore,
    Tenant,
    User,
    Visitor,
)
from api.services.dashboard import get_executive_summary, get_sync_health
from api.services.finance_ledger import (
    dedupe_sync_financial_rows,
    queryset_to_deduped_list,
)
from api.services.sync_metrics import filter_to_synced, sync_store_count
from api.sync.materializers import repair_employees_from_sync_store
from api.module_data_utils import iso, module_payload, money, pick_sync_value, rows_from_sync_store

_iso = iso
_money = money
_module_payload = module_payload
_pick_sync_value = pick_sync_value
_rows_from_sync_store = rows_from_sync_store


def get_module_handler(slug: str):
    from executive.module_registry import resolve_slug

    slug = resolve_slug(slug.replace("_", "-"))
    handlers = {
        "dashboard": None,
        "personnel": personnel,
        "presence": personnel,
        "locations": locations_list,
        "locations-list": locations_list,
        "locations-create": locations_contracts,
        "locations-rent-pay": locations_rent_payments,
        "locations-tenants": locations_tenants,
        "locations-landlord": locations_landlord,
        "locations-building": locations_buildings,
        "locations-apartments": locations_apartments,
        "locations-gestion": locations_gestion,
        "contrats": locations_contracts,
        "finances": finances,
        "finance": finances,
        "documents": documents,
        "technique": technique,
        "maintenance": technique,
        "incidents": incidents,
        "fournisseurs": fournisseurs,
        "consommations": consommations,
        "visites": visites,
        "emails": emails,
        "supervision": supervision,
        "validations": validations,
        "journal": activities,
        "activites-logs": activities,
        "utilisateurs": users,
        "rapports": reports,
        "synchronisation": sync_module,
        "parametres": settings_module,
        "audit-securite": audit,
    }
    return handlers.get(slug)


def personnel():
    repair_employees_from_sync_store()
    rows = [
        {
            "Matricule": (e.employee_number or "").strip() or "—",
            "Nom": (e.full_name or "").strip() or "—",
            "Poste": e.position or "—",
            "Département": e.department or "—",
            "Téléphone": e.phone or "—",
            "Statut": "Actif" if e.is_active else "Inactif",
        }
        for e in Employee.objects.filter(deleted_at__isnull=True).order_by("full_name")[:300]
    ]
    return _module_payload(
        "Personnel",
        rows,
        [
            {"label": "Employés", "value": Employee.objects.filter(deleted_at__isnull=True).count()},
            {"label": "Actifs", "value": Employee.objects.filter(deleted_at__isnull=True, is_active=True).count()},
        ],
    )


def locations_list():
    rows = [
        {
            "Code": p.code or "—",
            "Local": p.name,
            "Bâtiment": p.building_name or "—",
            "Étage": p.floor or "—",
            "Loyer": _money(p.monthly_rent),
            "Statut": "Occupé" if p.is_occupied else "Libre",
        }
        for p in filter_to_synced(
            Premise.objects.filter(deleted_at__isnull=True), "Premises"
        ).order_by("code")[:300]
    ]
    if not rows:
        rows = _rows_from_sync_store(
            ["Premises"],
            lambda d: {
                "Code": _pick_sync_value(d, "Code", "code"),
                "Local": _pick_sync_value(d, "Name", "name"),
                "Bâtiment": _pick_sync_value(d, "Building", "building", "BuildingName", "buildingName"),
                "Étage": _pick_sync_value(d, "Floor", "floor"),
                "Loyer": _money(_pick_sync_value(d, "MonthlyRent", "monthlyRent", default=0)),
                "Statut": "Occupé"
                if bool(_pick_sync_value(d, "IsOccupied", "isOccupied", default=False))
                else "Libre",
            },
        )
    total = filter_to_synced(
        Premise.objects.filter(deleted_at__isnull=True), "Premises"
    ).count()
    occupied = filter_to_synced(
        Premise.objects.filter(deleted_at__isnull=True), "Premises"
    ).filter(is_occupied=True).count()
    if total == 0 and rows:
        total = len(rows)
        occupied = sum(1 for r in rows if r.get("Statut") == "Occupé")
    return _module_payload(
        "Locaux",
        rows,
        [
            {"label": "Locaux", "value": total},
            {"label": "Occupés", "value": occupied},
            {"label": "Libres", "value": max(total - occupied, 0)},
        ],
    )


def locations_contracts():
    rows = [
        {
            "Référence": c.contract_number or f"CT-{str(c.id)[:8]}",
            "Locataire": c.tenant.name if c.tenant_id else "—",
            "Local": c.premise.name if c.premise_id else "—",
            "Début": _iso(c.start_date),
            "Fin": _iso(c.end_date),
            "Loyer mensuel": _money(c.monthly_rent),
            "Statut": c.status or "—",
        }
        for c in filter_to_synced(
            LeaseContract.objects.filter(deleted_at__isnull=True), "LeaseContracts"
        ).order_by("-updated_at")[:300]
    ]
    if not rows:
        rows = _rows_from_sync_store(
            ["LeaseContracts"],
            lambda d: {
                "Référence": _pick_sync_value(d, "ContractNumber", "contractNumber"),
                "Locataire": _pick_sync_value(d, "TenantName", "tenantName"),
                "Local": _pick_sync_value(d, "PremiseName", "premiseName"),
                "Début": _pick_sync_value(d, "StartDate", "startDate"),
                "Fin": _pick_sync_value(d, "EndDate", "endDate"),
                "Loyer mensuel": _money(_pick_sync_value(d, "MonthlyRent", "monthlyRent", default=0)),
                "Statut": _pick_sync_value(d, "Status", "status"),
            },
        )
    return _module_payload(
        "Contrats",
        rows,
        [
            {
                "label": "Contrats",
                "value": filter_to_synced(
                    LeaseContract.objects.filter(deleted_at__isnull=True), "LeaseContracts"
                ).count(),
            },
            {
                "label": "Actifs",
                "value": filter_to_synced(
                    LeaseContract.objects.filter(deleted_at__isnull=True), "LeaseContracts"
                )
                .filter(status__icontains="actif")
                .count(),
            },
        ],
    )


def locations_rent_payments():
    rows = [
        {
            "Période": f"{p.month:02d}/{p.year}",
            "Montant dû": _money(p.amount_due),
            "Payé": _money(p.amount_paid),
            "Échéance": _iso(p.due_date),
            "Date paiement": _iso(p.paid_date),
            "Statut": p.payment_status or "—",
            "Retard": "Oui" if p.is_late else "Non",
        }
        for p in filter_to_synced(
            RentPayment.objects.filter(deleted_at__isnull=True), "RentPayments"
        ).order_by("-year", "-month")[:300]
    ]
    if not rows:
        rows = _rows_from_sync_store(
            ["RentPayments"],
            lambda d: {
                "Période": f"{_pick_sync_value(d, 'Month', 'month', default='—')}/{_pick_sync_value(d, 'Year', 'year', default='—')}",
                "Montant dû": _money(_pick_sync_value(d, "AmountDue", "amountDue", default=0)),
                "Payé": _money(_pick_sync_value(d, "AmountPaid", "amountPaid", default=0)),
                "Échéance": _pick_sync_value(d, "DueDate", "dueDate"),
                "Date paiement": _pick_sync_value(d, "PaidDate", "paidDate"),
                "Statut": _pick_sync_value(d, "PaymentStatus", "paymentStatus"),
                "Retard": "Oui" if _pick_sync_value(d, "IsLate", "isLate", default=False) else "Non",
            },
        )
    late = filter_to_synced(
        RentPayment.objects.filter(deleted_at__isnull=True), "RentPayments"
    ).filter(is_late=True).count()
    return _module_payload(
        "Paiements loyer",
        rows,
        [
            {
                "label": "Paiements",
                "value": filter_to_synced(
                    RentPayment.objects.filter(deleted_at__isnull=True), "RentPayments"
                ).count(),
            },
            {"label": "En retard", "value": late},
        ],
    )


def locations_tenants():
    rows = [
        {
            "Dossier": t.dossier_number or "—",
            "Nom": t.name,
            "Email": t.email or "—",
            "Téléphone": t.phone or "—",
            "Entreprise": t.company or "—",
            "Statut": t.rental_status or "—",
        }
        for t in Tenant.objects.filter(deleted_at__isnull=True).order_by("name")[:300]
    ]
    if not rows:
        rows = _rows_from_sync_store(
            ["Tenants"],
            lambda d: {
                "Dossier": _pick_sync_value(d, "DossierNumber", "dossierNumber"),
                "Nom": _pick_sync_value(d, "Name", "name"),
                "Email": _pick_sync_value(d, "Email", "email"),
                "Téléphone": _pick_sync_value(d, "Phone", "phone"),
                "Entreprise": _pick_sync_value(d, "Company", "company"),
                "Statut": _pick_sync_value(d, "RentalStatus", "rentalStatus"),
            },
        )
    return _module_payload(
        "Locataires",
        rows,
        [{"label": "Locataires", "value": Tenant.objects.filter(deleted_at__isnull=True).count()}],
    )


def locations_landlord():
    rows = [
        {"Bâtiment": b.name, "Adresse": b.address or "—", "Ville": b.city or "—", "Étages": b.floors}
        for b in Building.objects.filter(deleted_at__isnull=True).order_by("name")[:100]
    ]
    if not rows:
        rows = _rows_from_sync_store(
            ["BuildingInfos"],
            lambda d: {
                "Bâtiment": _pick_sync_value(
                    d, "BuildingDisplayName", "buildingDisplayName", "Name", "name"
                ),
                "Adresse": _pick_sync_value(d, "Address", "address"),
                "Ville": _pick_sync_value(d, "City", "city"),
                "Étages": _pick_sync_value(d, "TotalFloors", "totalFloors", default="—"),
            },
        )
    landlord_rows = _rows_from_sync_store(
        ["Landlords"],
        lambda d: {
            "Référence": _pick_sync_value(d, "ReferenceNumber", "referenceNumber"),
            "Nom": _pick_sync_value(d, "Name", "name"),
            "Type": _pick_sync_value(d, "LandlordType", "landlordType"),
            "Téléphone": _pick_sync_value(d, "Phone", "phone"),
            "Email": _pick_sync_value(d, "Email", "email"),
            "Statut": _pick_sync_value(d, "Status", "status"),
        },
    )
    title = "Bailleur / Patrimoine"
    if landlord_rows and not rows:
        return _module_payload(
            title,
            landlord_rows,
            [{"label": "Bailleurs", "value": len(landlord_rows)}],
        )
    if landlord_rows:
        rows = rows + landlord_rows
    return _module_payload(
        title,
        rows,
        [
            {"label": "Bâtiments", "value": Building.objects.filter(deleted_at__isnull=True).count() or len(rows)},
            {"label": "Bailleurs", "value": len(landlord_rows)},
        ],
    )


def locations_buildings():
    return locations_landlord()


def locations_apartments():
    rows = [
        {
            "Code": p.code,
            "Appartement": p.name,
            "Bâtiment": p.building_name,
            "Type": p.premise_type or "—",
            "Surface m²": p.area_sq_m,
            "Loyer": _money(p.monthly_rent),
        }
        for p in filter_to_synced(
            Premise.objects.filter(deleted_at__isnull=True), "Premises"
        ).order_by("building_name", "code")[:300]
    ]
    if not rows:
        rows = _rows_from_sync_store(
            ["PropertyApartments", "Premises"],
            lambda d: {
                "Code": _pick_sync_value(d, "Code", "code", "Number", "number"),
                "Appartement": _pick_sync_value(d, "Name", "name", "Label", "label"),
                "Bâtiment": _pick_sync_value(d, "BuildingName", "buildingName", "Building", "building"),
                "Type": _pick_sync_value(d, "PremiseType", "premiseType", "Type", "type"),
                "Surface m²": _pick_sync_value(d, "AreaSqM", "areaSqM", default="—"),
                "Loyer": _money(_pick_sync_value(d, "MonthlyRent", "monthlyRent", default=0)),
            },
        )
    return _module_payload("Appartements", rows, [{"label": "Unités", "value": len(rows)}])


def locations_gestion():
    summary = get_executive_summary()
    rows = [
        {"Indicateur": "Taux occupation", "Valeur": f"{summary['occupancyRate']} %"},
        {"Indicateur": "Contrats actifs", "Valeur": summary.get("activeLeases", 0)},
        {"Indicateur": "Loyers encaissés (mois)", "Valeur": _money(summary.get("rentCollected", 0))},
        {"Indicateur": "Retards", "Valeur": summary.get("latePayments", 0)},
    ]
    return _module_payload("Gestion patrimoine", rows, summary.get("quickStats", []))


def _finance_rows_from_sync_store():
    def tx_type_label(raw) -> str:
        if isinstance(raw, int):
            return "Dépense" if raw == FinancialTransaction.TxType.DEPENSE else "Recette"
        if isinstance(raw, str) and raw.lower() in ("depense", "dépense", "2"):
            return "Dépense"
        return "Recette"

    def map_row(d: dict) -> dict:
        return {
            "Date": _pick_sync_value(d, "TransactionDate", "transactionDate"),
            "Type": tx_type_label(_pick_sync_value(d, "Type", "type", default=1)),
            "Catégorie": _pick_sync_value(d, "Category", "category"),
            "Description": _pick_sync_value(d, "Description", "description"),
            "Montant": _money(_pick_sync_value(d, "Amount", "amount", default=0)),
            "Statut": _pick_sync_value(d, "Status", "status"),
            "Référence": _pick_sync_value(d, "Reference", "reference", default="—"),
        }

    rows = dedupe_sync_financial_rows(map_row, limit=300)
    for row in rows:
        row.setdefault("Référence", "—")
    return rows


def _finance_totals_from_sync_store() -> tuple[float, float]:
    income = 0.0
    expenses = 0.0
    for row in SyncedEntityStore.objects.filter(
        entity_type="FinancialTransactions", deleted_at__isnull=True
    ).iterator():
        payload = row.json_data if isinstance(row.json_data, dict) else {}
        amount = float(_pick_sync_value(payload, "Amount", "amount", default=0) or 0)
        raw = _pick_sync_value(payload, "Type", "type", default=1)
        is_expense = raw in (2, "2", "Depense", "Dépense", "depense") or (
            isinstance(raw, str) and "dep" in str(raw).lower()
        )
        if is_expense:
            expenses += amount
        else:
            income += amount
    return income, expenses


def finances():
    from api.services.finance_pdg import (
        collect_pending_expenses,
        ledger_income_expense_totals,
        pending_validation_summary,
    )

    pending = collect_pending_expenses()
    pending_summary = pending_validation_summary(pending)
    pending_rows = [e.to_validation_dict() for e in pending]

    base_qs = FinancialTransaction.objects.filter(deleted_at__isnull=True)
    if sync_store_count("FinancialTransactions") > 0:
        txs = queryset_to_deduped_list(
            filter_to_synced(base_qs, "FinancialTransactions")
        )
    else:
        txs = queryset_to_deduped_list(base_qs)

    ledger_rows = [
        {
            "Date": _iso(t.transaction_date),
            "Type": "Dépense" if t.type == FinancialTransaction.TxType.DEPENSE else "Recette",
            "Catégorie": t.category or "—",
            "Description": t.description or "—",
            "Montant": _money(t.amount),
            "Statut": t.status or "—",
            "Référence": t.reference or "—",
        }
        for t in txs[:300]
    ]
    if not ledger_rows:
        ledger_rows = _finance_rows_from_sync_store()

    income, expenses = ledger_income_expense_totals()
    if not income and not expenses:
        inc_f, exp_f = _finance_totals_from_sync_store()
        income, expenses = inc_f, exp_f

    kpis = [
        {"label": "Recettes", "value": _money(income)},
        {"label": "Dépenses", "value": _money(expenses)},
        {"label": "Solde", "value": _money(income - expenses)},
        {
            "label": "À valider (PDG)",
            "value": pending_summary["count"],
            "hint": pending_summary["totalAmountLabel"],
            "variant": "warning" if pending_summary["count"] else "ok",
        },
    ]

    return _module_payload(
        "Finances",
        ledger_rows,
        kpis,
        actions=["approve-expense", "reject-expense"] if pending_rows else [],
        pending_validation={
            "title": "Dépenses en attente de validation",
            "subtitle": (
                "Décisions requises avant paiement — aligné sur le workflow comptable du Desktop."
            ),
            "summary": pending_summary,
            "rows": pending_rows,
        },
        sections=[
            {
                "id": "pending",
                "title": "Validation PDG",
                "emptyMessage": "Aucune dépense en attente — tout est à jour.",
            },
            {
                "id": "ledger",
                "title": "Journal des mouvements",
                "emptyMessage": "Aucun mouvement synchronisé.",
            },
        ],
    )


def documents():
    from api.services.web_desktop_modules import load_documents_page

    return load_documents_page()


def technique():
    rows = [
        {
            "Équipement": e.name,
            "Catégorie": e.category or "—",
            "Statut": e.status or "—",
            "Localisation": e.location or "—",
            "Dernière maj": _iso(e.updated_at),
        }
        for e in Equipment.objects.filter(deleted_at__isnull=True).order_by("name")[:300]
    ]
    if not rows:
        rows = _rows_from_sync_store(
            ["Equipment"],
            lambda d: {
                "Équipement": _pick_sync_value(d, "Name", "name"),
                "Catégorie": _pick_sync_value(d, "Category", "category"),
                "Statut": _pick_sync_value(d, "Status", "status"),
                "Localisation": _pick_sync_value(d, "Location", "location"),
                "Dernière maj": _pick_sync_value(d, "UpdatedAt", "updatedAt"),
            },
        )
    return _module_payload(
        "Technique & Sécurité",
        rows,
        [{"label": "Équipements", "value": Equipment.objects.filter(deleted_at__isnull=True).count()}],
    )


def incidents():
    rows = [
        {
            "Code": i.code or "—",
            "Titre": i.title,
            "Sévérité": i.severity,
            "Statut": i.status,
            "Lieu": i.location or "—",
            "Coût": _money(i.cost),
        }
        for i in Incident.objects.filter(deleted_at__isnull=True).order_by("-reported_at")[:300]
    ]
    if not rows:
        rows = _rows_from_sync_store(
            ["Incidents"],
            lambda d: {
                "Code": _pick_sync_value(d, "Code", "code"),
                "Titre": _pick_sync_value(d, "Title", "title"),
                "Sévérité": _pick_sync_value(d, "Severity", "severity"),
                "Statut": _pick_sync_value(d, "Status", "status"),
                "Lieu": _pick_sync_value(d, "Location", "location"),
                "Coût": _money(_pick_sync_value(d, "Cost", "cost", default=0)),
            },
        )
    return _module_payload(
        "Incidents",
        rows,
        [{"label": "Incidents", "value": Incident.objects.filter(deleted_at__isnull=True).count()}],
    )


def fournisseurs():
    rows = [
        {
            "Nom": s.name,
            "Contact": s.contact_person or "—",
            "Email": s.email or "—",
            "Téléphone": s.phone or "—",
            "Catégorie": s.category or "—",
        }
        for s in Supplier.objects.filter(deleted_at__isnull=True).order_by("name")[:300]
    ]
    if not rows:
        rows = _rows_from_sync_store(
            ["Suppliers"],
            lambda d: {
                "Nom": _pick_sync_value(d, "Name", "name"),
                "Contact": _pick_sync_value(d, "ContactPerson", "contactPerson"),
                "Email": _pick_sync_value(d, "Email", "email"),
                "Téléphone": _pick_sync_value(d, "Phone", "phone"),
                "Catégorie": _pick_sync_value(d, "Category", "category"),
            },
        )
    return _module_payload(
        "Fournisseurs",
        rows,
        [{"label": "Fournisseurs", "value": Supplier.objects.filter(deleted_at__isnull=True).count()}],
    )


def consommations():
    rows = [
        {
            "Type": c.consumption_type or "—",
            "Début": _iso(c.period_start),
            "Fin": _iso(c.period_end),
            "Quantité": c.quantity,
            "Coût": _money(c.cost),
        }
        for c in ConsumptionRecord.objects.filter(deleted_at__isnull=True).order_by("-period_end")[:300]
    ]
    if not rows:
        rows = _rows_from_sync_store(
            ["ConsumptionRecords"],
            lambda d: {
                "Type": _pick_sync_value(d, "ConsumptionType", "consumptionType", "Type", "type"),
                "Début": _pick_sync_value(d, "PeriodStart", "periodStart"),
                "Fin": _pick_sync_value(d, "PeriodEnd", "periodEnd"),
                "Quantité": _pick_sync_value(d, "Quantity", "quantity", default=0),
                "Coût": _money(_pick_sync_value(d, "Cost", "cost", default=0)),
            },
        )
    total_cost = ConsumptionRecord.objects.filter(deleted_at__isnull=True).aggregate(t=Sum("cost"))["t"] or 0
    return _module_payload(
        "Consommations",
        rows,
        [
            {"label": "Relevés", "value": ConsumptionRecord.objects.filter(deleted_at__isnull=True).count()},
            {"label": "Coût total", "value": _money(total_cost)},
        ],
    )


def visites():
    rows = [
        {
            "Visiteur": v.full_name,
            "Société": v.company or "—",
            "Motif": v.purpose or "—",
            "Entrée": _iso(v.check_in_at),
            "Sortie": _iso(v.check_out_at),
        }
        for v in Visitor.objects.filter(deleted_at__isnull=True).order_by("-check_in_at")[:300]
    ]
    if not rows:
        rows = _rows_from_sync_store(
            ["Visitors", "VisitorAppointments"],
            lambda d: {
                "Visiteur": _pick_sync_value(d, "FullName", "fullName", "Name", "name"),
                "Société": _pick_sync_value(d, "Company", "company"),
                "Motif": _pick_sync_value(d, "Purpose", "purpose"),
                "Entrée": _pick_sync_value(d, "CheckInAt", "checkInAt"),
                "Sortie": _pick_sync_value(d, "CheckOutAt", "checkOutAt"),
            },
        )
    on_site = Visitor.objects.filter(deleted_at__isnull=True, check_out_at__isnull=True).count()
    return _module_payload(
        "Visites & Accès",
        rows,
        [
            {"label": "Visiteurs", "value": Visitor.objects.filter(deleted_at__isnull=True).count()},
            {"label": "Sur site", "value": on_site},
        ],
    )


def emails():
    rows = _rows_from_sync_store(
        ["CachedEmails", "EmailAccounts"],
        lambda d: {
            "De": _pick_sync_value(d, "From", "from", "FromAddress", "fromAddress"),
            "Objet": _pick_sync_value(d, "Subject", "subject"),
            "Date": _pick_sync_value(d, "ReceivedAt", "receivedAt", "Date", "date"),
            "Lu": "Oui" if _pick_sync_value(d, "IsRead", "isRead", default=False) else "Non",
        },
        limit=200,
    )
    return _module_payload(
        "Emails & Communication",
        rows,
        [{"label": "Messages sync", "value": len(rows)}],
    )


def supervision():
    summary = get_executive_summary()
    rows = [
        {"Indicateur": "Revenus loyers (total)", "Valeur": _money(summary.get("rentCollectedTotal", summary["monthlyRevenue"])), "Statut": "Suivi"},
        {"Indicateur": "Dépenses engagées (total)", "Valeur": _money(summary.get("totalExpenses", summary["monthlyExpenses"])), "Statut": "À contrôler"},
        {"Indicateur": "Occupation", "Valeur": f"{summary['occupancyRate']} %", "Statut": "Live"},
        {"Indicateur": "Incidents ouverts", "Valeur": summary["openIncidents"], "Statut": "Prioritaire"},
    ]
    return _module_payload("Supervision", rows, summary["quickStats"])


def validations():
    from api.services.finance_pdg import collect_pending_expenses, pending_validation_summary

    pending = collect_pending_expenses()
    summary = pending_validation_summary(pending)
    rows = [e.to_validation_dict() for e in pending]
    return _module_payload(
        "Validations",
        rows,
        [
            {"label": "Dépenses à valider", "value": summary["count"]},
            {"label": "Montant en attente", "value": summary["totalAmountLabel"]},
            {"label": "Validation PDG requise", "value": summary["pdgRequiredCount"]},
        ],
        actions=["approve-expense", "reject-expense"],
    )


def activities():
    from api.services.web_desktop_modules import load_activity_log

    return load_activity_log()


def users():
    from api.services.web_desktop_modules import load_users

    return load_users()


def reports(date_from=None, date_to=None):
    from api.services.web_desktop_modules import load_rapports

    return load_rapports(date_from=date_from, date_to=date_to)


def sync_module():
    from api.services.web_desktop_modules import load_sync_page

    return load_sync_page()


def settings_module():
    from api.services.web_desktop_modules import load_settings_page

    return load_settings_page()


def audit():
    rows = [
        {"Contrôle": "Authentification JWT", "Statut": "Active", "Détail": "API protégée"},
        {"Contrôle": "Rôles desktop", "Statut": "Alignés", "Détail": "PermissionCodes partagés"},
        {"Contrôle": "Journal sync", "Statut": "Actif", "Détail": f"{ServerSyncEvent.objects.count()} événements"},
    ]
    return _module_payload("Audit & Sécurité", rows)
