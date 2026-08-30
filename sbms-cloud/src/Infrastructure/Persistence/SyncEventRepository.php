<?php

declare(strict_types=1);

namespace Sbms\Cloud\Infrastructure\Persistence;

use PDO;

final class SyncEventRepository
{
    public function __construct(private readonly PDO $pdo)
    {
    }

    public function log(
        string $username,
        string $role,
        string $entityType,
        string $direction,
        int $count,
        bool $success,
        string $error = '',
    ): void {
        $stmt = $this->pdo->prepare(
            'INSERT INTO server_sync_events
             (username, user_role, entity_type, direction, records_count, success, error_message)
             VALUES (?, ?, ?, ?, ?, ?, ?)'
        );
        $stmt->execute([$username, $role, $entityType, $direction, $count, $success ? 1 : 0, $error]);
    }

    public function recent(int $limit = 100): array
    {
        $stmt = $this->pdo->prepare(
            'SELECT username, user_role, entity_type, direction, records_count, success, error_message, created_at
             FROM server_sync_events ORDER BY created_at DESC LIMIT ?'
        );
        $stmt->bindValue(1, $limit, PDO::PARAM_INT);
        $stmt->execute();
        return $stmt->fetchAll();
    }

    public function recentSuccess(int $limit = 10): array
    {
        $stmt = $this->pdo->prepare(
            'SELECT username, user_role, entity_type, records_count, created_at
             FROM server_sync_events WHERE success = 1 ORDER BY created_at DESC LIMIT ?'
        );
        $stmt->bindValue(1, $limit, PDO::PARAM_INT);
        $stmt->execute();
        return $stmt->fetchAll();
    }
}
