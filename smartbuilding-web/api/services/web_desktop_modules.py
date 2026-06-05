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
    SyncedEntityStore,
    Tenant,
    User,
    Visitor,
)
from api.permission_codes import ALL_PERMISSION_CODES, permissions_for_role
from api.services.dashboard import get_sync_health
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


def load_rapports(date_from: date | None = None, date_to: date | None = None) -> dict:
    today = timezone.localdate()
    if date_to is None:
        date_to = today
    if date_from is None:
        date_from = today.replace(day=1) - timedelta(days=330)

    start = timezone.make_aware(datetime.combine(date_from, datetime.min.time()))
    end = timezone.make_aware(datetime.combine(date_to, datetime.max.time()))

    personnel = [
        {
            "Matricule": e.employee_number or "—",
            "Nom complet": e.full_name or "—",
            "Fonction": e.position or "—",
            "Département": e.department or "—",
            "Salaire": _money(e.monthly_salary),
            "Statut": "Actif" if e.is_active else "Inactif",
        }
        for e in Employee.objects.filter(deleted_at__isnull=True).order_by("full_name")[:500]
    ]

    rent_qs = filter_to_synced(
        RentPayment.objects.filter(deleted_at__isnull=True), "RentPayments"
    )
    loyers = []
    for p in rent_qs.order_by("-year", "-month")[:500]:
        loyers.append({
            "Période": f"{p.month:02d}/{p.year}",
            "Montant dû": _money(p.amount_due),
            "Montant payé": _money(p.amount_paid),
            "Échéance": _fmt_date(p.due_date),
            "Date paiement": _fmt_date(p.paid_date),
            "Statut": p.payment_status or ("Payé" if p.amount_paid >= p.amount_due else "En attente"),
            "En retard": "Oui" if p.is_late else "Non",
        })

    depenses = [
        {
            "Date": _fmt_date(t.transaction_date),
            "Catégorie": t.category or "—",
            "Montant": _money(t.amount),
            "Description": (t.description or "—")[:80],
            "Statut": t.status or "—",
            "Référence": t.reference or "—",
        }
        for t in FinancialTransaction.objects.filter(
            deleted_at__isnull=True,
            type=FinancialTransaction.TxType.DEPENSE,
            transaction_date__gte=start,
            transaction_date__lte=end,
        ).order_by("-transaction_date")[:500]
    ]

    consommations = [
        {
            "Période début": _fmt_date(c.period_start),
            "Période fin": _fmt_date(c.period_end),
            "Catégorie": c.consumption_type or "—",
            "Quantité": str(c.quantity),
            "Coût total": _money(c.cost),
        }
        for c in ConsumptionRecord.objects.filter(deleted_at__isnull=True).order_by("-period_end")[:500]
    ]

    recettes = FinancialTransaction.objects.filter(
        deleted_at__isnull=True, type=FinancialTransaction.TxType.RECETTE
    ).aggregate(t=Sum("amount"))["t"] or Decimal(0)
    dep_total = FinancialTransaction.objects.filter(
        deleted_at__isnull=True, type=FinancialTransaction.TxType.DEPENSE
    ).aggregate(t=Sum("amount"))["t"] or Decimal(0)
    rent_paid = rent_qs.aggregate(t=Sum("amount_paid"))["t"] or Decimal(0)

    financier_lignes = [
        {
            "Date": _fmt_date(t.transaction_date),
            "Type": "Recette" if t.type == FinancialTransaction.TxType.RECETTE else "Dépense",
            "Catégorie": t.category or "—",
            "Description": (t.description or "—")[:80],
            "Montant": _money(t.amount),
            "Référence": t.reference or "—",
            "Statut": t.status or "—",
        }
        for t in FinancialTransaction.objects.filter(deleted_at__isnull=True)
        .order_by("-transaction_date")[:300]
    ]

    contrats = [
        {
            "N° contrat": c.contract_number or "—",
            "Début": _fmt_date(c.start_date),
            "Fin": _fmt_date(c.end_date),
            "Loyer mensuel": _money(c.monthly_rent),
            "Statut": c.status or "—",
        }
        for c in LeaseContract.objects.filter(deleted_at__isnull=True).order_by("-start_date")[:300]
    ]

    incidents = [
        {
            "Code": i.code or "—",
            "Titre": i.title or "—",
            "Sévérité": i.severity or "—",
            "Statut": i.status or "—",
            "Lieu": i.location or "—",
            "Coût": _money(i.cost),
        }
        for i in Incident.objects.filter(deleted_at__isnull=True).order_by("-reported_at")[:300]
    ]

    visites = [
        {
            "Visiteur": v.full_name or "—",
            "Société": v.company or "—",
            "Motif": v.purpose or "—",
            "Arrivée": _iso(v.check_in_at),
            "Départ": _iso(v.check_out_at),
        }
        for v in Visitor.objects.filter(deleted_at__isnull=True).order_by("-check_in_at")[:300]
    ]

    activites = [
        {
            "Utilisateur": e.username or "Système",
            "Rôle": e.user_role or "—",
            "Type": e.entity_type,
            "Direction": e.direction,
            "Succès": "Oui" if e.success else "Non",
            "Date": _iso(e.created_at),
        }
        for e in ServerSyncEvent.objects.order_by("-created_at")[:300]
    ]

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
    for u in users:
        is_online = u.is_active and u.last_login_at and u.last_login_at >= online_threshold
        items.append({
            "id": str(u.id),
            "username": u.username,
            "fullName": u.full_name or u.username,
            "email": u.email or "—",
            "roleLabel": u.role,
            "department": "Administration",
            "statusLabel": "Actif" if u.is_active else "Suspendu",
            "isOnline": is_online,
            "lastLoginDisplay": (
                timezone.localtime(u.last_login_at).strftime("%d/%m/%Y %H:%M")
                if u.last_login_at else "Jamais"
            ),
            "initials": _initials(u.full_name or u.username),
            "createdAtDisplay": timezone.localtime(u.created_at).strftime("%d/%m/%Y"),
            "jobTitle": u.role,
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
        "createdAtDisplay": timezone.localtime(u.created_at).strftime("%d/%m/%Y %H:%M"),
        "lastLoginDisplay": (
            timezone.localtime(u.last_login_at).strftime("%d/%m/%Y %H:%M")
            if u.last_login_at else "Jamais"
        ),
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

    events = list(ServerSyncEvent.objects.order_by("-created_at")[:50])
    history = [
        {
            "startedAt": _iso(e.created_at),
            "typeLabel": e.entity_type or "Sync",
            "success": e.success,
            "itemsCount": e.records_count,
            "durationLabel": "—",
            "dataSizeLabel": "—",
            "userName": e.username or "Système",
            "detail": e.error_message or e.direction,
        }
        for e in events
    ]

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
            "timeDisplay": timezone.localtime(e.created_at).strftime("%H:%M"),
            "dateDisplay": timezone.localtime(e.created_at).strftime("%d/%m/%Y"),
            "userName": e.username or "Système",
            "userRole": e.user_role or "—",
            "userInitials": _initials(e.username or "S"),
            "actionTitle": f"{e.entity_type} — {e.direction}",
            "actionDescription": e.error_message or f"{e.records_count} enregistrement(s)",
            "module": e.entity_type or "Sync",
            "details": e.error_message or "",
            "deviceInfo": "Cloud Render",
            "ipAddress": "—",
            "statusLabel": "Succès" if e.success else "Échec",
            "activityType": act_type,
            "activityCode": str(e.id)[:8],
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
    }
