# Déploiement LWS — sbms.lasaveur.store

Domaine : **https://sbms.lasaveur.store/**

> Tant que la page LWS « Félicitations, votre domaine a bien été créé » s’affiche, l’API PHP n’est pas encore en ligne.

## 0. Package prêt à uploader (local)

Sur votre PC, générer le dossier plat (inclut `vendor/`) :

```powershell
cd sbms-cloud
php composer.phar install --no-dev --optimize-autoloader
.\scripts\prepare-lws-deploy.ps1
```

Résultat : **`deploy/lws-sbms-upload/`** — uploader **tout son contenu** à la racine FTP.

## 0b. Connexion FileZilla

| Paramètre | Valeur |
|-----------|--------|
| Hôte | `ftp.lasaveur.store` |
| Port | `21` |
| Protocole | FTP |
| Utilisateur | *(votre compte FTP LWS — voir panel LWS)* |
| Mot de passe | *(défini dans le panel LWS — ne jamais le committer)* |

**Important sécurité** : si le mot de passe a été partagé en clair (chat, email), **changez-le** dans LWS Panel → FTP.

Étapes FileZilla :

1. Panneau **local** → ouvrir `deploy/lws-sbms-upload/`
2. Panneau **distant** → racine du site (fichier `default_index.html` visible)
3. Glisser-déposer **tous** les fichiers et dossiers (`index.php`, `.htaccess`, `bootstrap/`, `src/`, `vendor/`, etc.)
4. **Supprimer** `default_index.html` sur le serveur
5. Vérifier que `.env` est bien uploadé (afficher fichiers cachés dans FileZilla : Serveur → Forcer l’affichage des fichiers cachés)

## État actuel (sbms.lasaveur.store)

| URL | Statut |
|-----|--------|
| https://sbms.lasaveur.store/health/ | OK — API PHP en ligne |
| https://sbms.lasaveur.store/login/ | OK — portail PDG |
| Login / sync | **En attente MySQL** — voir section 1 ci-dessous |

Fichiers déployés sur FTP (`662` fichiers + correctifs). `default_index.html` supprimé.

## 1. Panel LWS (LWS Panel) — **à faire maintenant**

1. **Hébergement web** → activer PHP **8.1+** pour le domaine `sbms.lasaveur.store`
2. **Base MySQL** → créer une base (ex. `lasavXXX_sbms`) + utilisateur + mot de passe
3. **phpMyAdmin** → importer `sbms-cloud/migrations/001_initial_schema.sql`
4. Noter : `DB_HOST` (souvent `localhost` ou `127.0.0.1` sur LWS), `DB_NAME`, `DB_USER`, `DB_PASS`

## 2. Upload des fichiers

Via **FTP / File Manager** :

```
/home/.../sbms.lasaveur.store/     ← racine du site (selon LWS)
├── public/          ← document root (à configurer dans le panel)
│   ├── index.php
│   └── .htaccess
├── bootstrap/
├── src/
├── templates/
├── migrations/
├── scripts/
├── vendor/          ← après composer install sur le serveur
├── composer.json
├── composer.lock
└── .env             ← créé à partir de .env.example (NE PAS committer)
```

**Document root** : le panel LWS doit cibler le sous-dossier **`public/`** (pas la racine du projet).

Si LWS ne permet pas de changer le document root, déplacer le contenu de `public/` à la racine et adapter `index.php` :

```php
require dirname(__DIR__) . '/vendor/autoload.php';
// devient selon structure :
require __DIR__ . '/../vendor/autoload.php';
```

## 3. Composer sur le serveur

SSH ou terminal LWS (si disponible) :

```bash
cd /chemin/vers/sbms-cloud
composer install --no-dev --optimize-autoloader
php scripts/seed.php
```

Sans SSH : uploader `vendor/` depuis votre PC après `composer install` local.

## 4. Fichier `.env`

```env
APP_ENV=production
APP_DEBUG=false
APP_URL=https://sbms.lasaveur.store

DB_HOST=127.0.0.1
DB_PORT=3306
DB_NAME=2675681_nombase
DB_USER=2675681_user
DB_PASS=votre_mot_de_passe_mysql

JWT_SIGNING_KEY=SmartBuilding_SuperSecret_Key_Min32Chars_2026!
JWT_TTL_HOURS=8
```

> Sur LWS : **`DB_HOST=127.0.0.1`** (évite l'erreur socket MySQL).

## 5. Vérifications

| URL | Résultat attendu |
|-----|------------------|
| https://sbms.lasaveur.store/health/ | `{"success":true,"data":{"status":"ok","service":"sbms-cloud-php"}}` |
| https://sbms.lasaveur.store/login/ | Page connexion portail PDG |
| POST https://sbms.lasaveur.store/api/auth/login/ | JWT avec `admin` / `Admin@2026` |

Test rapide (PowerShell) :

```powershell
Invoke-RestMethod -Uri "https://sbms.lasaveur.store/health/" -Method GET
```

## 6. Desktop WPF

`SmartBuilding.Desktop.WPF/appsettings.json` :

```json
"Api": {
  "BaseUrl": "https://sbms.lasaveur.store/"
}
```

Puis relancer l’app → module **Synchronisation** → push/pull.

## Comptes cloud

| Login | Mot de passe | Rôle |
|-------|--------------|------|
| admin | Admin@2026 | Administrateur |
| pdg | Pdg@2026 | PDG |
