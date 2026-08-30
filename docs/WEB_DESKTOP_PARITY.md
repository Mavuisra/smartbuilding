# Parité Web ↔ Desktop SBMS

## Principe fondamental (source de vérité)

| Couche | Base de données | Rôle |
|--------|-----------------|------|
| **Desktop WPF** | **MySQL (XAMPP)** — voir `docs/OFFLINE_FIRST_XAMPP.md` | **Maître local** — toute saisie, modification, suppression |
| **Web (LWS)** | **MySQL** (via PHP) | **Réplica / consommateur** — reçoit les données via `POST /api/sync/push`, affiche le PDG |

- Le desktop **fonctionne sans Internet** : login, CRUD, rapports, PDF.
- Le web **ne crée pas** la base métier du desktop ; il **lit** (et valide certaines dépenses) ce que le desktop a poussé.
- La synchronisation est **optionnelle** et **déclenchée depuis le desktop** (module Synchronisation).

## Rôles des applications

| Composant | Rôle |
|-----------|------|
| **SmartBuilding.Desktop.WPF** | Application opérationnelle complète (CRUD, MySQL locale, sync push/pull) |
| **sbms-cloud** | API REST PHP (JWT) + portail de **consultation / supervision** sur données synchronisées |
| **SmartBuilding.API** | API ASP.NET alternative (si déployée) |

Le portail web **ne remplace pas** l’ensemble des écrans WPF MVVM (formulaires multi-étapes, PDF, IMAP, etc.). Il expose les **mêmes modules**, **permissions** et **données reçues par sync**, avec validations PDG pour les dépenses.

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

Définies dans `SmartBuilding.Shared` (desktop) et `sbms-cloud/src/Infrastructure/Security/PermissionService.php` (web).

Le login JWT renvoie désormais `permissions[]` selon le rôle. Le menu est filtré via `GET /api/executive/navigation/`.

## Fonctionnalités encore **desktop-only**

- Création / édition complète (contrats, quittances PDF, structure patrimoine pièce par pièce)
- Boîte mail Gmail (IMAP/SMTP) en temps réel
- Module Inventaire (entrée registre desktop, pas de page racine web)
- Mise à jour automatique exe, thèmes WPF Material Design
- Travail **offline-first** sans connexion

## Fichiers clés (web)

- `sbms-cloud/src/Infrastructure/Services/ModuleRegistry.php` — registre modules
- `sbms-cloud/src/Infrastructure/Services/ModuleHandlerRegistry.php` — données par module
- `sbms-cloud/src/Infrastructure/Security/PermissionService.php` — rôles / permissions
- `sbms-cloud/templates/executive/partials/sidebar.twig` — menu dynamique

## Implémenté (parité navigation & permissions)

- Registre `ModuleRegistry.php` (modules portail PDG)
- Permissions `PermissionService.php` + `GET /api/executive/navigation/`
- Données par module : `ModuleHandlerRegistry.php`
- Contrôle d'accès API (403) selon le rôle
- Menu dynamique côté client (`Sbms.apiFetch`, permissions JWT)
- Redirections URL historiques (`/finance/` → `/finances/`, etc.)

## Prochaines étapes recommandées

1. SPA React/Blazor reprenant les ViewModels desktop pour CRUD web à 100 %
2. Endpoints REST CRUD par entité (au-delà de sync push/pull)
3. Tests d’intégration parité navigation + permissions
