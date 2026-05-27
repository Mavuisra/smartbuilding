# Plan de développement — Smart Building (SB)

## Phase 1 — Fondations ✅ (courant)

- [x] Solution .NET 8 multi-projets
- [x] Entités domaine + audit sync
- [x] DbContext EF Core (SQLite / PostgreSQL)
- [x] Auth JWT + BCrypt + seed admin
- [x] API REST (auth, sync, dashboard, rapports)
- [x] Shell WPF Material Design + login + dashboard
- [x] Moteur sync (structure + LWW)
- [x] Services email, PDF, Excel (base)

## Phase 2 — Modules CRUD (2-3 semaines)

1. **Personnel** : vues liste/formulaire employés, présences, salaires
2. **Technique** : équipements, maintenance, alertes
3. **Location** : locaux, locataires, contrats, loyers, retards
4. **Finance** : recettes/dépenses, filtres, export
5. **Fournisseurs** : CRUD + paiements
6. **Incidents** : workflow gravité → résolution
7. **Consommations** : saisie par type (eau, élec, etc.)
8. **Visiteurs** : check-in/out
9. **Inventaire** : stock et emplacements

## Phase 3 — Intégrations (1-2 semaines)

- UI module **Emails** (liste, lecture, réponse, pièces jointes)
- Notifications nouveaux emails
- Chiffrement mots de passe comptes email (DPAPI Windows)
- Sync complète pour **toutes** les entités (actuellement partielle)

## Phase 4 — Production (1 semaine)

- Migrations EF versionnées (PostgreSQL + SQLite)
- Sauvegarde auto SQLite planifiée
- Logs Serilog desktop
- Tests unitaires services sync/auth
- Déploiement API (Docker + PostgreSQL)
- Installeur MSI/ClickOnce WPF
- Limite 4 utilisateurs (middleware API)

## Phase 5 — Qualité & rapports

- Rapports par module (PDF/Excel)
- Tableau de bord enrichi (filtres date, drill-down)
- Tests d'intégration API
- Documentation utilisateur

## Priorités techniques immédiates

1. Étendre `SyncService` à toutes les tables
2. Créer vues WPF par module (pattern `DashboardViewModel`)
3. `PermissionBehavior` WPF pour masquer menus selon rôle
4. Migrations : `dotnet ef migrations add Initial -p Infrastructure -s API`

## Commandes utiles

```bash
# Compiler
dotnet build BuildingManagementSystem/SmartBuilding.sln

# Lancer API
dotnet run --project BuildingManagementSystem/SmartBuilding.API

# Lancer Desktop
dotnet run --project BuildingManagementSystem/SmartBuilding.Desktop.WPF
```
