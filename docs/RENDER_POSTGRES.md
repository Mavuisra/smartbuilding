# Base PostgreSQL Render — configuration SBMS

## Ne jamais mettre le mot de passe dans Git

Collez `DATABASE_URL` **uniquement** dans le dashboard Render :

**Service web → Environment → Add Environment Variable**

| Clé | Valeur |
|-----|--------|
| `DATABASE_URL` | Votre chaîne **Internal Database URL** (PostgreSQL) |
| `DJANGO_DEBUG` | `false` |
| `RENDER` | `true` |
| `SBMS_PRODUCTION` | `true` |
| `SBMS_RUN_SEED` | `true` au **premier** déploiement, puis `false` |

Exemple de format (sans exposer le vrai mot de passe) :

```text
postgresql://UTILISATEUR:MOT_DE_PASSE@dpg-xxxxx-a/NOM_BASE
```

## Lier la base au service web (obligatoire)

Render met `RENDER=true` automatiquement. Sans `DATABASE_URL` sur le **service web**, le build échoue.

### Méthode A — Connexions (recommandé)

1. Render → service web **smartbuilding-0kbk**
2. Onglet **Connections** (ou **Connect**)
3. **Connect** la base PostgreSQL **dimplomate**
4. Render ajoute `DATABASE_URL` automatiquement

### Méthode B — Variable manuelle

1. Render → base **dimplomate** → copier **Internal Database URL**
2. Render → service web → **Environment**
3. Clé : `DATABASE_URL` — valeur : l’URL interne complète
4. **Save Changes**

### Commande de build Render

Dans **Settings → Build Command** du service web :

```bash
cd smartbuilding-web && chmod +x build.sh && ./build.sh
```

**Start Command** :

```bash
cd smartbuilding-web && gunicorn smartbuilding_web.wsgi:application --bind 0.0.0.0:$PORT
```

Puis **Manual Deploy** → dernier commit.

## Après un échec de déploiement

Si le build échouait sans `DATABASE_URL`, l’app utilisait SQLite éphémère et notre garde-fou bloquait le démarrage.  
Une fois `DATABASE_URL` PostgreSQL configurée, le déploiement doit passer.

## Resynchroniser les données

Les données déjà perdues dans un SQLite éphémère ne reviennent pas seules :

1. Ouvrir l’application **desktop SBMS**
2. Menu **Synchronisation** → synchroniser vers le cloud
3. Vérifier le portail web **Personnel**, **Locations**, etc.

## Sécurité

Si la chaîne de connexion a été partagée en clair, **régénérez le mot de passe** de la base dans Render (Settings → Reset password), puis mettez à jour `DATABASE_URL`.
