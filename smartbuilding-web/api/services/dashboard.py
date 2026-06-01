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
from api.services.finance_ledger import dedupe_financial_transactions
from api.services.sync_metrics import (
    calendar_month_starts,
    count_active_leases,
    count_tenants_total,
    ensure_dashboard_orm_materialized,
    expenses_all_time_totals,
    expenses_month_totals,
    occupancy_totals,
    rent_all_time_collected,
    rent_month_totals,
    revenue_chart_totals,
    sync_store_count,
)


def _fc(amount) -> str:
    return f"$ {amount:,.2f}"


def get_executive_summary() -> dict:
    ensure_dashboard_orm_materialized()

    today = timezone.localdate()
    month_start = today.replace(day=1)

    rent_collected, rent_planned, late_count, rent_late_amount = rent_month_totals(
        today.year, today.month
    )
    rent_collected_total = rent_all_time_collected()

    expenses_month = expenses_month_totals(month_start)
    expenses_total = expenses_all_time_totals()
    available_balance = rent_collected_total - expenses_total
    available_this_month = rent_collected - expenses_month

    total_premises, occupied = occupancy_totals()
    occupancy = (occupied / total_premises * 100) if total_premises else 0

    closed_statuses = {"Clôturé", "Cloture", "Résolu", "Resolu", "4", "3"}
    open_incidents = (
        Incident.objects.filter(deleted_at__isnull=True)
        .exclude(status__in=closed_statuses)
        .count()
    )

    active_leases = count_active_leases()
    total_tenants = count_tenants_total()

    recent_syncs = list(
        ServerSyncEvent.objects.filter(success=True)
        .order_by("-created_at")[:10]
        .values("username", "user_role", "entity_type", "records_count", "created_at")
    )

    month_starts = calendar_month_starts(today)
    revenue_chart = revenue_chart_totals(month_starts)

    recent_movements = _resolve_recent_movements(8)

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
        "monthlyRevenue": float(rent_collected_total),
        "rentRevenue": float(rent_collected_total),
        "rentCollectedTotal": float(rent_collected_total),
        "monthlyExpenses": float(expenses_month),
        "expensesThisMonth": float(expenses_month),
        "totalExpenses": float(expenses_total),
        "availableBalance": float(available_balance),
        "availableThisMonth": float(available_this_month),
        "netBalance": float(available_balance),
        "netBalanceThisMonth": float(available_this_month),
        "treasuryBalance": float(available_balance),
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
            {"label": "Loyers encaissés (total)", "value": _fc(rent_collected_total)},
            {"label": "Loyers du mois", "value": _fc(rent_collected)},
            {"label": "Dépenses engagées (total)", "value": _fc(expenses_total)},
            {"label": "Dépenses (mois)", "value": _fc(expenses_month)},
            {"label": "Trésorerie disponible", "value": _fc(available_balance)},
        ],
        "dataSources": {
            "rent": "RentPayments",
            "expenses": "FinancialTransactions",
            "occupancy": "Premises",
        },
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


def _resolve_month_rent(year: int, month: int) -> tuple[Decimal, Decimal, int, Decimal]:
    """Priorité : magasin sync Desktop, puis ORM, puis écritures loyer (FinancialTransactions)."""
    if sync_store_count("RentPayments") > 0:
        sync_vals = _rent_from_sync_store(year, month)
        if sync_vals[0] > 0 or sync_vals[1] > 0:
            return sync_vals
        orm_vals = rent_from_orm(year, month, synced_only=True)
        if orm_vals[0] > 0 or orm_vals[1] > 0:
            return orm_vals
        return sync_vals

    orm_vals = rent_from_orm(year, month, synced_only=False)
    if orm_vals[0] > 0 or orm_vals[1] > 0:
        return orm_vals

    ledger = _rent_from_ledger_sync_store(year, month)
    if ledger[0] > 0 or ledger[1] > 0:
        return ledger

    return _rent_from_sync_store(year, month)


def _resolve_expenses_month(month_start) -> Decimal:
    if sync_store_count("FinancialTransactions") > 0:
        sync_val = _expenses_from_sync_store(month_start)
        if sync_val > 0:
            return sync_val
        orm_val = expenses_from_orm(month_start, synced_only=True)
        return orm_val if orm_val > 0 else sync_val

    orm_val = expenses_from_orm(month_start, synced_only=False)
    if orm_val > 0:
        return orm_val
    return _expenses_from_sync_store(month_start)


def _resolve_occupancy() -> tuple[int, int]:
    if sync_store_count("Premises") > 0:
        sync_vals = _occupancy_from_sync_store()
        if sync_vals[0] > 0:
            return sync_vals
        orm_vals = occupancy_from_orm(synced_only=True)
        return orm_vals if orm_vals[0] > 0 else sync_vals

    orm_vals = occupancy_from_orm(synced_only=False)
    if orm_vals[0] > 0:
        return orm_vals
    return _occupancy_from_sync_store()


def _resolve_revenue_chart(month_starts: list) -> list[dict]:
    if sync_store_count("RentPayments") > 0:
        return [
            {
                "label": ms.strftime("%b %Y"),
                "value": float(_rent_from_sync_store(ms.year, ms.month)[0]),
            }
            for ms in month_starts
        ]

    chart = revenue_chart_from_orm(month_starts, synced_only=False)
    if any(p["value"] > 0 for p in chart):
        return chart

    return [
        {
            "label": ms.strftime("%b %Y"),
            "value": float(
                _rent_from_sync_store(ms.year, ms.month)[0]
                or _rent_from_ledger_sync_store(ms.year, ms.month)[0]
            ),
        }
        for ms in month_starts
    ]


def _resolve_recent_movements(limit: int) -> list[dict]:
    if sync_store_count("FinancialTransactions") > 0:
        sync_rows = _recent_movements_from_sync_store(limit)
        if sync_rows:
            return sync_rows
    base = FinancialTransaction.objects.filter(deleted_at__isnull=True).order_by(
        "-transaction_date"
    )[: limit * 3]
    deduped = dedupe_financial_transactions(list(base))[:limit]
    if deduped:
        return [
            {
                "transaction_date": t.transaction_date,
                "type": t.type,
                "category": t.category,
                "description": t.description,
                "amount": t.amount,
                "reference": t.reference,
            }
            for t in deduped
        ]
    return _recent_movements_from_sync_store(limit)


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


def _rent_from_ledger_sync_store(year: int, month: int) -> tuple[Decimal, Decimal, int, Decimal]:
    """Recettes « Loyer » dans FinancialTransactions (si RentPayments absents du sync)."""
    collected = Decimal("0")
    for row in SyncedEntityStore.objects.filter(
        entity_type="FinancialTransactions", deleted_at__isnull=True
    ).iterator():
        payload = row.json_data if isinstance(row.json_data, dict) else {}
        raw_type = _pick_json(payload, "Type", "type", default=1)
        is_income = raw_type in (1, "1", "Recette") or (
            isinstance(raw_type, str) and "rec" in str(raw_type).lower()
        )
        if not is_income:
            continue
        category = str(_pick_json(payload, "Category", "category", default="")).lower()
        if "loyer" not in category and "rent" not in category:
            continue
        dt = parse_datetime_safe(_pick_json(payload, "TransactionDate", "transactionDate"))
        if not dt or dt.year != year or dt.month != month:
            continue
        collected += Decimal(str(_pick_json(payload, "Amount", "amount", default=0) or 0))
    return collected, collected, 0, Decimal("0")


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
