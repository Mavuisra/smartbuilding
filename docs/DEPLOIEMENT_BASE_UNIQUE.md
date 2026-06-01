# Une seule base MySQL — PC serveur + PC clients (exe)

## Schéma

```
                    ┌─────────────────────────────┐
                    │  PC SERVEUR (admin)         │
                    │  XAMPP MySQL + sbms_local   │
                    │  IP fixe ex. 192.168.1.10  │
                    └──────────────┬──────────────┘
                                   │ port 3306 (réseau local)
              ┌────────────────────┼────────────────────┐
              ▼                    ▼                    ▼
        PC Réception           PC Bureau 2          (exe SBMS)
        appsettings Client     appsettings Client
```

**Une seule base** `sbms_local` — tous les postes lisent/écrivent dedans.

## 1. PC serveur (admin)

1. Installer **XAMPP**, démarrer **MySQL**.
2. Donner une **IP fixe** au PC (ex. `192.168.1.10`) dans le routeur ou les paramètres réseau Windows.
3. Copier le dossier SBMS (exe) et renommer `deploy/appsettings.Serveur.json` → `appsettings.json` à côté de l’exe.
4. Lancer SBMS **une fois** (crée la base + tables + comptes admin).
5. phpMyAdmin → exécuter `deploy/mysql-utilisateur-reseau.sql` (mot de passe utilisateur `sbms`).
6. Pare-feu Windows : autoriser **MySQL (port 3306)** en réseau **privé**.

## 2. PC clients (réception, autre bureau)

1. **Pas besoin** de XAMPP sur le client (optionnel).
2. Copier le dossier SBMS (exe).
3. Copier `deploy/appsettings.Client.json` → `appsettings.json`.
4. Modifier :
   - `ServerHost` : IP du PC serveur (`192.168.1.10`)
   - `User` / `Password` : identiques au script SQL (`sbms` / votre mot de passe)
5. Lancer l’exe → connexion directe à la base du serveur.

## 3. Cloud (optionnel)

La sync cloud reste possible pour sauvegarde / portail PDG, mais **les postes partagent déjà la même base** en local.

## Dépannage client

| Problème | Solution |
|----------|----------|
| « Impossible de joindre MySQL » | MySQL démarré sur le serveur ? Bonne IP ? |
| Accès refusé | Utilisateur `sbms` créé ? Mot de passe dans appsettings ? |
| Timeout | Pare-feu 3306 ouvert sur le serveur |

## Fichiers modèles

- `deploy/appsettings.Serveur.json`
- `deploy/appsettings.Client.json`
- `deploy/mysql-utilisateur-reseau.sql`
