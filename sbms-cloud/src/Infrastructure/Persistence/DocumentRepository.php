<?php

declare(strict_types=1);

namespace Sbms\Cloud\Infrastructure\Persistence;

use PDO;

final class DocumentRepository
{
    public function __construct(private readonly PDO $pdo)
    {
    }

    public function findDuplicate(string $entityType, string $entityId, string $sha256): ?array
    {
        $stmt = $this->pdo->prepare(
            'SELECT id FROM synced_documents WHERE entity_type = ? AND entity_id = ? AND content_sha256 = ? LIMIT 1'
        );
        $stmt->execute([$entityType, $entityId, $sha256]);
        $row = $stmt->fetch();
        return $row ?: null;
    }

    public function upsert(
        string $id,
        string $entityType,
        string $entityId,
        string $category,
        string $fileName,
        string $mimeType,
        string $binary,
        string $sha256,
        string $addedBy,
    ): void {
        $stmt = $this->pdo->prepare(
            'INSERT INTO synced_documents
             (id, entity_type, entity_id, category, file_name, mime_type, file_data, file_size, content_sha256, added_by)
             VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
             ON DUPLICATE KEY UPDATE
             entity_type = VALUES(entity_type), entity_id = VALUES(entity_id), category = VALUES(category),
             file_name = VALUES(file_name), mime_type = VALUES(mime_type), file_data = VALUES(file_data),
             file_size = VALUES(file_size), content_sha256 = VALUES(content_sha256), added_by = VALUES(added_by),
             updated_at = NOW()'
        );
        $stmt->execute([
            $id, $entityType, $entityId, $category, $fileName, $mimeType,
            $binary, strlen($binary), $sha256, $addedBy,
        ]);
    }

    public function find(string $id): ?array
    {
        $stmt = $this->pdo->prepare('SELECT * FROM synced_documents WHERE id = ?');
        $stmt->execute([$id]);
        $row = $stmt->fetch();
        return $row ?: null;
    }

    public function listByCategory(string $category, int $limit = 200): array
    {
        $stmt = $this->pdo->prepare(
            'SELECT id, entity_type, entity_id, category, file_name, mime_type, file_size, added_by, updated_at
             FROM synced_documents WHERE category = ? ORDER BY updated_at DESC LIMIT ?'
        );
        $stmt->bindValue(1, $category);
        $stmt->bindValue(2, $limit, PDO::PARAM_INT);
        $stmt->execute();
        return $stmt->fetchAll();
    }
}
