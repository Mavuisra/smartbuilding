# Parité Web ↔ Desktop SBMS

## Rôles des applications

| Composant | Rôle |
|-----------|------|
| **SmartBuilding.Desktop.WPF** | Application opérationnelle complète (CRUD, offline SQLite, sync) |
| **smartbuilding-web** | API REST (sync JWT) + portail web de **consultation / supervision** |
| **SmartBuilding.API** | API ASP.NET alternative (si déployée) |

Le portail web **ne remplace pas** l’ensemble des écrans WPF MVVM (formulaires multi-étapes, PDF, IMAP, etc.). Il expose les **mêmes modules**, **permissions** et **données synchronisées** en lecture, avec validations PDG pour les dépenses.

## Modules alignés (ModuleRegistry)

| ID Desktop | Web | Statut |
|------------|-----|--------|
| `dashboard` | `/` | KPI + graphiques API |
| `locations` + 8 sous-menus | `/locations-*` | Données ORM + sync |
| `personnel` | `/personnel/` | Employés |
| `finances` | `/finances/` | Transactions |
| `technique` | `/technique/` | Équipements |
| `incidents` (onglet technique) | `/incidents/` | Incidents |
| `fournisseurs` | `/fournisseurs/` | Fournisseurs |
| `consommations` | `/consommations/` | Relevés |
| `visites` | `/visites/` | Visiteurs |
| `emails` | `/emails/` | Cache sync (CachedEmails) |
| `documents` | `/documents/` | Contrats / sync |
| `utilisateurs` | `/utilisateurs/` | Comptes |
| `parametres` | `/parametres/` | Config |
| `synchronisation` | `/synchronisation/` | Santé sync |
| `journal` | `/journal/` | Logs sync |

### Modules web supervision (complément PDG)

- `supervision`, `validations`, `rapports`, `audit-securite`

### Alias URL (anciennes routes)

- `/finance/` → `/finances/`
- `/contrats/` → `/locations-list/`
- `/presence/` → `/personnel/`
- `/maintenance/` → `/technique/`
- `/activites-logs/` → `/journal/`

## Permissions (`PermissionCodes`)

Définies dans `SmartBuilding.Shared` (desktop) et `smartbuilding-web/api/permission_codes.py` (web).

Le login JWT renvoie désormais `permissions[]` selon le rôle. Le menu est filtré via `GET /api/executive/navigation/`.

## Fonctionnalités encore **desktop-only**

- Création / édition complète (contrats, quittances PDF, structure patrimoine pièce par pièce)
- Boîte mail Gmail (IMAP/SMTP) en temps réel
- Module Inventaire (entrée registre desktop, pas de page racine web)
- Mise à jour automatique exe, thèmes WPF Material Design
- Travail **offline-first** sans connexion

## Fichiers clés (web)

- `executive/module_registry.py` — registre modules
- `api/module_handlers.py` — données par module
- `api/permission_codes.py` — rôles / permissions
- `executive/templates/executive/partials/sidebar.html` — menu dynamique

## Implémenté (parité navigation & permissions)

- Registre `executive/module_registry.py` (modules + 8 sous-menus Location)
- Permissions `api/permission_codes.py` + `GET /api/executive/navigation/`
- Données par module : `api/module_handlers.py`
- Contrôle d'accès API (403) selon le rôle
- Menu dynamique côté client (`Sbms.apiFetch`, permissions JWT)
- Redirections URL historiques (`/finance/` → `/finances/`, etc.)

## Prochaines étapes recommandées

1. SPA React/Blazor reprenant les ViewModels desktop pour CRUD web à 100 %
2. Endpoints REST CRUD par entité (au-delà de sync push/pull)
3. Tests d’intégration parité navigation + permissions
