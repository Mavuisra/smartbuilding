from datetime import timedelta
from decimal import Decimal

from django.db.models import Sum
from django.utils import timezone

from api.models import (
    Employee,
    ExecutiveNotification,
    FinancialTransaction,
    Incident,
    LeaseContract,
    Premise,
    RentPayment,
    ServerSyncEvent,
    SyncedEntityStore,
    Tenant,
)
from api.services.diagnostics import get_data_pipeline_diagnostics


def _fc(amount) -> str:
    return f"$ {amount:,.2f}"


def get_executive_summary() -> dict:
    today = timezone.localdate()
    month_start = today.replace(day=1)

    rent_qs = RentPayment.objects.filter(deleted_at__isnull=True)
    month_rents = list(rent_qs.filter(year=today.year, month=today.month))
    if month_rents:
        rent_collected = sum((r.amount_paid for r in month_rents), Decimal("0"))
        rent_planned = sum((r.amount_due for r in month_rents), Decimal("0"))
        late_count = sum(1 for r in month_rents if r.is_late or r.amount_paid < r.amount_due)
        rent_late_amount = sum(
            (r.amount_due - r.amount_paid for r in month_rents if r.amount_paid < r.amount_due),
            Decimal("0"),
        )
    else:
        rent_collected, rent_planned, late_count, rent_late_amount = _rent_from_sync_store(
            today.year, today.month
        )

    expenses_month = (
        FinancialTransaction.objects.filter(
            deleted_at__isnull=True,
            type=FinancialTransaction.TxType.DEPENSE,
            transaction_date__date__gte=month_start,
        ).aggregate(t=Sum("amount"))["t"]
        or Decimal("0")
    )
    if expenses_month == 0:
        expenses_month = _expenses_from_sync_store(month_start)

    total_premises = Premise.objects.filter(deleted_at__isnull=True).count()
    occupied = Premise.objects.filter(deleted_at__isnull=True, is_occupied=True).count()
    if total_premises == 0:
        total_premises, occupied = _occupancy_from_sync_store()
    occupancy = (occupied / total_premises * 100) if total_premises else 0

    closed_statuses = {"Clôturé", "Cloture", "Résolu", "Resolu", "4", "3"}
    open_incidents = (
        Incident.objects.filter(deleted_at__isnull=True)
        .exclude(status__in=closed_statuses)
        .count()
    )

    active_leases = LeaseContract.objects.filter(
        deleted_at__isnull=True, status__icontains="Actif"
    ).count()
    total_tenants = Tenant.objects.filter(deleted_at__isnull=True).count()
    if active_leases == 0:
        active_leases = _count_sync_store("LeaseContracts", status_contains="actif")
    if total_tenants == 0:
        total_tenants = _count_sync_store("Tenants")

    recent_syncs = list(
        ServerSyncEvent.objects.filter(success=True)
        .order_by("-created_at")[:10]
        .values("username", "user_role", "entity_type", "records_count", "created_at")
    )

    revenue_chart = []
    for i in range(5, -1, -1):
        d = month_start - timedelta(days=30 * i)
        y, m = d.year, d.month
        val = rent_qs.filter(year=y, month=m).aggregate(t=Sum("amount_paid"))["t"] or 0
        revenue_chart.append({"label": d.strftime("%b %Y"), "value": float(val)})

    recent_movements = list(
        FinancialTransaction.objects.filter(deleted_at__isnull=True)
        .order_by("-transaction_date")[:8]
        .values("transaction_date", "type", "category", "description", "amount", "reference")
    )
    if not recent_movements:
        recent_movements = _recent_movements_from_sync_store(8)

    alerts = []
    if rent_late_amount > 0:
        alerts.append(
            {
                "title": "Loyers en retard",
                "message": f"{_fc(rent_late_amount)} à recouvrer ({late_count} paiement(s))",
                "severity": "Warning",
            }
        )
    if open_incidents > 0:
        alerts.append(
            {
                "title": "Incidents ouverts",
                "message": f"{open_incidents} incident(s) à traiter",
                "severity": "Error",
            }
        )
    if not alerts:
        alerts.append(
            {
                "title": "Situation stable",
                "message": "Aucune alerte critique",
                "severity": "Success",
            }
        )

    return {
        "monthlyRevenue": float(rent_collected),
        "rentRevenue": float(rent_collected),
        "monthlyExpenses": float(expenses_month),
        "netBalance": float(rent_collected - expenses_month),
        "rentCollected": float(rent_collected),
        "rentPlanned": float(rent_planned),
        "rentLateAmount": float(rent_late_amount),
        "openIncidents": open_incidents,
        "occupancyRate": round(occupancy, 1),
        "latePayments": late_count,
        "totalPremises": total_premises,
        "occupiedPremises": occupied,
        "activeLeases": active_leases,
        "totalTenants": total_tenants,
        "revenueChart": revenue_chart,
        "alerts": alerts,
        "recentMovements": [
            {
                "date": m["transaction_date"].isoformat(),
                "type": "IN" if m["type"] == 1 else "OUT",
                "category": m["category"],
                "description": m["description"],
                "amount": float(m["amount"]),
                "reference": m["reference"] or "—",
            }
            for m in recent_movements
        ],
        "recentSyncActivity": [
            {
                "text": (
                    f"{s['username']} ({s['user_role']}) — {s['entity_type']}: "
                    f"{s['records_count']} enregistrement(s)"
                ),
                "timestamp": s["created_at"].isoformat(),
            }
            for s in recent_syncs
        ],
        "quickStats": [
            {"label": "Locataires", "value": str(total_tenants)},
            {"label": "Contrats actifs", "value": str(active_leases)},
            {"label": "Taux d'occupation", "value": f"{occupancy:.1f} %"},
            {"label": "Loyers du mois", "value": _fc(rent_collected)},
            {"label": "Dépenses (mois)", "value": _fc(expenses_month)},
        ],
    }


def get_sync_health(window_hours: int = 24) -> dict:
    since = timezone.now() - timedelta(hours=window_hours)
    events = ServerSyncEvent.objects.filter(created_at__gte=since)

    total = events.count()
    successful = events.filter(success=True).count()
    failed = total - successful
    push_events = events.filter(direction="push").count()
    pull_events = events.filter(direction="pull").count()
    records_synced = events.aggregate(t=Sum("records_count"))["t"] or 0
    last_sync = events.order_by("-created_at").first()

    return {
        "windowHours": window_hours,
        "totalEvents": total,
        "successfulEvents": successful,
        "failedEvents": failed,
        "successRate": round((successful / total) * 100, 1) if total else 100.0,
        "pushEvents": push_events,
        "pullEvents": pull_events,
        "recordsSynced": records_synced,
        "lastSyncAt": last_sync.created_at.isoformat() if last_sync else None,
    }


def get_executive_overview() -> dict:
    summary = get_executive_summary()
    pending_contracts = list(
        LeaseContract.objects.filter(deleted_at__isnull=True)
        .exclude(status__icontains="actif")
        .order_by("-updated_at")[:6]
    )
    from api.services.finance_pdg import collect_pending_expenses

    active_employees = Employee.objects.filter(deleted_at__isnull=True, is_active=True).count()
    total_employees = Employee.objects.filter(deleted_at__isnull=True).count()

    validations = []
    for c in pending_contracts:
        validations.append(
            {
                "type": "Contrat",
                "reference": c.contract_number or f"CT-{str(c.id)[:8].upper()}",
                "description": "Validation contractuelle",
                "requester": "Gestionnaire",
                "requestDate": c.updated_at.isoformat(),
                "amount": float(c.monthly_rent),
                "status": c.status or "En attente",
            }
        )
    for exp in collect_pending_expenses(limit=8):
        validations.append(
            {
                "type": "Dépense",
                "reference": exp.reference or f"DEP-{exp.id[:8].upper()}",
                "description": (exp.description or "")[:60],
                "requester": exp.recorded_by or "Comptable",
                "requestDate": (
                    exp.transaction_date.isoformat()
                    if hasattr(exp.transaction_date, "isoformat")
                    else str(exp.transaction_date or "")
                ),
                "amount": float(exp.amount),
                "status": exp.status or "En attente validation PDG",
            }
        )
    validations = validations[:8]

    recent_activities = [
        {
            "text": item["text"],
            "timestamp": item["timestamp"],
            "category": "Synchronisation",
        }
        for item in summary["recentSyncActivity"][:5]
    ]
    for inc in (
        Incident.objects.filter(deleted_at__isnull=True)
        .order_by("-updated_at")
        .values("title", "updated_at")[:5]
    ):
        recent_activities.append(
            {
                "text": f"Incident signalé: {inc['title']}",
                "timestamp": inc["updated_at"].isoformat(),
                "category": "Incident",
            }
        )
    recent_activities.sort(key=lambda x: x["timestamp"], reverse=True)

    notifications = list(
        ExecutiveNotification.objects.all()
        .order_by("-created_at")
        .values("id", "title", "message", "severity", "source", "created_at")[:20]
    )
    unread_count = ExecutiveNotification.objects.filter(is_read=False).count()

    return {
        "summary": summary,
        "syncHealth": get_sync_health(),
        "diagnostics": get_data_pipeline_diagnostics(),
        "pendingValidations": validations,
        "recentActivities": recent_activities[:10],
        "notifications": [
            {
                "id": n["id"],
                "title": n["title"],
                "message": n["message"],
                "severity": n["severity"],
                "source": n["source"] or "Système",
                "timestamp": n["created_at"].isoformat(),
            }
            for n in notifications
        ],
        "unreadNotifications": unread_count,
        "presence": {
            "activeEmployees": active_employees,
            "totalEmployees": total_employees,
            "rate": round((active_employees / total_employees) * 100, 1)
            if total_employees
            else 0.0,
        },
    }


def _pick_json(data: dict, *keys, default=None):
    for key in keys:
        if key in data and data[key] not in (None, ""):
            return data[key]
    return default


def _count_sync_store(entity_type: str, status_contains: str | None = None) -> int:
    qs = SyncedEntityStore.objects.filter(entity_type=entity_type, deleted_at__isnull=True)
    if not status_contains:
        return qs.count()
    n = 0
    for row in qs.iterator():
        payload = row.json_data if isinstance(row.json_data, dict) else {}
        status = str(_pick_json(payload, "Status", "status", default="")).lower()
        if status_contains.lower() in status:
            n += 1
    return n


def _occupancy_from_sync_store() -> tuple[int, int]:
    total = 0
    occupied = 0
    for row in SyncedEntityStore.objects.filter(
        entity_type="Premises", deleted_at__isnull=True
    ).iterator():
        payload = row.json_data if isinstance(row.json_data, dict) else {}
        total += 1
        if _pick_json(payload, "IsOccupied", "isOccupied", default=False) in (
            True,
            "true",
            "True",
            1,
            "1",
        ):
            occupied += 1
    return total, occupied


def _expenses_from_sync_store(month_start) -> Decimal:
    total = Decimal("0")
    for row in SyncedEntityStore.objects.filter(
        entity_type="FinancialTransactions", deleted_at__isnull=True
    ).iterator():
        payload = row.json_data if isinstance(row.json_data, dict) else {}
        raw_type = _pick_json(payload, "Type", "type", default=1)
        is_expense = raw_type in (2, "2", "Depense", "Dépense") or (
            isinstance(raw_type, str) and "dep" in str(raw_type).lower()
        )
        if not is_expense:
            continue
        dt = _pick_json(payload, "TransactionDate", "transactionDate")
        parsed = parse_datetime_safe(dt)
        if parsed and parsed.date() < month_start:
            continue
        total += Decimal(str(_pick_json(payload, "Amount", "amount", default=0) or 0))
    return total


def parse_datetime_safe(value):
    from api.sync.utils import normalize_sync_datetime

    return normalize_sync_datetime(value)


def _recent_movements_from_sync_store(limit: int) -> list[dict]:
    rows = []
    for store in (
        SyncedEntityStore.objects.filter(
            entity_type="FinancialTransactions", deleted_at__isnull=True
        )
        .order_by("-updated_at")[:limit]
    ):
        payload = store.json_data if isinstance(store.json_data, dict) else {}
        raw_type = _pick_json(payload, "Type", "type", default=1)
        tx_type = 1
        if raw_type in (2, "2", "Depense", "Dépense") or (
            isinstance(raw_type, str) and "dep" in str(raw_type).lower()
        ):
            tx_type = 2
        dt = parse_datetime_safe(_pick_json(payload, "TransactionDate", "transactionDate"))
        rows.append(
            {
                "transaction_date": dt or store.updated_at,
                "type": tx_type,
                "category": _pick_json(payload, "Category", "category", default=""),
                "description": _pick_json(payload, "Description", "description", default=""),
                "amount": Decimal(str(_pick_json(payload, "Amount", "amount", default=0) or 0)),
                "reference": _pick_json(payload, "Reference", "reference", default=""),
            }
        )
    return rows


def _rent_from_sync_store(year: int, month: int) -> tuple[Decimal, Decimal, int, Decimal]:
    collected = Decimal("0")
    planned = Decimal("0")
    late_count = 0
    late_amount = Decimal("0")
    for row in SyncedEntityStore.objects.filter(
        entity_type="RentPayments", deleted_at__isnull=True
    ).iterator():
        payload = row.json_data if isinstance(row.json_data, dict) else {}
        y = int(_pick_json(payload, "Year", "year", default=0) or 0)
        m = int(_pick_json(payload, "Month", "month", default=0) or 0)
        if y != year or m != month:
            continue
        due = Decimal(str(_pick_json(payload, "AmountDue", "amountDue", default=0) or 0))
        paid = Decimal(str(_pick_json(payload, "AmountPaid", "amountPaid", default=0) or 0))
        planned += due
        collected += paid
        is_late = _pick_json(payload, "IsLate", "isLate", default=False) in (
            True,
            "true",
            True,
            1,
        )
        if is_late or paid < due:
            late_count += 1
            late_amount += max(due - paid, Decimal("0"))
    return collected, planned, late_count, late_amount
