#!/usr/bin/env bash
set -euo pipefail

pip install -r requirements.txt

if [ -z "${DATABASE_URL:-}" ]; then
  echo "ERREUR: DATABASE_URL non définie. Liez PostgreSQL Render dans Environment."
  exit 1
fi

# Migrations uniquement — ne jamais flush/recréer la base en déploiement.
python manage.py migrate --noinput

# Super admin cloud (Jessica) — toujours garanti après chaque déploiement.
python manage.py seed_smartbuilding

python manage.py collectstatic --noinput
