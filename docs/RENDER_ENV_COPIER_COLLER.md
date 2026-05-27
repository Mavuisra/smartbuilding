# Variables à coller sur Render (service WEB smartbuilding-0kbk)

Dashboard → **smartbuilding-0kbk** → **Environment** → ajouter chaque ligne :

| Clé | Valeur |
|-----|--------|
| `DATABASE_URL` | *(Internal Database URL de dimplomate — une seule ligne)* |
| `DJANGO_DEBUG` | `false` |
| `RENDER` | `true` |
| `SBMS_PRODUCTION` | `true` |
| `DATABASE_SSLMODE` | `prefer` |
| `SBMS_RUN_SEED` | `true` *(premier déploiement seulement, puis `false`)* |
| `DJANGO_SECRET_KEY` | *(générer une clé longue aléatoire)* |
| `JWT_SIGNING_KEY` | *(même clé ou autre chaîne ≥ 32 caractères)* |

**Build Command :**

```bash
cd smartbuilding-web && chmod +x build.sh && ./build.sh
```

**Start Command :**

```bash
cd smartbuilding-web && gunicorn smartbuilding_web.wsgi:application --bind 0.0.0.0:$PORT
```

Puis **Save Changes** → **Manual Deploy**.

Le fichier `smartbuilding-web/.env` sur votre PC contient la même config (non envoyé sur Git).
