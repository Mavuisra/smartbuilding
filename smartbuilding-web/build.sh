#!/usr/bin/env bash
set -euo pipefail

pip install -r requirements.txt

# Migrations uniquement — ne jamais flush/recréer la base en déploiement.
python manage.py migrate --noinput

# Comptes par défaut : uniquement si explicitement demandé (premier déploiement).
if [ "${SBMS_RUN_SEED:-false}" = "true" ]; then
  python manage.py seed_smartbuilding
fi

python manage.py collectstatic --noinput
