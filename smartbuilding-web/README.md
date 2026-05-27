# Smart Building — Portail Web PDG (Django REST)

API et interface web pour que le **PDG** visualise en temps réel les données synchronisées par les **gérants** depuis l’application desktop WPF.

## Architecture

```
Desktop WPF (SQLite, offline)
        │  POST /api/sync/push
        │  GET  /api/sync/pull
        ▼
Django REST API (PostgreSQL ou SQLite)
        │
        ▼
Portail Web PDG (lecture consolidée)
```

## Démarrage rapide

```bash
cd smartbuilding-web
python -m venv .venv
.venv\Scripts\activate          # Windows
pip install -r requirements.txt
copy .env.example .env
python manage.py migrate
python manage.py seed_smartbuilding
python manage.py runserver 8000
```

- **Portail PDG** : http://localhost:8000/
- **API** : http://localhost:8000/api/
- **Health** (desktop) : http://localhost:8000/health/

### Comptes par défaut

| Utilisateur | Mot de passe | Rôle |
|-------------|--------------|------|
| `pdg` | `Pdg@2026` | PDG (portail web) |
| `admin` | `Admin@2026` | Administrateur |

## Configurer le desktop pour synchroniser vers Django

Dans `SmartBuilding.Desktop.WPF/appsettings.json` :

```json
"Api": {
  "BaseUrl": "http://localhost:8000/",
  "Token": ""
}
```

Après connexion desktop, le token JWT est utilisé pour la sync (à brancher côté `SyncService` si besoin).

## Endpoints principaux

| Méthode | URL | Description |
|---------|-----|-------------|
| GET | `/health/` | Santé serveur |
| POST | `/api/auth/login/` | Connexion JWT |
| POST | `/api/sync/push/` | Réception données gérant |
| GET | `/api/sync/pull/` | Envoi changements serveur |
| GET | `/api/dashboard/summary/` | KPI PDG |
| GET | `/api/executive/tenants/` | Liste locataires |
| GET | `/api/executive/incidents/` | Liste incidents |
| GET | `/api/executive/sync-logs/` | Journal sync serveur |

## Production (données persistantes)

**Règle importante** : en ligne, la base doit être **PostgreSQL persistant** (Render Postgres, RDS, etc.).  
Ne jamais utiliser SQLite dans le conteneur de déploiement : il est **recréé à chaque commit/déploiement** et toutes les données sont perdues.

### Render (recommandé)

1. Déployer avec le fichier `render.yaml` à la racine du dépôt (base `smartbuilding-db` + service web).
2. Vérifier que `DATABASE_URL` est bien liée à PostgreSQL (dashboard Render → Environment).
3. Premier déploiement seulement : `SBMS_RUN_SEED=true` pour créer les comptes `admin` / `pdg`, puis remettre à `false`.
4. Les déploiements suivants exécutent uniquement `migrate` — **aucune purge** des données.

### Variables utiles

| Variable | Rôle |
|----------|------|
| `DATABASE_URL` | PostgreSQL en production |
| `DJANGO_DEBUG` | `False` en production |
| `RENDER` / `SBMS_PRODUCTION` | Active la protection anti-SQLite éphémère |
| `SBMS_RUN_SEED` | `true` une seule fois pour les comptes initiaux |

```bash
gunicorn smartbuilding_web.wsgi:application
```
