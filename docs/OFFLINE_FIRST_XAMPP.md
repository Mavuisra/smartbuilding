# Architecture Offline First — SBMS (sans PHP)

## Principe

| Couche | Technologie | Rôle |
|--------|-------------|------|
| **Desktop WPF** | **MySQL (XAMPP)** ou **SQLite** (secours) | Travail **100 % local**, sans Internet |
| **Traitement** | **C# / .NET 8** (EF Core, services) | Aucun PHP : XAMPP = **MySQL uniquement** |
| **Cloud** | **PostgreSQL** + API Django | Hub partagé entre postes |

Apache fourni par XAMPP **n’est pas utilisé** par SBMS.

## Configuration (`appsettings.json`)

```json
"LocalDatabase": {
  "Provider": "Auto",
  "MySql": "Server=127.0.0.1;Port=3306;Database=sbms_local;User=root;Password=;CharSet=utf8mb4;",
  "AutoFallbackToSqlite": true
}
```

| Provider | Comportement |
|----------|----------------|
| `Auto` | MySQL si XAMPP/MySQL démarré, sinon SQLite |
| `MySql` | MySQL obligatoire (erreur claire si arrêté) |
| `Sqlite` | Fichier `%LocalAppData%\SBMS\data\smartbuilding.db` |

Au premier démarrage MySQL, la base `sbms_local` est créée automatiquement.

## Synchronisation (offline first)

1. **Saisie locale** → `IsSynced = false` (`MarkUpdated()` sur chaque entité).
2. **Sans Internet** → tout reste en MySQL/SQLite local.
3. **Avec Internet** → sync auto (60 s) ou manuelle (module Synchronisation) :
   - **Phase 1 — Push** : envoi des enregistrements `IsSynced = false` vers PostgreSQL via l’API.
   - **Phase 2 — Pull** : récupération des changements des **autres postes** depuis le cloud.
4. **Conflits** : stratégie *Last write wins* (`UpdatedAt` le plus récent).
5. **Suppressions** : `DeletedAt` (soft delete) synchronisé.
6. **Retry** : backoff exponentiel si échec réseau (`SyncRetryPolicy`).
7. **Poste** : identifiant stable `%LocalAppData%\SBMS\device-id.txt` (journal sync).

## Multi-postes

Chaque PC a sa base locale. Le **cloud PostgreSQL** est le point de convergence :

```
Poste A (MySQL local) ──push/pull──┐
Poste B (MySQL local) ──push/pull──┼── API ── PostgreSQL
Poste C (SQLite)      ──push/pull──┘
```

## Démarrage XAMPP

1. Ouvrir **XAMPP Control Panel**.
2. Démarrer **MySQL** (Apache optionnel, non requis pour SBMS).
3. Lancer **SBMS Desktop**.

## Comptes administrateur locaux

| Utilisateur | Mot de passe |
|-------------|----------------|
| admin | Admin@2026 |
| admin2 | Admin@2026 |

## Fichiers clés

- `DesktopLocalDatabaseBootstrap.cs` — choix MySQL / SQLite
- `SyncService.cs` — push puis pull
- `EntitySyncAdapter.cs` — `IsSynced`, soft delete
- `DesktopSyncDevice.cs` — ID poste
- `materializers.py` — alignement PostgreSQL côté web

## Migrations EF Core (MySQL)

Au démarrage, SBMS appelle **`Database.Migrate()`** (via `DesktopDatabaseInitializer`) : toutes les migrations en attente sont appliquées sur `sbms_local`.

**Nouvelle migration** (après modification du modèle) :

```bash
dotnet ef migrations add NomDeLaMigration ^
  --project SmartBuilding.Infrastructure ^
  --startup-project SmartBuilding.Desktop.WPF ^
  --context SmartBuildingDbContext ^
  --output-dir Migrations/MySql
```

Fichiers : `SmartBuilding.Infrastructure/Migrations/MySql/`.

Si la base MySQL a été créée **avant** les migrations (ancien `EnsureCreated`) et que `Migrate()` échoue (tables déjà existantes) : supprimez la base `sbms_local` dans phpMyAdmin puis relancez SBMS, ou exportez vos données puis recréez la base.

## Passage SQLite → MySQL

1. Synchroniser tout vers le cloud (module Synchronisation).
2. Passer `LocalDatabase:Provider` à `MySql`.
3. Démarrer **MySQL** dans XAMPP, relancer SBMS (migrations appliquées automatiquement).
4. Resynchroniser depuis le cloud pour repeupler la base locale MySQL.
