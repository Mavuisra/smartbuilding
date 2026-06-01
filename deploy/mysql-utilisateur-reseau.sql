-- À exécuter dans phpMyAdmin sur le PC SERVEUR (une fois).
-- Permet aux autres PC du réseau de se connecter à la base unique sbms_local.

CREATE DATABASE IF NOT EXISTS sbms_local
  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

CREATE USER IF NOT EXISTS 'sbms'@'%' IDENTIFIED BY 'CHANGER_MOT_DE_PASSE';
GRANT ALL PRIVILEGES ON sbms_local.* TO 'sbms'@'%';
FLUSH PRIVILEGES;
