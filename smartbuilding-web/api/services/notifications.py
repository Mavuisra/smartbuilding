from __future__ import annotations

import json
import logging
from typing import Any

from django.db import DatabaseError

from api.models import ExecutiveNotification

logger = logging.getLogger(__name__)


IMPORTANT_PUSH_TYPES = {
    "FinancialTransactions",
    "Incidents",
    "LeaseContracts",
    "RentPayments",
    "Suppliers",
    "ConsumptionRecords",
    "Users",
}


def notify_event(
    *,
    title: str,
    message: str,
    severity: str = ExecutiveNotification.Severity.INFO,
    source: str = "",
    action_type: str = "",
    entity_type: str = "",
    entity_count: int = 0,
    created_by: str = "",
) -> ExecutiveNotification:
    try:
        return ExecutiveNotification.objects.create(
            title=title[:200],
            message=message,
            severity=severity,
            source=source,
            action_type=action_type,
            entity_type=entity_type,
            entity_count=max(entity_count, 0),
            created_by=created_by or "",
        )
    except DatabaseError as ex:
        # Ne jamais casser les flux métier (login/sync) si la table de notif
        # n'est pas encore migrée ou indisponible temporairement.
        logger.warning("Impossible d'enregistrer une notification: %s", ex)
        return ExecutiveNotification(
            title=title[:200],
            message=message,
            severity=severity,
            source=source,
            action_type=action_type,
            entity_type=entity_type,
            entity_count=max(entity_count, 0),
            created_by=created_by or "",
        )


def maybe_notify_sync_push(
    *,
    entity_type: str,
    records_count: int,
    username: str,
    success: bool,
    error_message: str = "",
) -> None:
    if success:
        if entity_type not in IMPORTANT_PUSH_TYPES or records_count <= 0:
            return
        notify_event(
            title="Synchronisation importante reçue",
            message=f"{records_count} enregistrement(s) {entity_type} synchronisé(s) par {username}.",
            severity=ExecutiveNotification.Severity.SUCCESS,
            source="Desktop Sync",
            action_type="sync_push",
            entity_type=entity_type,
            entity_count=records_count,
            created_by=username,
        )
        return

    notify_event(
        title="Échec de synchronisation",
        message=f"Échec push {entity_type} par {username}: {error_message or 'erreur inconnue'}",
        severity=ExecutiveNotification.Severity.ERROR,
        source="Desktop Sync",
        action_type="sync_push_error",
        entity_type=entity_type,
        entity_count=records_count,
        created_by=username,
    )


def notify_login_failure(username: str) -> None:
    notify_event(
        title="Échec d'authentification API",
        message=f"Tentative de connexion invalide pour l'utilisateur '{username or 'inconnu'}'.",
        severity=ExecutiveNotification.Severity.WARNING,
        source="API Auth",
        action_type="login_failed",
        entity_type="Users",
        entity_count=1,
        created_by=username or "unknown",
    )


def notify_granular_events_from_push(
    *,
    entity_type: str,
    entities: list[dict[str, Any]],
    username: str,
) -> None:
    for payload in entities:
        data = _read_json_data(payload)
        if not data:
            continue

        if entity_type == "FinancialTransactions":
            _notify_finance_transaction(data, username)
        elif entity_type == "ConsumptionRecords":
            _notify_consumption(data, username)
        elif entity_type == "Incidents":
            _notify_incident(data, username)


def _read_json_data(payload: dict[str, Any]) -> dict[str, Any]:
    json_raw = payload.get("jsonData") or payload.get("JsonData") or {}
    if isinstance(json_raw, str):
        try:
            parsed = json.loads(json_raw)
            return parsed if isinstance(parsed, dict) else {}
        except json.JSONDecodeError:
            return {}
    return json_raw if isinstance(json_raw, dict) else {}


def _notify_finance_transaction(data: dict[str, Any], username: str) -> None:
    tx_type = str(data.get("Type") or data.get("type") or "")
    if tx_type not in {"2", "Depense", "Dépense"}:
        return

    description = str(data.get("Description") or data.get("description") or "")
    category = str(data.get("Category") or data.get("category") or "—")
    status = str(data.get("Status") or data.get("status") or "")
    reference = str(data.get("Reference") or data.get("reference") or "—")

    if "facture fournisseur" not in description.lower():
        return

    is_pending = "attente" in status.lower() or "impay" in status.lower()
    notify_event(
        title="Nouvelle facture fournisseur",
        message=f"{reference} · {category} · statut: {status or '—'}.",
        severity=ExecutiveNotification.Severity.WARNING
        if is_pending
        else ExecutiveNotification.Severity.INFO,
        source="Desktop Sync",
        action_type="supplier_invoice_created",
        entity_type="FinancialTransactions",
        entity_count=1,
        created_by=username,
    )


def _notify_consumption(data: dict[str, Any], username: str) -> None:
    status = str(data.get("Status") or data.get("status") or "")
    is_anomaly = bool(data.get("IsAnomaly") or data.get("isAnomaly"))
    variation = float(data.get("VariationPercent") or data.get("variationPercent") or 0)
    equipment = str(data.get("EquipmentSource") or data.get("equipmentSource") or "Équipement inconnu")
    type_label = str(data.get("Type") or data.get("type") or "Consommation")

    is_critical = "critique" in status.lower() or is_anomaly or variation >= 25
    is_warning = "élevé" in status.lower() or "eleve" in status.lower() or variation >= 15
    if not (is_critical or is_warning):
        return

    severity = (
        ExecutiveNotification.Severity.ERROR
        if is_critical
        else ExecutiveNotification.Severity.WARNING
    )
    notify_event(
        title="Consommation anormale détectée",
        message=f"{type_label} · {equipment} · variation {variation:+.1f}% · statut {status or '—'}.",
        severity=severity,
        source="Desktop Sync",
        action_type="consumption_alert",
        entity_type="ConsumptionRecords",
        entity_count=1,
        created_by=username,
    )


def _notify_incident(data: dict[str, Any], username: str) -> None:
    title = str(data.get("Title") or data.get("title") or "Incident")
    severity_txt = str(data.get("Severity") or data.get("severity") or "")
    status = str(data.get("Status") or data.get("status") or "")
    code = str(data.get("Code") or data.get("code") or "—")

    sev_lower = severity_txt.lower()
    is_critical = "critique" in sev_lower or "haute" in sev_lower or "high" in sev_lower
    is_open = not any(x in status.lower() for x in ["clos", "clôt", "résolu", "resolu"])

    if not (is_critical or is_open):
        return

    notify_event(
        title="Incident critique / ouvert",
        message=f"{code} · {title} · sévérité {severity_txt or '—'} · statut {status or '—'}.",
        severity=ExecutiveNotification.Severity.ERROR
        if is_critical
        else ExecutiveNotification.Severity.WARNING,
        source="Desktop Sync",
        action_type="incident_alert",
        entity_type="Incidents",
        entity_count=1,
        created_by=username,
    )
