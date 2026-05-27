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

## Lier la base au service web

1. Render → votre base PostgreSQL (`dimplomate`)
2. Copier **Internal Database URL**
3. Render → service web `smartbuilding-0kbk` → **Environment**
4. Coller dans `DATABASE_URL` → **Save Changes**
5. **Manual Deploy** → Deploy latest commit

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
