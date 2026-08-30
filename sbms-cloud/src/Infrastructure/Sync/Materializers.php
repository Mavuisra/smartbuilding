<?php

declare(strict_types=1);

namespace Sbms\Cloud\Infrastructure\Sync;

use PDO;
use Sbms\Cloud\Infrastructure\Persistence\SyncStoreRepository;

/** Délègue la matérialisation au moteur générique (schéma EF complet). */
final class Materializers
{
    private static ?self $instance = null;
    private GenericEntityMaterializer $generic;

    public function __construct(
        private readonly PDO $pdo,
        private readonly SyncStoreRepository $store,
    ) {
        $this->generic = new GenericEntityMaterializer($pdo);
    }

    public static function get(PDO $pdo, SyncStoreRepository $store): self
    {
        if (self::$instance === null) {
            self::$instance = new self($pdo, $store);
        }
        return self::$instance;
    }

    public function handler(string $entityType): ?callable
    {
        if (EntityTableMap::tableFor($entityType) === null) {
            return null;
        }
        return fn (array $data) => $this->generic->materialize($entityType, $data);
    }

    public function materialize(string $entityType, array $data): void
    {
        $this->generic->materialize($entityType, $data);
    }

    public function ensureEntityMaterialized(string $entityType, string $entityId): bool
    {
        $uid = SyncUtils::parseUuid($entityId);
        if (!$uid) {
            return false;
        }

        $table = EntityTableMap::tableFor($entityType);
        if ($table === null) {
            return false;
        }

        if ($this->exists($table, $uid)) {
            return true;
        }

        $storeRow = $this->store->findByTypeAndId($entityType, $uid);
        if (!$storeRow) {
            return false;
        }

        $payload = SyncUtils::injectEntityId($storeRow['json_data'], $uid);
        $this->materialize($entityType, $payload);

        return $this->exists($table, $uid);
    }

    private function exists(string $table, string $id): bool
    {
        $stmt = $this->pdo->prepare("SELECT 1 FROM `{$table}` WHERE `Id` = ? AND `DeletedAt` IS NULL LIMIT 1");
        $stmt->execute([$id]);
        return (bool) $stmt->fetchColumn();
    }
}
