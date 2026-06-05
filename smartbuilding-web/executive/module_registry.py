"""
Registre des modules — aligné sur SmartBuilding.Desktop.WPF.Services.ModuleRegistry.
"""

from __future__ import annotations

from dataclasses import dataclass, field

# Portail web — onglets alignés desktop WPF (+ tableau de bord)
WEB_PORTAL_MODULES: frozenset[str] = frozenset({
    "dashboard",
    "rapports",
    "documents",
    "utilisateurs",
    "parametres",
    "synchronisation",
    "journal",
})

WEB_PORTAL_ORDER: tuple[str, ...] = (
    "dashboard",
    "rapports",
    "documents",
    "utilisateurs",
    "parametres",
    "synchronisation",
    "journal",
)


@dataclass(frozen=True)
class ModuleChild:
    slug: str
    label: str


@dataclass(frozen=True)
class WebModule:
    id: str
    title: str
    subtitle: str
    icon: str
    section: str  # main | gestion | admin | supervision
    permission: str | None = None
    children: tuple[ModuleChild, ...] = field(default_factory=tuple)
    desktop_only: bool = False  # pas d'écran desktop dédié (supervision PDG)


# Ordre et libellés identiques au menu desktop WPF
ALL_MODULES: tuple[WebModule, ...] = (
    WebModule(
        "dashboard",
        "Tableau de bord",
        "Vue d'ensemble",
        "ViewDashboard",
        "main",
        "dashboard.view",
    ),
    WebModule(
        "locations",
        "Location",
        "Locataires, locaux et contrats",
        "HomeCity",
        "gestion",
        "location.manage",
        children=(
            ModuleChild("locations-create", "Créer"),
            ModuleChild("locations-list", "Voir"),
            ModuleChild("locations-rent-pay", "Paiement loyer"),
            ModuleChild("locations-tenants", "Locateur"),
            ModuleChild("locations-landlord", "Bailleur"),
            ModuleChild("locations-building", "Bâtiment"),
            ModuleChild("locations-apartments", "Appartements"),
            ModuleChild("locations-gestion", "Gestion"),
        ),
    ),
    WebModule(
        "personnel",
        "Personnel",
        "Employés, présences et salaires",
        "AccountGroup",
        "gestion",
        "personnel.view",
    ),
    WebModule(
        "finances",
        "Finances",
        "Recettes, dépenses et trésorerie",
        "CashMultiple",
        "gestion",
        "finance.view",
    ),
    WebModule(
        "technique",
        "Technique & Sécurité",
        "Équipements, maintenance et incidents",
        "HammerWrench",
        "gestion",
        "technical.manage",
        children=(ModuleChild("incidents", "Incidents"),),
    ),
    WebModule(
        "fournisseurs",
        "Fournisseurs",
        "Partenaires et contrats fournisseurs",
        "TruckDelivery",
        "gestion",
        "suppliers.manage",
    ),
    WebModule(
        "consommations",
        "Consommations",
        "Énergie, eau et coûts",
        "LightningBolt",
        "gestion",
        "consumption.manage",
    ),
    WebModule(
        "visites",
        "Visites & Accès",
        "Visiteurs, accès et réception",
        "BadgeAccount",
        "gestion",
        "visitors.manage",
    ),
    WebModule(
        "emails",
        "Emails & Communication",
        "Boîte mail intégrée Gmail/Outlook",
        "EmailOutline",
        "gestion",
        "email.manage",
    ),
    WebModule(
        "documents",
        "Documents",
        "Fichiers et pièces jointes liés",
        "FileDocumentOutline",
        "admin",
        "dashboard.view",
    ),
    WebModule(
        "utilisateurs",
        "Utilisateurs",
        "Comptes et rôles",
        "AccountKey",
        "admin",
        "users.manage",
    ),
    WebModule(
        "parametres",
        "Paramètres",
        "Configuration du bâtiment",
        "Cog",
        "admin",
        "users.manage",
    ),
    WebModule(
        "synchronisation",
        "Synchronisation",
        "État cloud et conflits",
        "Sync",
        "admin",
        "sync.manage",
    ),
    WebModule(
        "journal",
        "Journal d'activité",
        "Logs système et synchronisation",
        "History",
        "admin",
        "sync.manage",
    ),
    # Modules portail exécutif (complément supervision — non présents comme entrée racine desktop)
    WebModule(
        "supervision",
        "Supervision",
        "Vue globale des opérations et signaux critiques",
        "ChartLine",
        "supervision",
        "dashboard.view",
        desktop_only=True,
    ),
    WebModule(
        "validations",
        "Validations",
        "Approbation des dépenses et demandes sensibles",
        "CheckCircle",
        "supervision",
        "finance.manage",
        desktop_only=True,
    ),
    WebModule(
        "rapports",
        "Rapports",
        "Indicateurs, exports et synthèses",
        "FileChart",
        "admin",
        "reports.export",
    ),
    WebModule(
        "audit-securite",
        "Audit & Sécurité",
        "Contrôles, accès et événements de sécurité",
        "Shield",
        "supervision",
        "users.manage",
        desktop_only=True,
    ),
)

_BY_ID = {m.id: m for m in ALL_MODULES}
# Alias historiques web
_ALIASES = {
    "finance": "finances",
    "contrats": "locations-list",
    "presence": "personnel",
    "maintenance": "technique",
    "activites-logs": "journal",
}


def resolve_slug(slug: str) -> str:
    return _ALIASES.get(slug, slug)


def get_module(slug: str) -> WebModule | None:
    slug = resolve_slug(slug)
    if slug in _BY_ID:
        return _BY_ID[slug]
    for mod in ALL_MODULES:
        if any(c.slug == slug for c in mod.children):
            return mod
    return None


def permission_for_slug(slug: str) -> str | None:
    """Permission requise pour un slug (module racine ou sous-menu Location)."""
    slug = resolve_slug(slug)
    mod = _BY_ID.get(slug)
    if mod:
        return mod.permission
    if slug == "incidents":
        return "incidents.manage"
    if slug.startswith("locations-"):
        return "location.manage"
    return None


def can_access_module(role: str, slug: str) -> bool:
    from api.permission_codes import role_has_permission

    slug = resolve_slug(slug)
    if slug == "incidents":
        return role_has_permission(role, "incidents.manage") or role_has_permission(
            role, "technical.manage"
        )
    if slug == "technique":
        return role_has_permission(role, "technical.manage") or role_has_permission(
            role, "incidents.manage"
        )
    if slug.startswith("locations-"):
        return role_has_permission(role, "location.manage")
    code = permission_for_slug(slug)
    return role_has_permission(role, code)


def is_web_portal_module(slug: str) -> bool:
    return resolve_slug(slug) in WEB_PORTAL_MODULES


def default_web_module_for_role(role: str) -> str:
    for slug in WEB_PORTAL_ORDER:
        if can_access_module(role, slug):
            return slug
    return "rapports"


_CHILD_LABELS = {c.slug: c.label for m in ALL_MODULES for c in m.children}


def module_meta(slug: str) -> dict:
    raw_slug = slug
    slug = resolve_slug(slug)
    mod = _BY_ID.get(slug)
    child_label = _CHILD_LABELS.get(raw_slug) or _CHILD_LABELS.get(slug)
    group_map = {
        "main": "Principal",
        "gestion": "Gestion",
        "admin": "Administration",
        "supervision": "Supervision",
    }
    if mod is None and child_label:
        parent = get_module(raw_slug)
        if parent:
            return {
                "id": raw_slug,
                "title": child_label,
                "subtitle": parent.subtitle,
                "group": group_map.get(parent.section, "Gestion"),
                "parent_title": parent.title,
                "is_validation": False,
            }
        return {
            "title": child_label,
            "subtitle": "Page SBMS",
            "group": "Gestion",
            "id": raw_slug,
        }
    if mod is None:
        return {
            "title": "Module",
            "subtitle": "Page SBMS",
            "group": "Gestion",
            "id": slug,
        }
    title = child_label if child_label and mod.id != slug else mod.title
    return {
        "id": mod.id if not child_label else raw_slug,
        "title": title,
        "subtitle": mod.subtitle,
        "group": group_map.get(mod.section, "Gestion"),
        "parent_title": mod.title if child_label else None,
        "is_validation": mod.id == "validations",
    }


def build_navigation(role: str) -> list[dict]:
    nav: list[dict] = [{"type": "header", "label": "ADMINISTRATION"}]

    for mod_id in WEB_PORTAL_ORDER:
        mod = _BY_ID.get(mod_id)
        if mod is None or not can_access_module(role, mod_id):
            continue
        nav.append(
            {
                "type": "module",
                "slug": mod.id,
                "label": mod.title,
                "children": [],
            }
        )
    return nav
