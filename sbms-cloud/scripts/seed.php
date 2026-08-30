<?php

declare(strict_types=1);

/**
 * Seed comptes admin / PDG — équivalent seed_smartbuilding Django
 * Usage: php scripts/seed.php
 */

require dirname(__DIR__) . '/vendor/autoload.php';

use Dotenv\Dotenv;
use Sbms\Cloud\Infrastructure\Persistence\Database;
use Sbms\Cloud\Infrastructure\Persistence\UserRepository;

$root = dirname(__DIR__);
if (file_exists($root . '/.env')) {
    Dotenv::createImmutable($root)->safeLoad();
}

$users = new UserRepository(Database::pdo());

$accounts = [
    ['admin', 'Admin@2026', 'Administrateur', 'Administrateur SBMS'],
    ['pdg', 'Pdg@2026', 'PDG', 'Directeur Général'],
];

foreach ($accounts as [$username, $password, $role, $fullName]) {
    $users->upsertSeedUser($username, $password, $role, $fullName);
    echo "Compte {$username} vérifié ({$role})\n";
}

echo "Seed terminé.\n";
