from datetime import timedelta
from decimal import Decimal

from django.db.models import Sum
from django.utils import timezone

from api.models import (
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
