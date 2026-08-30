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

    public function find(string $id): ?array
    {
        $stmt = $this->pdo->prepare('SELECT * FROM synced_entity_store WHERE id = ?');
        $stmt->execute([$id]);
        $row = $stmt->fetch();
        if (!$row) {
            return null;
        }
        $row['json_data'] = json_decode($row['json_data'], true) ?? [];
        return $row;
    }

    public function findByTypeAndId(string $entityType, string $id): ?array
    {
        $stmt = $this->pdo->prepare(
            'SELECT * FROM synced_entity_store WHERE id = ? AND entity_type = ?'
        );
        $stmt->execute([$id, $entityType]);
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
    ): void {
        $existing = $this->find($id);
        $created = SyncUtils::toMysqlDatetime($createdAt ?? ($existing ? null : SyncUtils::nowUtc()));
        if ($existing && !$created) {
            $created = $existing['created_at'];
        }

        if ($existing) {
            $stmt = $this->pdo->prepare(
                'UPDATE synced_entity_store SET entity_type = ?, json_data = ?, updated_at = ?,
                 deleted_at = ? WHERE id = ?'
            );
            $stmt->execute([
                $entityType,
                json_encode($jsonData, JSON_UNESCAPED_UNICODE),
                SyncUtils::toMysqlDatetime($updatedAt),
                SyncUtils::toMysqlDatetime($deletedAt),
                $id,
            ]);
            return;
        }

        $stmt = $this->pdo->prepare(
            'INSERT INTO synced_entity_store (id, entity_type, json_data, created_at, updated_at, deleted_at)
             VALUES (?, ?, ?, ?, ?, ?)'
        );
        $stmt->execute([
            $id,
            $entityType,
            json_encode($jsonData, JSON_UNESCAPED_UNICODE),
            $created ?? SyncUtils::toMysqlDatetime(SyncUtils::nowUtc()),
            SyncUtils::toMysqlDatetime($updatedAt),
            SyncUtils::toMysqlDatetime($deletedAt),
        ]);
    }

    public function changesSince(string $entityType, DateTimeImmutable $since, int $limit = 500): array
    {
        $stmt = $this->pdo->prepare(
            'SELECT id, updated_at, deleted_at, json_data FROM synced_entity_store
             WHERE entity_type = ? AND updated_at > ? ORDER BY updated_at ASC LIMIT ?'
        );
        $stmt->bindValue(1, $entityType);
        $stmt->bindValue(2, SyncUtils::toMysqlDatetime($since));
        $stmt->bindValue(3, $limit, PDO::PARAM_INT);
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

    public function countByType(string $entityType): int
    {
        $stmt = $this->pdo->prepare(
            'SELECT COUNT(*) FROM synced_entity_store WHERE entity_type = ? AND deleted_at IS NULL'
        );
        $stmt->execute([$entityType]);
        return (int) $stmt->fetchColumn();
    }

    public function rowsByType(string $entityType, int $limit = 500): array
    {
        $stmt = $this->pdo->prepare(
            'SELECT id, json_data FROM synced_entity_store
             WHERE entity_type = ? AND deleted_at IS NULL ORDER BY updated_at DESC LIMIT ?'
        );
        $stmt->bindValue(1, $entityType);
        $stmt->bindValue(2, $limit, PDO::PARAM_INT);
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
