"""
Données modules portail web — structure alignée sur les ViewModels desktop WPF.
"""

from __future__ import annotations

from datetime import date, datetime, timedelta
from decimal import Decimal

from django.db.models import Count, Sum
from django.utils import timezone

from api.models import (
    Building,
    ConsumptionRecord,
    Employee,
    FinancialTransaction,
    Incident,
    LeaseContract,
    Premise,
    RentPayment,
    ServerSyncEvent,
    SyncedDocument,
    SyncedEntityStore,
    Tenant,
    User,
    Visitor,
)
from api.permission_codes import ALL_PERMISSION_CODES, permissions_for_role
from api.services.dashboard import get_executive_overview, get_sync_health
from api.services.sync_metrics import (
    calendar_month_starts,
    expenses_month_totals,
    sync_store_count,
)
from api.services.database_reset import database_info
from api.services.sync_metrics import filter_to_synced, sync_store_count


def _iso(dt) -> str | None:
    if dt is None:
        return None
    if isinstance(dt, datetime):
        return dt.isoformat()
    return str(dt)


def _money(v) -> str:
    return f"$ {float(v or 0):,.2f}"


def _fmt_date(d) -> str:
    if not d:
        return "—"
    if isinstance(d, datetime):
        d = d.date()
    return d.strftime("%d/%m/%Y")


def _fmt_datetime(dt) -> str:
    if not dt:
        return "—"
    if isinstance(dt, str):
        from api.sync.utils import parse_datetime

        raw = dt.strip()
        if not raw:
            return "—"
        parsed = parse_datetime(raw)
        if parsed is None:
            return raw
        dt = parsed
    if isinstance(dt, date) and not isinstance(dt, datetime):
        return dt.strftime("%d/%m/%Y")
    if isinstance(dt, datetime):
        if timezone.is_aware(dt):
            dt = timezone.localtime(dt)
        return dt.strftime("%d/%m/%Y %H:%M:%S")
    return str(dt)


def _audit_row(obj) -> dict:
    """Métadonnées communes : ID, création, modification, sync, suppression."""
    return {
        "ID": str(getattr(obj, "id", "")),
        "Créé le": _fmt_datetime(getattr(obj, "created_at", None)),
        "Modifié le": _fmt_datetime(getattr(obj, "updated_at", None)),
        "Synchronisé": "Oui" if getattr(obj, "is_synced", True) else "Non",
        "Supprimé le": _fmt_datetime(getattr(obj, "deleted_at", None)),
    }


def _full_text(value) -> str:
    if value is None or value == "":
        return "—"
    return str(value)


def _trend(today: int, yesterday: int) -> str:
    if yesterday == 0:
        return f"+{today}" if today else "—"
    diff = today - yesterday
    sign = "+" if diff >= 0 else ""
    return f"{sign}{diff} vs hier"


def _initials(name: str) -> str:
    parts = (name or "").split()
    if not parts:
        return "?"
    if len(parts) == 1:
        return parts[0][:2].upper()
    return (parts[0][0] + parts[-1][0]).upper()


def load_dashboard_page(organization_id=None) -> dict:
    """Tableau de bord web — synthèse explicative avec graphiques (parité desktop PDG)."""
    overview = get_executive_overview(organization_id=organization_id)
    summary = overview["summary"]
    sync = overview["syncHealth"]
    diag = overview.get("diagnostics") or {}

    today = timezone.localdate()
    month_starts = calendar_month_starts(today, months=6)
    expenses_chart = [
        {"label": ms.strftime("%b %Y"), "value": float(expenses_month_totals(ms, organization_id=organization_id))}
        for ms in month_starts
    ]

    spark_labels = []
    spark_counts = _last7_sync_counts()
    for i in range(6, -1, -1):
        d = today - timedelta(days=i)
        spark_labels.append(d.strftime("%d/%m"))

    entity_types = [
        "Users", "Employees", "Premises", "Tenants", "LeaseContracts",
        "RentPayments", "FinancialTransactions", "Incidents", "Visitors",
    ]
    entity_rows = [
        {
            "Type entité": et,
            "Enregistrements sync": sync_store_count(et, organization_id=organization_id),
            "Source": "Magasin sync desktop → cloud Render",
        }
        for et in entity_types
    ]

    validation_rows = [
        {
            "Type": v.get("type", "—"),
            "Référence": v.get("reference", "—"),
            "Description": v.get("description", "—"),
            "Demandeur": v.get("requester", "—"),
            "Date demande": _fmt_datetime(v.get("requestDate")),
            "Montant": _money(v.get("amount")),
            "Statut": v.get("status", "—"),
        }
        for v in overview.get("pendingValidations") or []
    ]

    activity_rows = [
        {
            "Activité": a.get("text", "—"),
            "Catégorie": a.get("category", "—"),
            "Date/heure": _fmt_datetime(a.get("timestamp")),
        }
        for a in overview.get("recentActivities") or []
    ]

    notification_rows = [
        {
            "ID": str(n.get("id", "")),
            "Titre": n.get("title", "—"),
            "Message": n.get("message", "—"),
            "Sévérité": n.get("severity", "—"),
            "Source": n.get("source", "—"),
            "Date/heure": _fmt_datetime(n.get("timestamp")),
        }
        for n in overview.get("notifications") or []
    ]

    movement_rows = [
        {
            "Date/heure": _fmt_datetime(m.get("date")),
            "Sens": "Entrée" if m.get("type") == "IN" else "Sortie",
            "Catégorie": m.get("category", "—"),
            "Description": m.get("description", "—"),
            "Montant": _money(m.get("amount")),
            "Référence": m.get("reference", "—"),
        }
        for m in summary.get("recentMovements") or []
    ]

    tenants = list(Tenant.objects.filter(deleted_at__isnull=True).order_by("name")[:100])
    tenant_rows = [
        {
            "ID": str(t.id),
            "Nom": _full_text(t.name),
            "Email": _full_text(t.email),
            "Téléphone": _full_text(t.phone),
            "Société": _full_text(t.company),
            "N° dossier": _full_text(t.dossier_number),
            "Statut location": _full_text(t.rental_status),
            "Catégorie": _full_text(t.tenant_category),
            "Créé le": _fmt_datetime(t.created_at),
            "Modifié le": _fmt_datetime(t.updated_at),
        }
        for t in tenants
    ]

    treasury = float(summary.get("availableBalance") or summary.get("netBalance") or 0)
    explanations = [
        {
            "title": "Trésorerie disponible",
            "value": _money(treasury),
            "detail": "Loyers encaissés (cumul) moins dépenses engagées (cumul). "
            "Sources : RentPayments + FinancialTransactions synchronisés depuis le desktop.",
            "icon": "piggy-bank",
        },
        {
            "title": "Loyers encaissés",
            "value": _money(summary.get("rentCollectedTotal")),
            "detail": f"Ce mois : {_money(summary.get('rentCollected'))} sur "
            f"{_money(summary.get('rentPlanned'))} prévus. "
            "Données issues des quittances / RentPayments.",
            "icon": "cash-coin",
        },
        {
            "title": "Dépenses engagées",
            "value": _money(summary.get("totalExpenses")),
            "detail": f"Ce mois : {_money(summary.get('expensesThisMonth'))}. "
            "Écritures FinancialTransactions (type Dépense), dédupliquées.",
            "icon": "wallet2",
        },
        {
            "title": "Occupation des locaux",
            "value": f"{summary.get('occupiedPremises', 0)} / {summary.get('totalPremises', 0)}",
            "detail": f"Taux {summary.get('occupancyRate', 0)} % — calculé sur les Premises synchronisés.",
            "icon": "buildings",
        },
        {
            "title": "Contrats & locataires",
            "value": f"{summary.get('activeLeases', 0)} contrats · {summary.get('totalTenants', 0)} locataires",
            "detail": "LeaseContracts actifs et fiche Tenants. Voir module Rapports pour le détail.",
            "icon": "file-earmark-text",
        },
        {
            "title": "Synchronisation cloud",
            "value": f"{sync.get('successRate', 100)} % succès",
            "detail": f"{sync.get('recordsSynced', 0)} enregistrements sur {sync.get('totalEvents', 0)} "
            f"événements (fenêtre {sync.get('windowHours', 24)}h). "
            f"Dernière sync : {_fmt_datetime(sync.get('lastSyncAt'))}.",
            "icon": "cloud-arrow-up",
        },
    ]

    return {
        "layout": "desktop",
        "summary": summary,
        "syncHealth": sync,
        "diagnostics": diag,
        "presence": overview.get("presence") or {},
        "unreadNotifications": overview.get("unreadNotifications", 0),
        "revenueChart": summary.get("revenueChart") or [],
        "expensesChart": expenses_chart,
        "syncSparkline": spark_counts,
        "syncSparklineLabels": spark_labels,
        "occupancyRate": summary.get("occupancyRate", 0),
        "alerts": summary.get("alerts") or [],
        "quickStats": summary.get("quickStats") or [],
        "dataSources": summary.get("dataSources") or {},
        "explanations": explanations,
        "validationTableRows": validation_rows,
        "activityTableRows": activity_rows,
        "notificationTableRows": notification_rows,
        "movementTableRows": movement_rows,
        "tenantTableRows": tenant_rows,
        "entityCountRows": entity_rows,
        "recentSyncActivity": summary.get("recentSyncActivity") or [],
    }


def load_rapports(date_from: date | None = None, date_to: date | None = None) -> dict:
    today = timezone.localdate()
    if date_to is None:
        date_to = today
    if date_from is None:
        date_from = today.replace(day=1) - timedelta(days=330)

    start = timezone.make_aware(datetime.combine(date_from, datetime.min.time()))
    end = timezone.make_aware(datetime.combine(date_to, datetime.max.time()))

    personnel = []
    for e in Employee.objects.filter(deleted_at__isnull=True).order_by("full_name")[:500]:
        personnel.append({
            "Matricule": _full_text(e.employee_number),
            "Nom complet": _full_text(e.full_name),
            "Fonction": _full_text(e.position),
            "Département": _full_text(e.department),
            "Email": _full_text(e.email),
            "Téléphone": _full_text(e.phone),
            "Salaire mensuel": _money(e.monthly_salary),
            "Statut": "Actif" if e.is_active else "Inactif",
            **_audit_row(e),
        })

    rent_qs = filter_to_synced(
        RentPayment.objects.filter(deleted_at__isnull=True).select_related(
            "lease_contract", "lease_contract__tenant", "lease_contract__premise"
        ),
        "RentPayments",
    )
    loyers = []
    for p in rent_qs.order_by("-year", "-month")[:500]:
        contract = p.lease_contract
        loyers.append({
            "Année": p.year,
            "Mois": p.month,
            "Période": f"{p.month:02d}/{p.year}",
            "ID contrat (sync)": _full_text(p.lease_contract_id_sync),
            "N° contrat": _full_text(contract.contract_number if contract else None),
            "Locataire": _full_text(contract.tenant.name if contract and contract.tenant else None),
            "Local": _full_text(contract.premise.name if contract and contract.premise else None),
            "Montant dû": _money(p.amount_due),
            "Montant payé": _money(p.amount_paid),
            "Échéance": _fmt_date(p.due_date),
            "Date paiement": _fmt_date(p.paid_date),
            "Statut paiement": _full_text(
                p.payment_status or ("Payé" if p.amount_paid >= p.amount_due else "En attente")
            ),
            "En retard": "Oui" if p.is_late else "Non",
            **_audit_row(p),
        })

    depenses = []
    for t in FinancialTransaction.objects.filter(
        deleted_at__isnull=True,
        type=FinancialTransaction.TxType.DEPENSE,
        transaction_date__gte=start,
        transaction_date__lte=end,
    ).order_by("-transaction_date")[:500]:
        depenses.append({
            "Date transaction": _fmt_datetime(t.transaction_date),
            "Type": "Dépense",
            "Catégorie": _full_text(t.category),
            "Montant": _money(t.amount),
            "Description": _full_text(t.description),
            "Référence": _full_text(t.reference),
            "Mode paiement": _full_text(t.payment_method),
            "Statut": _full_text(t.status),
            "Enregistré par": _full_text(t.recorded_by),
            "Approbation PDG requise": "Oui" if t.requires_pdg_approval else "Non",
            "Approuvé le": _fmt_datetime(t.approved_at),
            "Approuvé par": _full_text(t.approved_by),
            **_audit_row(t),
        })

    consommations = []
    for c in ConsumptionRecord.objects.filter(deleted_at__isnull=True).order_by("-period_end")[:500]:
        consommations.append({
            "Type consommation": _full_text(c.consumption_type),
            "Période début": _fmt_date(c.period_start),
            "Période fin": _fmt_date(c.period_end),
            "Quantité": str(c.quantity),
            "Coût total": _money(c.cost),
            **_audit_row(c),
        })

    recettes = FinancialTransaction.objects.filter(
        deleted_at__isnull=True, type=FinancialTransaction.TxType.RECETTE
    ).aggregate(t=Sum("amount"))["t"] or Decimal(0)
    dep_total = FinancialTransaction.objects.filter(
        deleted_at__isnull=True, type=FinancialTransaction.TxType.DEPENSE
    ).aggregate(t=Sum("amount"))["t"] or Decimal(0)
    rent_paid = rent_qs.aggregate(t=Sum("amount_paid"))["t"] or Decimal(0)

    financier_lignes = []
    for t in FinancialTransaction.objects.filter(deleted_at__isnull=True).order_by("-transaction_date")[:500]:
        financier_lignes.append({
            "Date transaction": _fmt_datetime(t.transaction_date),
            "Type": "Recette" if t.type == FinancialTransaction.TxType.RECETTE else "Dépense",
            "Catégorie": _full_text(t.category),
            "Description": _full_text(t.description),
            "Montant": _money(t.amount),
            "Référence": _full_text(t.reference),
            "Mode paiement": _full_text(t.payment_method),
            "Statut": _full_text(t.status),
            "Enregistré par": _full_text(t.recorded_by),
            "Approbation PDG requise": "Oui" if t.requires_pdg_approval else "Non",
            "Approuvé le": _fmt_datetime(t.approved_at),
            "Approuvé par": _full_text(t.approved_by),
            **_audit_row(t),
        })

    contrats = []
    for c in LeaseContract.objects.filter(deleted_at__isnull=True).select_related(
        "premise", "tenant"
    ).order_by("-start_date")[:500]:
        contrats.append({
            "N° contrat": _full_text(c.contract_number),
            "Locataire": _full_text(c.tenant.name if c.tenant else None),
            "ID locataire (sync)": _full_text(c.tenant_id_sync),
            "Local": _full_text(c.premise.name if c.premise else None),
            "ID local (sync)": _full_text(c.premise_id_sync),
            "Date début": _fmt_date(c.start_date),
            "Date fin": _fmt_date(c.end_date),
            "Loyer mensuel": _money(c.monthly_rent),
            "Caution": _money(c.deposit),
            "Statut": _full_text(c.status),
            **_audit_row(c),
        })

    incidents = []
    for i in Incident.objects.filter(deleted_at__isnull=True).order_by("-reported_at")[:500]:
        incidents.append({
            "Code": _full_text(i.code),
            "Titre": _full_text(i.title),
            "Description": _full_text(i.description),
            "Type incident": _full_text(i.incident_type),
            "Sévérité": _full_text(i.severity),
            "Statut": _full_text(i.status),
            "Lieu": _full_text(i.location),
            "Bâtiment": _full_text(i.building),
            "Signalé le": _fmt_datetime(i.reported_at),
            "Coût": _money(i.cost),
            **_audit_row(i),
        })

    visites = []
    for v in Visitor.objects.filter(deleted_at__isnull=True).order_by("-check_in_at")[:500]:
        visites.append({
            "Visiteur": _full_text(v.full_name),
            "Société": _full_text(v.company),
            "Motif": _full_text(v.purpose),
            "Arrivée": _fmt_datetime(v.check_in_at),
            "Départ": _fmt_datetime(v.check_out_at),
            **_audit_row(v),
        })

    activites = []
    for e in ServerSyncEvent.objects.order_by("-created_at")[:500]:
        activites.append({
            "ID événement": str(e.id),
            "Date/heure": _fmt_datetime(e.created_at),
            "Utilisateur": _full_text(e.username) if e.username else "Système",
            "Rôle": _full_text(e.user_role),
            "Type entité": _full_text(e.entity_type),
            "Direction": _full_text(e.direction),
            "Nb enregistrements": e.records_count,
            "Succès": "Oui" if e.success else "Non",
            "Message erreur": _full_text(e.error_message),
        })

    sections = [
        {"index": 0, "label": "Personnel", "rows": personnel},
        {"index": 1, "label": "Loyers", "rows": loyers},
        {"index": 2, "label": "Dépenses", "rows": depenses},
        {"index": 3, "label": "Consommations", "rows": consommations},
        {"index": 4, "label": "Financier", "rows": financier_lignes},
        {"index": 5, "label": "Contrats", "rows": contrats},
        {"index": 6, "label": "Incidents", "rows": incidents},
        {"index": 7, "label": "Visites", "rows": visites},
        {"index": 8, "label": "Activités", "rows": activites},
    ]

    return {
        "layout": "desktop",
        "dateFrom": date_from.isoformat(),
        "dateTo": date_to.isoformat(),
        "sections": sections,
        "sectionTabs": [s["label"] for s in sections],
        "financierSummary": {
            "loyersEncaisses": float(rent_paid),
            "totalEntrees": float(recettes + rent_paid),
            "totalSorties": float(dep_total),
            "solde": float(recettes + rent_paid - dep_total),
        },
        "kpisBySection": {
            "0": [
                {"label": "Effectif", "value": len(personnel)},
                {"label": "Actifs", "value": sum(1 for p in personnel if p["Statut"] == "Actif")},
                {"label": "Départements", "value": len({p["Département"] for p in personnel})},
                {"label": "Enregistrements", "value": len(personnel)},
                {"label": "Période", "value": f"{date_from:%d/%m} – {date_to:%d/%m}"},
            ],
            "1": [
                {"label": "Paiements", "value": len(loyers)},
                {"label": "Encaissé", "value": _money(rent_paid)},
                {"label": "En retard", "value": sum(1 for l in loyers if l.get("En retard") == "Oui")},
                {"label": "Lignes", "value": len(loyers)},
                {"label": "Période", "value": f"{date_from:%d/%m} – {date_to:%d/%m}"},
            ],
        },
    }


def load_users(current_username: str | None = None) -> dict:
    today = timezone.localdate()
    yesterday = today - timedelta(days=1)
    online_threshold = timezone.now() - timedelta(minutes=30)

    users = list(
        User.objects.filter(deleted_at__isnull=True).order_by("-created_at")
    )
    items = []
    table_rows = []
    for u in users:
        is_online = u.is_active and u.last_login_at and u.last_login_at >= online_threshold
        last_login = _fmt_datetime(u.last_login_at) if u.last_login_at else "Jamais"
        row = {
            "id": str(u.id),
            "username": u.username,
            "fullName": u.full_name or u.username,
            "email": u.email or "—",
            "roleLabel": u.role,
            "department": "Administration",
            "statusLabel": "Actif" if u.is_active else "Suspendu",
            "isOnline": is_online,
            "lastLoginDisplay": last_login,
            "initials": _initials(u.full_name or u.username),
            "createdAtDisplay": _fmt_datetime(u.created_at),
            "updatedAtDisplay": _fmt_datetime(u.updated_at),
            "deletedAtDisplay": _fmt_datetime(u.deleted_at),
            "isSynced": u.is_synced,
            "isStaff": u.is_staff,
            "jobTitle": u.role,
        }
        items.append(row)
        table_rows.append({
            "ID": str(u.id),
            "Identifiant": u.username,
            "Nom complet": _full_text(u.full_name),
            "Email": _full_text(u.email),
            "Rôle": u.role,
            "Statut": "Actif" if u.is_active else "Suspendu",
            "Staff": "Oui" if u.is_staff else "Non",
            "En ligne": "Oui" if is_online else "Non",
            "Dernière connexion": last_login,
            "Créé le": _fmt_datetime(u.created_at),
            "Modifié le": _fmt_datetime(u.updated_at),
            "Synchronisé": "Oui" if u.is_synced else "Non",
            "Supprimé le": _fmt_datetime(u.deleted_at),
        })

    logins_today = sum(1 for u in users if u.last_login_at and u.last_login_at.date() == today)
    logins_yesterday = sum(1 for u in users if u.last_login_at and u.last_login_at.date() == yesterday)
    active = sum(1 for u in users if u.is_active)
    suspended = len(users) - active
    admins = sum(1 for u in users if u.role in (User.Role.ADMIN, User.Role.PDG))
    sessions = sum(1 for u in users if u.is_active and u.last_login_at and u.last_login_at >= online_threshold)

    role_dist = {}
    for u in users:
        role_dist[u.role] = role_dist.get(u.role, 0) + 1

    login_trend = []
    for i in range(6, -1, -1):
        d = today - timedelta(days=i)
        login_trend.append({
            "label": d.strftime("%a"),
            "count": sum(1 for u in users if u.last_login_at and u.last_login_at.date() == d),
        })

    assignable_roles = [r.label for r in User.Role]

    return {
        "layout": "desktop",
        "canManageUsers": True,
        "totalCount": len(users),
        "administratorsCount": admins,
        "activeCount": active,
        "suspendedCount": suspended,
        "loginsTodayCount": logins_today,
        "activeSessionsCount": max(sessions, 1 if current_username else 0),
        "totalTrend": _trend(len(users), len(users)),
        "administratorsTrend": str(admins),
        "activeTrend": _trend(active, active),
        "suspendedTrend": str(suspended),
        "loginsTodayTrend": _trend(logins_today, logins_yesterday),
        "activeSessionsTrend": "Temps réel",
        "loginsSparkline": [p["count"] for p in login_trend],
        "users": items,
        "tableRows": table_rows,
        "roleDistribution": [{"role": k, "count": v} for k, v in role_dist.items()],
        "statusDistribution": [
            {"status": "Actif", "count": active},
            {"status": "Suspendu", "count": suspended},
        ],
        "loginTrend": login_trend,
        "roleFilters": ["Tous les rôles"] + sorted(role_dist.keys()),
        "assignableRoles": assignable_roles,
        "permissionsCatalog": [
            {"code": c, "name": c.replace(".", " ").title(), "module": c.split(".")[0]}
            for c in ALL_PERMISSION_CODES
        ],
    }


def load_user_detail(user_id: str) -> dict | None:
    try:
        u = User.objects.get(id=user_id, deleted_at__isnull=True)
    except User.DoesNotExist:
        return None
    perms = permissions_for_role(u.role)
    return {
        "id": str(u.id),
        "username": u.username,
        "fullName": u.full_name or u.username,
        "email": u.email or "",
        "roleLabel": u.role,
        "statusLabel": "Actif" if u.is_active else "Suspendu",
        "isStaff": u.is_staff,
        "isSynced": u.is_synced,
        "createdAtDisplay": _fmt_datetime(u.created_at),
        "updatedAtDisplay": _fmt_datetime(u.updated_at),
        "deletedAtDisplay": _fmt_datetime(u.deleted_at),
        "lastLoginDisplay": _fmt_datetime(u.last_login_at) if u.last_login_at else "Jamais",
        "permissions": [
            {"code": p, "name": p.replace(".", " ").title(), "module": p.split(".")[0]}
            for p in perms
        ],
        "activities": [],
        "sessions": [],
    }


def load_sync_page() -> dict:
    health = get_sync_health(window_hours=168)
    info = database_info()

    entity_types = [
        "Users", "Employees", "Premises", "Tenants", "LeaseContracts", "RentPayments",
        "FinancialTransactions", "Incidents", "ConsumptionRecords", "Visitors",
    ]
    data_types = []
    total_store = 0
    for et in entity_types:
        count = sync_store_count(et)
        total_store += count
        data_types.append({"name": et, "synced": count, "total": count, "isComplete": True})

    events = list(ServerSyncEvent.objects.order_by("-created_at")[:200])
    history = []
    history_table_rows = []
    for e in events:
        history.append({
            "id": str(e.id),
            "startedAt": _iso(e.created_at),
            "startedAtDisplay": _fmt_datetime(e.created_at),
            "typeLabel": e.entity_type or "Sync",
            "success": e.success,
            "itemsCount": e.records_count,
            "durationLabel": "—",
            "dataSizeLabel": "—",
            "userName": e.username or "Système",
            "userRole": e.user_role or "—",
            "direction": e.direction or "—",
            "detail": e.error_message or e.direction,
            "errorMessage": e.error_message or "—",
        })
        history_table_rows.append({
            "ID événement": str(e.id),
            "Date/heure": _fmt_datetime(e.created_at),
            "Utilisateur": _full_text(e.username) if e.username else "Système",
            "Rôle": _full_text(e.user_role),
            "Type entité": _full_text(e.entity_type),
            "Direction": _full_text(e.direction),
            "Nb enregistrements": e.records_count,
            "Succès": "Oui" if e.success else "Non",
            "Message erreur": _full_text(e.error_message),
        })

    failed = [e for e in events if not e.success]
    pending_types = {}
    for e in failed[-10:]:
        key = e.entity_type or "Sync"
        pending_types[key] = pending_types.get(key, 0) + 1

    pending_items = [
        {
            "typeLabel": k,
            "description": f"{v} événement(s) en échec récent(s)",
            "createdAt": _iso(events[0].created_at) if events else None,
        }
        for k, v in pending_types.items()
    ]

    last = events[0] if events else None
    success_rate = health.get("successRate", 100)

    return {
        "layout": "desktop",
        "syncedCount": health.get("recordsSynced", 0),
        "pendingCount": health.get("failedEvents", 0),
        "conflictCount": 0,
        "totalRecords": total_store,
        "localDatabaseLabel": "PostgreSQL (Render)",
        "cloudServerUrl": info.get("remoteApiUrl") or "https://smartbuilding-0kbk.onrender.com",
        "lastSyncAt": health.get("lastSyncAt"),
        "isOnline": True,
        "isCloudReachable": health.get("hasBusinessData", False),
        "pingMs": 0,
        "syncIntervalSeconds": 60,
        "globalProgress": min(100, int(success_rate)),
        "syncStatusText": (
            "À jour — sync automatique active"
            if success_rate >= 90
            else "Synchronisation en cours — vérifiez les erreurs"
        ),
        "dataTypes": data_types,
        "pendingItems": pending_items,
        "conflicts": [],
        "history": history,
        "historyTableRows": history_table_rows,
        "alerts": [
            {
                "title": "Portail cloud",
                "message": f"Taux de succès sync (7j) : {success_rate} %",
                "timeLabel": "Maintenant",
            }
        ],
        "last7DaysCounts": _last7_sync_counts(),
        "lastSyncError": failed[0].error_message if failed else None,
        "autoSyncEnabled": True,
        "autoSyncStatusLabel": "Active (desktop)",
        "cloudDbOnline": True,
        "isSynchronized": health.get("failedEvents", 0) == 0,
        "cloudIdentityMessage": f"{total_store} entité(s) synchronisée(s) depuis le desktop.",
    }


def _last7_sync_counts() -> list[int]:
    today = timezone.localdate()
    out = []
    for i in range(6, -1, -1):
        d = today - timedelta(days=i)
        start = timezone.make_aware(datetime.combine(d, datetime.min.time()))
        end = timezone.make_aware(datetime.combine(d, datetime.max.time()))
        out.append(
            ServerSyncEvent.objects.filter(created_at__gte=start, created_at__lte=end).count()
        )
    return out


def load_activity_log() -> dict:
    today = timezone.localdate()
    range_start = today - timedelta(days=3)
    events = list(ServerSyncEvent.objects.order_by("-created_at")[:500])

    activities = []
    for e in events:
        act_type = "Synchronisation"
        if e.direction == "push":
            act_type = "Modification"
        if not e.success:
            act_type = "Erreur"
        if "login" in (e.direction or "").lower():
            act_type = "Connexion"

        activities.append({
            "id": str(e.id),
            "timeDisplay": timezone.localtime(e.created_at).strftime("%H:%M:%S"),
            "dateDisplay": timezone.localtime(e.created_at).strftime("%d/%m/%Y"),
            "dateTimeDisplay": _fmt_datetime(e.created_at),
            "userName": e.username or "Système",
            "userRole": e.user_role or "—",
            "userInitials": _initials(e.username or "S"),
            "actionTitle": f"{e.entity_type} — {e.direction}",
            "actionDescription": e.error_message or f"{e.records_count} enregistrement(s)",
            "module": e.entity_type or "Sync",
            "entityType": e.entity_type or "—",
            "direction": e.direction or "—",
            "recordsCount": e.records_count,
            "details": e.error_message or "",
            "errorMessage": e.error_message or "—",
            "deviceInfo": "Cloud Render",
            "ipAddress": "—",
            "statusLabel": "Succès" if e.success else "Échec",
            "success": e.success,
            "activityType": act_type,
            "activityCode": str(e.id),
            "occurredAt": _iso(e.created_at),
            "oldValues": "",
            "newValues": "",
        })

    today_items = [a for a in activities if a["dateDisplay"] == today.strftime("%d/%m/%Y")]
    yesterday = (today - timedelta(days=1)).strftime("%d/%m/%Y")
    yesterday_items = [a for a in activities if a["dateDisplay"] == yesterday]

    def count_type(items, t):
        return sum(1 for a in items if a["activityType"] == t)

    return {
        "layout": "desktop",
        "activitiesToday": len(today_items),
        "loginsCount": count_type(today_items, "Connexion"),
        "modificationsCount": count_type(today_items, "Modification"),
        "securityAlertsCount": 0,
        "systemErrorsCount": count_type(today_items, "Erreur"),
        "syncCount": count_type(today_items, "Synchronisation"),
        "activitiesTodayTrend": _trend(len(today_items), len(yesterday_items)),
        "loginsTrend": _trend(count_type(today_items, "Connexion"), count_type(yesterday_items, "Connexion")),
        "modificationsTrend": _trend(count_type(today_items, "Modification"), count_type(yesterday_items, "Modification")),
        "securityAlertsTrend": "—",
        "systemErrorsTrend": _trend(count_type(today_items, "Erreur"), count_type(yesterday_items, "Erreur")),
        "syncTrend": _trend(count_type(today_items, "Synchronisation"), count_type(yesterday_items, "Synchronisation")),
        "activitiesSparkline": _last7_sync_counts(),
        "activities": activities,
        "tableRows": [
            {
                "ID": a["activityCode"],
                "Date/heure": a["dateTimeDisplay"],
                "Type activité": a["activityType"],
                "Utilisateur": a["userName"],
                "Rôle": a["userRole"],
                "Module / entité": a["entityType"],
                "Direction": a["direction"],
                "Nb enregistrements": a["recordsCount"],
                "Statut": a["statusLabel"],
                "Action": a["actionTitle"],
                "Description": a["actionDescription"],
                "Message erreur": a["errorMessage"],
                "Appareil": a["deviceInfo"],
                "Adresse IP": a["ipAddress"],
            }
            for a in activities
        ],
        "typeFilters": ["Tous les types"] + sorted({a["activityType"] for a in activities}),
        "moduleFilters": ["Tous les modules"] + sorted({a["module"] for a in activities}),
        "userFilters": ["Tous les utilisateurs"] + sorted({a["userName"] for a in activities}),
        "statusFilters": ["Tous les statuts", "Succès", "Échec", "Erreur"],
        "dateRangeStart": range_start.isoformat(),
        "dateRangeEnd": today.isoformat(),
    }


def load_settings_page() -> dict:
    info = database_info()
    health = get_sync_health()
    categories = [
        {"id": "general", "label": "Général", "icon": "tune"},
        {"id": "buildings", "label": "Société & bailleur", "icon": "domain"},
        {"id": "utilisateurs", "label": "Utilisateurs & rôles", "icon": "people"},
        {"id": "permissions", "label": "Permissions", "icon": "shield"},
        {"id": "synchronisation", "label": "Synchronisation", "icon": "sync"},
        {"id": "backups", "label": "Sauvegardes", "icon": "backup"},
        {"id": "security", "label": "Sécurité", "icon": "shield-check"},
        {"id": "notifications", "label": "Notifications", "icon": "bell"},
        {"id": "logs", "label": "Logs système", "icon": "journal-text"},
        {"id": "about", "label": "À propos", "icon": "info-circle"},
    ]
    building = Building.objects.filter(deleted_at__isnull=True).first()
    sync_entities = list(
        SyncedEntityStore.objects.order_by("-updated_at")[:300]
    )
    sync_table_rows = [
        {
            "ID": str(s.id),
            "Type entité": s.entity_type,
            "Créé le": _fmt_datetime(s.created_at),
            "Modifié le": _fmt_datetime(s.updated_at),
            "Supprimé le": _fmt_datetime(s.deleted_at),
            "Données JSON": str(s.json_data) if s.json_data else "—",
        }
        for s in sync_entities
    ]
    building_rows = []
    if building:
        building_rows.append({
            "ID": str(building.id),
            "Nom": _full_text(building.name),
            "Adresse": _full_text(building.address),
            "Ville": _full_text(building.city),
            "Étages": building.floors,
            "Créé le": _fmt_datetime(building.created_at),
            "Modifié le": _fmt_datetime(building.updated_at),
            "Synchronisé": "Oui" if building.is_synced else "Non",
            "Supprimé le": _fmt_datetime(building.deleted_at),
        })
    return {
        "layout": "desktop",
        "categories": categories,
        "selectedCategoryId": "general",
        "companyName": building.name if building else "BLOOM PROSPERTY INVESTISSEMENT",
        "databaseLabel": info.get("engineLabel", "PostgreSQL"),
        "databasePathDisplay": info.get("name", "—"),
        "environmentName": "Production (Render)" if info.get("isRender") else "Développement",
        "appVersion": "SBMS Web 1.0",
        "activeUsersDisplay": str(User.objects.filter(deleted_at__isnull=True, is_active=True).count()),
        "rolesDisplay": str(len(User.Role.choices)),
        "syncKpiLabel": f"{health.get('successRate', 100)} %",
        "syncKpiSub": "Taux succès sync (24h)",
        "securityKpiLabel": "JWT",
        "securityKpiSub": "Authentification active",
        "stats": [
            {"label": "Entités sync", "value": SyncedEntityStore.objects.count()},
            {"label": "Utilisateurs", "value": User.objects.filter(deleted_at__isnull=True).count()},
            {"label": "Locataires", "value": Tenant.objects.filter(deleted_at__isnull=True).count()},
            {"label": "Contrats", "value": LeaseContract.objects.filter(deleted_at__isnull=True).count()},
            {"label": "Locaux", "value": Premise.objects.filter(deleted_at__isnull=True).count()},
        ],
        "canResetDatabase": True,
        "resetConfirmPhrase": "REINITIALISER SBMS",
        "buildingTableRows": building_rows,
        "syncEntityTableRows": sync_table_rows,
    }


_DOCUMENT_CATEGORIES: tuple[tuple[str, str, str], ...] = (
    ("all", "Tous les documents", "#2D6A4F"),
    ("contrats", "Contrats", "#7C3AED"),
    ("factures", "Factures", "#2563EB"),
    ("personnel", "Personnel", "#0EA5E9"),
    ("technique", "Technique", "#EA580C"),
    ("securite", "Sécurité", "#DC2626"),
    ("fournisseurs", "Fournisseurs", "#D97706"),
    ("emails", "Emails", "#64748B"),
    ("rapports", "Rapports", "#6D28D9"),
    ("inventaire", "Inventaire", "#166534"),
    ("archives", "Archives", "#94A3B8"),
)

_CATEGORY_LABELS = {cid: label for cid, label, _ in _DOCUMENT_CATEGORIES if cid != "all"}
_CATEGORY_LABELS["corbeille"] = "Corbeille"

_DEFAULT_QUOTA_BYTES = 20 * 1024 * 1024 * 1024


def _fmt_size(n: int) -> str:
    if n < 1024:
        return f"{n} o"
    if n < 1024 * 1024:
        return f"{n / 1024:.1f} Ko"
    return f"{n / (1024 * 1024):.1f} Mo"


def _file_type_label(mime: str, file_name: str) -> str:
    if mime == "application/pdf" or file_name.lower().endswith(".pdf"):
        return "PDF"
    if "word" in mime or file_name.lower().endswith((".doc", ".docx")):
        return "Word"
    if "excel" in mime or "spreadsheet" in mime or file_name.lower().endswith((".xls", ".xlsx")):
        return "Excel"
    if file_name.lower().endswith(".csv"):
        return "CSV"
    return "Fichier"


def load_documents_page() -> dict:
    today = timezone.localdate()
    week_start = today - timedelta(days=7)
    month_start = today.replace(day=1)
    prev_month_start = (month_start - timedelta(days=1)).replace(day=1)

    docs = list(SyncedDocument.objects.order_by("-updated_at")[:2000])
    total_bytes = sum(d.file_size for d in docs)

    recent = sum(1 for d in docs if d.updated_at.date() >= week_start)
    recent_prev = sum(
        1
        for d in docs
        if week_start - timedelta(days=7) <= d.updated_at.date() < week_start
    )
    contracts = sum(
        1 for d in docs if d.category in ("contrats", "fournisseurs")
    )
    contracts_prev = sum(
        1
        for d in docs
        if d.category in ("contrats", "fournisseurs")
        and d.updated_at.date() < month_start
    )
    this_month = sum(1 for d in docs if d.updated_at.date() >= month_start)
    prev_month = sum(
        1
        for d in docs
        if prev_month_start <= d.updated_at.date() < month_start
    )
    shared = sum(1 for d in docs if d.added_by)

    storage_percent = min(
        100.0,
        round(total_bytes * 100.0 / _DEFAULT_QUOTA_BYTES, 1) if _DEFAULT_QUOTA_BYTES else 0,
    )

    def count_for_category(cat_id: str) -> int:
        if cat_id == "all":
            return len(docs)
        return sum(1 for d in docs if d.category == cat_id)

    categories = [
        {
            "categoryId": cid,
            "label": label,
            "iconColor": color,
            "count": count_for_category(cid),
            "isSelected": cid == "all",
        }
        for cid, label, color in _DOCUMENT_CATEGORIES
    ]

    items = []
    table_rows = []
    for d in docs:
        item = {
            "id": str(d.id),
            "fileName": d.file_name,
            "fileType": _file_type_label(d.mime_type, d.file_name),
            "categoryId": d.category,
            "categoryLabel": _CATEGORY_LABELS.get(d.category, "Document"),
            "entityType": d.entity_type,
            "entityId": str(d.entity_id),
            "sizeDisplay": _fmt_size(d.file_size),
            "sizeBytes": d.file_size,
            "dateDisplay": _fmt_datetime(d.updated_at),
            "addedAtDisplay": _fmt_datetime(d.created_at),
            "modifiedAtDisplay": _fmt_datetime(d.updated_at),
            "addedBy": d.added_by or "Desktop SBMS",
            "downloadUrl": f"/api/documents/{d.id}/",
            "mimeType": d.mime_type,
            "contentSha256": d.content_sha256 or "—",
            "status": "Synchronisé",
        }
        items.append(item)
        table_rows.append({
            "ID document": str(d.id),
            "Fichier": d.file_name,
            "Format": _file_type_label(d.mime_type, d.file_name),
            "Type MIME": d.mime_type,
            "Catégorie": _CATEGORY_LABELS.get(d.category, d.category),
            "Type entité": d.entity_type,
            "ID entité": str(d.entity_id),
            "Taille": _fmt_size(d.file_size),
            "Taille (octets)": d.file_size,
            "SHA-256": d.content_sha256 or "—",
            "Ajouté par": d.added_by or "Desktop SBMS",
            "Créé le": _fmt_datetime(d.created_at),
            "Modifié le": _fmt_datetime(d.updated_at),
            "Statut": "Synchronisé",
            "Téléchargement": f"/api/documents/{d.id}/",
        })

    entity_types = sorted({d.entity_type for d in docs if d.entity_type})

    return {
        "layout": "desktop",
        "selectedCategoryId": "all",
        "categories": categories,
        "documents": items,
        "tableRows": table_rows,
        "entityTypeFilters": ["Tous types", *entity_types],
        "totalCount": len(docs),
        "recentCount": recent,
        "activeContractsCount": contracts,
        "sharedCount": shared,
        "criticalCount": 0,
        "storagePercent": storage_percent,
        "storageDisplay": _fmt_size(total_bytes),
        "totalTrend": _trend(this_month, prev_month),
        "recentTrend": _trend(recent, recent_prev),
        "contractsTrend": _trend(contracts, contracts_prev),
        "sharedTrend": f"{shared} partagés" if shared else "—",
        "storageTrend": f"{storage_percent} % quota",
        "criticalTrend": "—",
        "emptyMessage": (
            "Aucun document cloud pour le moment. "
            "Générez des PDF depuis le desktop SBMS puis lancez une synchronisation."
            if not docs
            else ""
        ),
    }
