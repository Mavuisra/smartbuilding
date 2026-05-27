# Smart Building (SB)

Application desktop professionnelle de gestion de bâtiment — **offline-first** avec synchronisation cloud PostgreSQL.

## Stack

- .NET 8 · WPF · ASP.NET Core Web API
- SQLite (local) · PostgreSQL (cloud)
- Entity Framework Core · Material Design · MailKit · QuestPDF · EPPlus
- MVVM · Clean Architecture · Repository · Unit of Work

## Structure

```
BuildingManagementSystem/
├── SmartBuilding.Domain/
├── SmartBuilding.Shared/
├── SmartBuilding.Application/
├── SmartBuilding.Infrastructure/
├── SmartBuilding.API/
├── SmartBuilding.Desktop.WPF/
└── docs/
```

## Démarrage rapide

### Prérequis

- .NET 8 SDK
- PostgreSQL (pour l'API cloud)

### Desktop (mode offline)

```bash
cd BuildingManagementSystem
dotnet run
```

Connexion : `admin` / `Admin@2026`

### API

Configurer `SmartBuilding.API/appsettings.json` (PostgreSQL), puis :

```bash
dotnet run --project SmartBuilding.API
```

Swagger : `https://localhost:7xxx/swagger`

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Plan de développement](docs/PLAN_DEVELOPPEMENT.md)
