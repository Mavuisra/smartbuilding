<?php

declare(strict_types=1);

namespace Sbms\Cloud\Infrastructure\Sync;

use PDO;
use Sbms\Cloud\Domain\Sync\SyncEntityTypes;
use Sbms\Cloud\Infrastructure\Persistence\SyncStoreRepository;

final class SyncRegistry
{
    public function __construct(
        private readonly PDO $pdo,
        private readonly SyncStoreRepository $store,
        private readonly Materializers $materializers,
    ) {
    }

    public function applyPush(string $entityType, array $entities, ?string $organizationId = null): int
    {
        if (!SyncEntityTypes::isSyncable($entityType)) {
            return 0;
        }

        if ($entityType === 'RentPayments') {
            $this->preflightRentPaymentParents($entities);
        }

        $applied = 0;
        foreach ($entities as $payload) {
            try {
                $this->pdo->beginTransaction();
                if ($this->applySinglePush($entityType, $payload, $organizationId)) {
                    $applied++;
                }
                $this->pdo->commit();
            } catch (\Throwable $e) {
                $this->pdo->rollBack();
                error_log("Push échoué {$entityType}: " . $e->getMessage());
            }
        }

        return $applied;
    }

    public function getChangesSince(string $entityType, \DateTimeImmutable $since, ?string $organizationId = null): array
    {
        if (!SyncEntityTypes::isSyncable($entityType)) {
            return [];
        }
        return $this->store->changesSince($entityType, $since, $organizationId);
    }

    private function applySinglePush(string $entityType, array $payload, ?string $organizationId = null): bool
    {
        $entityId = SyncUtils::parseUuid($payload['id'] ?? $payload['Id'] ?? null);
        if (!$entityId) {
            return false;
        }

        $updatedAt = SyncUtils::normalizeSyncDatetime(
            $payload['updatedAt'] ?? $payload['UpdatedAt'] ?? null
        ) ?? SyncUtils::nowUtc();
        $deletedAt = SyncUtils::normalizeSyncDatetime(
            $payload['deletedAt'] ?? $payload['DeletedAt'] ?? null
        );

        $data = $this->parsePushJson($payload);
        if ($data === null) {
            return false;
        }

        $existing = $this->store->find($entityId, $organizationId);
        if ($existing) {
            $data = SyncUtils::mergeSyncPayload($existing['json_data'], $data);
        }

        $this->store->save($entityId, $entityType, $data, $updatedAt, $deletedAt, null, $organizationId);

        $handler = $this->materializers->handler($entityType);
        if ($handler !== null) {
            $handler(SyncUtils::injectEntityId($data, $entityId));
        }

        return true;
    }

    private function parsePushJson(array $payload): ?array
    {
        $jsonRaw = $payload['jsonData'] ?? $payload['JsonData'] ?? '{}';
        if (is_string($jsonRaw)) {
            $decoded = json_decode($jsonRaw, true);
            return is_array($decoded) ? $decoded : null;
        }
        if (is_array($jsonRaw)) {
            return $jsonRaw;
        }
        return null;
    }

    private function preflightRentPaymentParents(array $entities): void
    {
        $leaseIds = [];
        foreach ($entities as $payload) {
            $data = $this->parsePushJson($payload);
            if (!$data) {
                continue;
            }
            $lid = SyncUtils::pick($data, 'LeaseContractId', 'leaseContractId');
            if ($lid) {
                $leaseIds[(string) $lid] = true;
            }
        }

        foreach (array_keys($leaseIds) as $lid) {
            if ($this->materializers->ensureEntityMaterialized('LeaseContracts', $lid)) {
                continue;
            }
            $this->materializers->ensureEntityMaterialized('LeaseContracts', $lid);
            $store = $this->store->findByTypeAndId('LeaseContracts', $lid);
            if ($store) {
                $json = $store['json_data'];
                $pid = SyncUtils::pick($json, 'PremiseId', 'premiseId');
                $tid = SyncUtils::pick($json, 'TenantId', 'tenantId');
                if ($pid) {
                    $this->materializers->ensureEntityMaterialized('Premises', (string) $pid);
                }
                if ($tid) {
                    $this->materializers->ensureEntityMaterialized('Tenants', (string) $tid);
                }
            }
            $this->materializers->ensureEntityMaterialized('LeaseContracts', $lid);
        }
    }
}
