# SBMS Cloud — PHP (Clean Architecture)

API cloud **Bloom Prosperity / SmartBuilding** pour hébergement **LWS mutualisé** (PHP 8.1+ + MySQL).

Remplace entièrement l'ancienne stack Django/Render.

## Architecture

```
sbms-cloud/
├── public/              → Point d'entrée web (index.php)
├── bootstrap/           → Composition root (DI, routes Slim)
├── src/
│   ├── Domain/          → Entités & règles métier pures
│   ├── Application/     → Cas d'usage (Login, Sync…)
│   ├── Infrastructure/  → PDO, JWT, matérialiseurs sync
│   └── Presentation/    → Controllers HTTP + portail Twig
├── templates/executive/ → Portail PDG (Twig)
├── migrations/          → Schéma MySQL
└── scripts/seed.php     → Comptes admin / PDG
```

## Déploiement LWS

1. Créer une base MySQL dans le panel LWS
2. Importer `migrations/001_initial_schema.sql`
3. Uploader le dossier `sbms-cloud/` (document root → `public/`)
4. Copier `.env.example` → `.env` et configurer DB + JWT
5. `composer install --no-dev --optimize-autoloader`
6. `php scripts/seed.php`

## Comptes par défaut

| Utilisateur | Mot de passe | Rôle |
|-------------|--------------|------|
| admin       | Admin@2026   | Administrateur |
| pdg         | Pdg@2026     | PDG |

## API (contrat identique au desktop WPF)

| Méthode | Endpoint | Description |
|---------|----------|-------------|
| GET | `/health/` | Santé service |
| POST | `/api/auth/login/` | JWT (8h, HS256) |
| GET | `/api/auth/session/` | Session |
| POST | `/api/sync/push/` | Push sync |
| GET | `/api/sync/pull/` | Pull sync (**sans enveloppe**) |
| GET | `/api/sync/status/` | Types syncables |
| POST | `/api/sync/documents/upload/` | Upload PDF |

## Desktop WPF

Mettre à jour `Api.BaseUrl` dans `appsettings.json` :

```json
"Api": {
  "BaseUrl": "https://sbms.lasaveur.store/"
}
```

## Portail PDG

- `/login/` — Connexion
- `/dashboard/` — Tableau de bord
- Modules : rapports, documents, utilisateurs, paramètres, synchronisation, journal

## Développement local

```bash
cd sbms-cloud
composer install
cp .env.example .env
php -S localhost:8080 -t public
```
