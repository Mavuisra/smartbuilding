# Architecture Offline First — SBMS (sans PHP)

## Principe

| Couche | Technologie | Rôle |
|--------|-------------|------|
| **Desktop WPF** | **MySQL (XAMPP)** | Travail **100 % local**, sans Internet |
| **Traitement** | **C# / .NET 8** (EF Core, services) | Aucun PHP : XAMPP = **MySQL uniquement** |
| **Cloud** | **PostgreSQL** + API Django | Hub partagé entre postes |

Apache fourni par XAMPP **n’est pas utilisé** par SBMS.

## Configuration (`appsettings.json`)

```json
"LocalDatabase": {
  "Provider": "MySql",
  "MySql": "Server=127.0.0.1;Port=3306;Database=sbms_local;User=root;Password=;CharSet=utf8mb4;"
}
```

| Mode déploiement | Comportement |
|------------------|----------------|
| **Serveur** | MySQL local (`127.0.0.1`) — base unique `sbms_local` sur le PC serveur |
| **Client** | MySQL distant (IP LAN du serveur) — même base partagée en réseau |

Au premier démarrage MySQL, la base `sbms_local` est créée automatiquement (serveur).

## Synchronisation (offline first)

1. **Saisie locale** → `IsSynced = false` (`MarkUpdated()` sur chaque entité).
2. **Sans Internet** → tout reste en MySQL local.
3. **Avec Internet** → sync auto (60 s) ou manuelle (module Synchronisation) :
   - **Phase 1 — Push** : envoi des enregistrements `IsSynced = false` vers PostgreSQL via l’API.
   - **Phase 2 — Pull** : récupération des changements des **autres postes** depuis le cloud.
4. **Conflits** : stratégie *Last write wins* (`UpdatedAt` le plus récent).
5. **Suppressions** : `DeletedAt` (soft delete) synchronisé.
6. **Retry** : backoff exponentiel si échec réseau (`SyncRetryPolicy`).
7. **Poste** : identifiant stable `%LocalAppData%\SBMS\device-id.txt` (journal sync).

## Multi-postes

Chaque PC se connecte à la **même base MySQL** sur le serveur LAN (déploiement base unique) ou possède sa copie locale selon le mode choisi. Le **cloud PostgreSQL** reste le point de convergence inter-sites :

```
Poste serveur (MySQL sbms_local) ──push/pull──┐
Postes clients (MySQL → serveur) ──push/pull──┼── API ── PostgreSQL
```

## Démarrage XAMPP

1. Ouvrir **XAMPP Control Panel**.
2. Démarrer **MySQL** (Apache optionnel, non requis pour SBMS).
3. Lancer **SBMS Desktop** et terminer l’**assistant de configuration** si demandé.

## Comptes administrateur

Créés à l’**étape 1** de l’assistant de configuration (plus de comptes fantômes au démarrage). Les comptes réservés `admin` / `admini` sont assurés à la connexion.

## Fichiers clés

- `DesktopLocalDatabaseBootstrap.cs` — résolution MySQL (serveur / client)
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
