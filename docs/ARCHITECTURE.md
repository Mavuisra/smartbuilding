# Smart Building (SB) — Architecture technique

## Vue d'ensemble

```
┌─────────────────────────────────────────────────────────────┐
│              SmartBuilding.Desktop.WPF (MVVM)               │
│         Material Design · LiveCharts · Host DI              │
└──────────────────────────┬──────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────┐
│           SmartBuilding.Application (Use Cases)               │
│    IAuthService · ISyncService · IDashboardService · ...      │
└──────────────────────────┬──────────────────────────────────┘
                           │
┌──────────────────────────▼──────────────────────────────────┐
│         SmartBuilding.Infrastructure (Adapters)               │
│   EF Core · MailKit · QuestPDF · EPPlus · HttpClient          │
└──────────────┬─────────────────────────────┬────────────────┘
               │                             │
     ┌─────────▼─────────┐         ┌─────────▼─────────┐
     │  SQLite (local)   │◄─Sync──►│ PostgreSQL (cloud)│
     │  Offline-first    │  60s    │  via REST API     │
     └───────────────────┘         └───────────────────┘
```

## Couches Clean Architecture

| Projet | Responsabilité |
|--------|----------------|
| **Domain** | Entités, enums, interfaces `IRepository`, `IUnitOfWork` |
| **Shared** | DTOs API/sync, constantes permissions |
| **Application** | Contrats de services métier |
| **Infrastructure** | EF Core, auth JWT, sync, email IMAP/SMTP, rapports |
| **API** | ASP.NET Core REST, sécurité Bearer |
| **Desktop.WPF** | UI MVVM, timer sync background |

## Modèle de données

Toutes les entités héritent de `BaseEntity` :

- `Id` : **GUID**
- `CreatedAt`, `UpdatedAt`
- `IsSynced` : indicateur offline
- `DeletedAt` : soft delete

### Modules et tables

| Module | Tables |
|--------|--------|
| Auth | Users, Permissions, UserPermissions |
| Personnel | Employees, Attendances, SalaryPayments |
| Technique | Equipment, MaintenanceRecords, RepairRecords, TechnicalAlerts |
| Location | Premises, Tenants, LeaseContracts, RentPayments |
| Finance | FinancialTransactions |
| Fournisseurs | Suppliers, SupplierContracts, SupplierPayments |
| Incidents | Incidents |
| Consommations | ConsumptionRecords |
| Visiteurs | Visitors |
| Inventaire | InventoryItems |
| Email | EmailAccounts, CachedEmails |
| Système | SyncLogs, SystemLogs, BuildingInfos |

## Synchronisation offline-first

1. Toute modification locale met `IsSynced = false`
2. Timer WPF : **60 secondes** (`SyncBackgroundService`)
3. Push : entités non synchronisées → `POST /api/sync/push`
4. Pull : changements serveur depuis `LastSyncAt` → `GET /api/sync/pull`
5. Conflits : **Last Write Wins** (`UpdatedAt` le plus récent gagne)
6. Journal : table `SyncLogs`

## Sécurité

- Mots de passe : **BCrypt**
- API : **JWT Bearer** (HS256)
- Rôles : Administrateur, Comptable, Technique, Gestionnaire
- Permissions granulaires via `PermissionCodes`

## Compte par défaut

- Utilisateur : `admin`
- Mot de passe : `Admin@2026`

## Limites

- Maximum **4 utilisateurs** simultanés (à appliquer côté licence/API en production)
