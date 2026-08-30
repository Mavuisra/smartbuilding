<?php

declare(strict_types=1);

/**
 * Migration parité desktop ↔ cloud LWS
 * - Supprime les tables legacy (001 snake_case)
 * - Importe le schéma EF complet (002)
 * - Recrée les tables cloud-only (003)
 * - Seed admin / pdg
 *
 * Usage (une fois) : php scripts/migrate_parity.php
 * Puis supprimer ce fichier du serveur.
 */

require dirname(__DIR__) . '/vendor/autoload.php';

use Dotenv\Dotenv;
use Sbms\Cloud\Infrastructure\Persistence\Database;
use Sbms\Cloud\Infrastructure\Persistence\UserRepository;

$root = dirname(__DIR__);
if (file_exists($root . '/.env')) {
    Dotenv::createImmutable($root)->safeLoad();
}

$pdo = Database::pdo();

/** Tables cloud à conserver (données sync / portail). */
$preserve = [
    'synced_entity_store',
    'server_sync_events',
    'synced_documents',
    'executive_notifications',
];

/** Anciennes tables 001 (snake_case) à supprimer si présentes. */
$legacy001 = [
    'rent_payments', 'lease_contracts', 'financial_transactions',
    'consumption_records', 'inventory_items',
];

function runSqlFile(PDO $pdo, string $path): void
{
    if (!is_readable($path)) {
        throw new RuntimeException("Fichier SQL introuvable : {$path}");
    }
    $sql = file_get_contents($path);
    if ($sql === false || $sql === '') {
        throw new RuntimeException("Fichier SQL vide : {$path}");
    }
    if (str_starts_with($sql, "\xEF\xBB\xBF")) {
        $sql = substr($sql, 3);
    }
    $pdo->exec($sql);
}

echo "=== Migration parité SBMS Cloud ===\n";
echo 'Base : ' . ($_ENV['DB_NAME'] ?? '?') . "\n\n";

$pdo->exec('SET FOREIGN_KEY_CHECKS = 0');

$existing = $pdo->query('SHOW TABLES')->fetchAll(PDO::FETCH_COLUMN);
echo count($existing) . " table(s) avant migration\n";

foreach ($existing as $table) {
    if (in_array($table, $preserve, true)) {
        echo "  conserve : {$table}\n";
        continue;
    }
    echo "  DROP {$table}\n";
    $pdo->exec("DROP TABLE IF EXISTS `{$table}`");
}

foreach ($legacy001 as $table) {
    $pdo->exec("DROP TABLE IF EXISTS `{$table}`");
}

echo "\nImport 002_desktop_full_schema.sql …\n";
runSqlFile($pdo, $root . '/migrations/002_desktop_full_schema.sql');

echo "Import 003_cloud_extensions.sql …\n";
runSqlFile($pdo, $root . '/migrations/003_cloud_extensions.sql');

echo "Import 004_organizations_multitenant.sql …\n";
try {
    runSqlFile($pdo, $root . '/migrations/004_organizations_multitenant.sql');
} catch (PDOException $e) {
    if (!str_contains($e->getMessage(), 'Duplicate column')) {
        throw $e;
    }
    echo "  (colonnes organisation déjà présentes — ignoré)\n";
}

$pdo->exec('SET FOREIGN_KEY_CHECKS = 1');

$tables = $pdo->query('SHOW TABLES')->fetchAll(PDO::FETCH_COLUMN);
sort($tables);
echo "\n" . count($tables) . " table(s) après migration :\n";
foreach ($tables as $t) {
    echo "  - {$t}\n";
}

echo "\nSeed comptes portail …\n";
$users = new UserRepository($pdo);
foreach (
    [
        ['admin', 'Admin@2026', 'Administrateur', 'Administrateur SBMS'],
        ['pdg', 'Pdg@2026', 'PDG', 'Directeur Général'],
    ] as [$username, $password, $role, $fullName]
) {
    $users->upsertSeedUser($username, $password, $role, $fullName);
    echo "  OK {$username}\n";
}

echo "\nMigration terminée avec succès.\n";
