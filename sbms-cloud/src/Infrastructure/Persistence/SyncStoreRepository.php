<?php

declare(strict_types=1);

namespace Sbms\Cloud\Infrastructure\Persistence;

use DateTimeImmutable;
use PDO;
use Sbms\Cloud\Infrastructure\Sync\SyncUtils;

final class SyncStoreRepository
{
    public function __construct(private readonly PDO $pdo)
    {
    }

    public function find(string $id, ?string $organizationId = null): ?array
    {
        $sql = 'SELECT * FROM synced_entity_store WHERE id = ?';
        $params = [$id];
        if ($organizationId !== null) {
            $sql .= ' AND (organization_id IS NULL OR organization_id = ?)';
            $params[] = $organizationId;
        }
        $stmt = $this->pdo->prepare($sql);
        $stmt->execute($params);
        $row = $stmt->fetch();
        if (!$row) {
            return null;
        }
        $row['json_data'] = json_decode($row['json_data'], true) ?? [];
        return $row;
    }

    public function findByTypeAndId(string $entityType, string $id, ?string $organizationId = null): ?array
    {
        $sql = 'SELECT * FROM synced_entity_store WHERE id = ? AND entity_type = ?';
        $params = [$id, $entityType];
        if ($organizationId !== null) {
            $sql .= ' AND (organization_id IS NULL OR organization_id = ?)';
            $params[] = $organizationId;
        }
        $stmt = $this->pdo->prepare($sql);
        $stmt->execute($params);
        $row = $stmt->fetch();
        if (!$row) {
            return null;
        }
        $row['json_data'] = json_decode($row['json_data'], true) ?? [];
        return $row;
    }

    public function save(
        string $id,
        string $entityType,
        array $jsonData,
        DateTimeImmutable $updatedAt,
        ?DateTimeImmutable $deletedAt,
        ?DateTimeImmutable $createdAt = null,
        ?string $organizationId = null,
    ): void {
        $existing = $this->find($id, $organizationId);
        $created = SyncUtils::toMysqlDatetime($createdAt ?? ($existing ? null : SyncUtils::nowUtc()));
        if ($existing && !$created) {
            $created = $existing['created_at'];
        }
        $orgId = $organizationId ?? OrganizationRepository::DEFAULT_ORG_ID;

        if ($existing) {
            $stmt = $this->pdo->prepare(
                'UPDATE synced_entity_store SET entity_type = ?, json_data = ?, updated_at = ?,
                 deleted_at = ?, organization_id = ? WHERE id = ?'
            );
            $stmt->execute([
                $entityType,
                json_encode($jsonData, JSON_UNESCAPED_UNICODE),
                SyncUtils::toMysqlDatetime($updatedAt),
                SyncUtils::toMysqlDatetime($deletedAt),
                $orgId,
                $id,
            ]);
            return;
        }

        $stmt = $this->pdo->prepare(
            'INSERT INTO synced_entity_store (id, entity_type, organization_id, json_data, created_at, updated_at, deleted_at)
             VALUES (?, ?, ?, ?, ?, ?, ?)'
        );
        $stmt->execute([
            $id,
            $entityType,
            $orgId,
            json_encode($jsonData, JSON_UNESCAPED_UNICODE),
            $created ?? SyncUtils::toMysqlDatetime(SyncUtils::nowUtc()),
            SyncUtils::toMysqlDatetime($updatedAt),
            SyncUtils::toMysqlDatetime($deletedAt),
        ]);
    }

    public function changesSince(string $entityType, DateTimeImmutable $since, ?string $organizationId = null, int $limit = 500): array
    {
        $sql = 'SELECT id, updated_at, deleted_at, json_data FROM synced_entity_store
             WHERE entity_type = ? AND updated_at > ?';
        $params = [$entityType, SyncUtils::toMysqlDatetime($since)];
        if ($organizationId !== null) {
            $sql .= ' AND (organization_id IS NULL OR organization_id = ?)';
            $params[] = $organizationId;
        }
        $sql .= ' ORDER BY updated_at ASC LIMIT ?';
        $params[] = $limit;

        $stmt = $this->pdo->prepare($sql);
        foreach ($params as $i => $param) {
            $type = ($i === count($params) - 1) ? PDO::PARAM_INT : PDO::PARAM_STR;
            $stmt->bindValue($i + 1, $param, $type);
        }
        $stmt->execute();

        $rows = [];
        foreach ($stmt->fetchAll() as $row) {
            $json = json_decode($row['json_data'], true) ?? [];
            $updated = new DateTimeImmutable($row['updated_at']);
            $deleted = $row['deleted_at'] ? new DateTimeImmutable($row['deleted_at']) : null;
            $rows[] = [
                'id' => $row['id'],
                'updatedAt' => $updated->format('Y-m-d\TH:i:s.u'),
                'deletedAt' => $deleted?->format('Y-m-d\TH:i:s.u'),
                'jsonData' => json_encode($json, JSON_UNESCAPED_UNICODE),
            ];
        }
        return $rows;
    }

    public function countByType(string $entityType, ?string $organizationId = null): int
    {
        $sql = 'SELECT COUNT(*) FROM synced_entity_store WHERE entity_type = ? AND deleted_at IS NULL';
        $params = [$entityType];
        if ($organizationId !== null) {
            $sql .= ' AND (organization_id IS NULL OR organization_id = ?)';
            $params[] = $organizationId;
        }
        $stmt = $this->pdo->prepare($sql);
        $stmt->execute($params);
        return (int) $stmt->fetchColumn();
    }

    public function rowsByType(string $entityType, int $limit = 500, ?string $organizationId = null): array
    {
        $sql = 'SELECT id, json_data FROM synced_entity_store
             WHERE entity_type = ? AND deleted_at IS NULL';
        $params = [$entityType];
        if ($organizationId !== null) {
            $sql .= ' AND (organization_id IS NULL OR organization_id = ?)';
            $params[] = $organizationId;
        }
        $sql .= ' ORDER BY updated_at DESC LIMIT ?';
        $params[] = $limit;

        $stmt = $this->pdo->prepare($sql);
        foreach ($params as $i => $param) {
            $type = ($i === count($params) - 1) ? PDO::PARAM_INT : PDO::PARAM_STR;
            $stmt->bindValue($i + 1, $param, $type);
        }
        $stmt->execute();
        $out = [];
        foreach ($stmt->fetchAll() as $row) {
            $out[] = [
                'id' => $row['id'],
                'json_data' => json_decode($row['json_data'], true) ?? [],
            ];
        }
        return $out;
    }
}
