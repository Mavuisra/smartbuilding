from datetime import timedelta
from decimal import Decimal

from django.db.models import Sum
from django.utils import timezone

from api.models import (
    Employee,
    FinancialTransaction,
    Incident,
    LeaseContract,
    Premise,
    RentPayment,
    ServerSyncEvent,
    Tenant,
)


def _fc(amount) -> str:
    return f"{amount:,.0f} FC".replace(",", " ")


def get_executive_summary() -> dict:
    today = timezone.localdate()
    month_start = today.replace(day=1)

    rent_qs = RentPayment.objects.filter(deleted_at__isnull=True)
    month_rents = list(rent_qs.filter(year=today.year, month=today.month))
    rent_collected = sum((r.amount_paid for r in month_rents), Decimal("0"))
    rent_planned = sum((r.amount_due for r in month_rents), Decimal("0"))
    late_count = sum(1 for r in month_rents if r.is_late or r.amount_paid < r.amount_due)
    rent_late_amount = sum(
        (r.amount_due - r.amount_paid for r in month_rents if r.amount_paid < r.amount_due),
        Decimal("0"),
    )

    expenses_month = (
        FinancialTransaction.objects.filter(
            deleted_at__isnull=True,
            type=FinancialTransaction.TxType.DEPENSE,
            transaction_date__date__gte=month_start,
        ).aggregate(t=Sum("amount"))["t"]
        or Decimal("0")
    )

    total_premises = Premise.objects.filter(deleted_at__isnull=True).count()
    occupied = Premise.objects.filter(deleted_at__isnull=True, is_occupied=True).count()
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
    pending_payments = list(
        FinancialTransaction.objects.filter(
            deleted_at__isnull=True,
            type=FinancialTransaction.TxType.DEPENSE,
            status__icontains="attente",
        )
        .order_by("-updated_at")[:6]
    )
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
    for p in pending_payments:
        validations.append(
            {
                "type": "Dépense",
                "reference": p.reference or f"DEP-{str(p.id)[:8].upper()}",
                "description": p.description[:60],
                "requester": p.recorded_by or "Comptable",
                "requestDate": p.updated_at.isoformat(),
                "amount": float(p.amount),
                "status": p.status or "En attente",
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

    return {
        "summary": summary,
        "syncHealth": get_sync_health(),
        "pendingValidations": validations,
        "recentActivities": recent_activities[:10],
        "presence": {
            "activeEmployees": active_employees,
            "totalEmployees": total_employees,
            "rate": round((active_employees / total_employees) * 100, 1)
            if total_employees
            else 0.0,
        },
    }
