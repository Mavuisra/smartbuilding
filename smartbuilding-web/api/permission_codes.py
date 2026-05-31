"""
Aligné sur SmartBuilding.Shared.Constants.PermissionCodes (desktop).
"""

from __future__ import annotations

ROLE_PERMISSIONS: dict[str, list[str]] = {
    "Administrateur": ["*"],
    "PDG": ["*"],
    "Comptable": [
        "dashboard.view",
        "finance.manage",
        "finance.view",
        "location.manage",
        "suppliers.manage",
        "reports.export",
        "personnel.view",
    ],
    "Technique": [
        "dashboard.view",
        "technical.manage",
        "incidents.manage",
        "consumption.manage",
        "inventory.manage",
        "personnel.view",
    ],
    "Gestionnaire": [
        "dashboard.view",
        "location.manage",
        "visitors.manage",
        "incidents.manage",
        "personnel.manage",
        "consumption.manage",
        "email.manage",
        "reports.export",
    ],
}

ALL_PERMISSION_CODES = [
    "dashboard.view",
    "personnel.manage",
    "personnel.view",
    "technical.manage",
    "location.manage",
    "finance.manage",
    "finance.view",
    "suppliers.manage",
    "incidents.manage",
    "consumption.manage",
    "visitors.manage",
    "inventory.manage",
    "email.manage",
    "users.manage",
    "sync.manage",
    "reports.export",
]


def permissions_for_role(role: str) -> list[str]:
    perms = ROLE_PERMISSIONS.get(role, [])
    if "*" in perms:
        return list(ALL_PERMISSION_CODES)
    return list(perms)


def role_has_permission(role: str, code: str | None) -> bool:
    if not code:
        return True
    perms = ROLE_PERMISSIONS.get(role, [])
    if "*" in perms:
        return True
    return code in perms
