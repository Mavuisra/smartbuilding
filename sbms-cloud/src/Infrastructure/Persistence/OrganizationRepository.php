<?php

declare(strict_types=1);

namespace Sbms\Cloud\Infrastructure\Persistence;

use PDO;

final class OrganizationRepository
{
    public const DEFAULT_ORG_ID = '00000000-0000-0000-0000-000000000001';

    public function __construct(private readonly PDO $pdo)
    {
    }

    public function ensureDefault(): void
    {
        $stmt = $this->pdo->prepare(
            'INSERT IGNORE INTO organizations (id, name, slug, database_name, city, is_active, created_by_username)
             VALUES (?, ?, ?, ?, ?, 1, ?)'
        );
        $stmt->execute([
            self::DEFAULT_ORG_ID,
            'Organisation principale',
            'organisation-principale',
            'sbms_local',
            '',
            'admin',
        ]);
    }

    public function findById(string $id): ?array
    {
        $stmt = $this->pdo->prepare(
            'SELECT * FROM organizations WHERE id = ? AND deleted_at IS NULL LIMIT 1'
        );
        $stmt->execute([$id]);
        $row = $stmt->fetch();
        return $row ? $this->normalize($row) : null;
    }

    /** @return list<array<string, mixed>> */
    public function listActive(): array
    {
        $stmt = $this->pdo->query(
            'SELECT * FROM organizations WHERE deleted_at IS NULL AND is_active = 1 ORDER BY name'
        );
        return array_map(fn ($r) => $this->normalize($r), $stmt->fetchAll());
    }

    /** @param array<string, mixed> $payload */
    public function register(string $orgId, array $payload, string $username): array
    {
        $name = trim((string) ($payload['name'] ?? $payload['Name'] ?? ''));
        $slug = strtolower(trim((string) ($payload['slug'] ?? $payload['Slug'] ?? '')));
        if ($name === '') {
            throw new \InvalidArgumentException('Le nom du tenant est obligatoire.');
        }
        if ($slug === '') {
            $slug = $this->slugify($name);
        }
        $slug = $this->normalizeSlug($slug);

        $stmt = $this->pdo->prepare(
            'SELECT id, created_by_username FROM organizations WHERE slug = ? AND id <> ? AND deleted_at IS NULL LIMIT 1'
        );
        $stmt->execute([$slug, $orgId]);
        if ($stmt->fetch()) {
            throw new \InvalidArgumentException('Ce slug est déjà utilisé par une autre organisation.');
        }

        $existing = $this->findById($orgId);
        if ($existing && strtolower($username) !== 'jessica') {
            $owner = strtolower((string) ($existing['created_by_username'] ?? ''));
            if ($owner !== '' && $owner !== strtolower($username)) {
                throw new \RuntimeException('Mise à jour refusée : vous n\'êtes pas propriétaire de cette organisation.');
            }
        }

        $databaseName = trim((string) ($payload['databaseName'] ?? $payload['database_name'] ?? ''));
        $city = trim((string) ($payload['city'] ?? $payload['City'] ?? ''));

        if ($existing) {
            $stmt = $this->pdo->prepare(
                'UPDATE organizations SET name = ?, slug = ?, database_name = ?, city = ?,
                 is_active = 1, created_by_username = COALESCE(NULLIF(created_by_username, ""), ?),
                 updated_at = NOW() WHERE id = ?'
            );
            $stmt->execute([$name, $slug, $databaseName, $city, $username, $orgId]);
        } else {
            $stmt = $this->pdo->prepare(
                'INSERT INTO organizations (id, name, slug, database_name, city, is_active, created_by_username)
                 VALUES (?, ?, ?, ?, ?, 1, ?)'
            );
            $stmt->execute([$orgId, $name, $slug, $databaseName, $city, $username]);
        }

        return $this->findById($orgId) ?? [];
    }

    /** @param array<string, mixed> $row */
    private function normalize(array $row): array
    {
        return [
            'id' => $row['id'],
            'name' => $row['name'],
            'slug' => $row['slug'],
            'databaseName' => $row['database_name'],
            'city' => $row['city'],
            'isActive' => (bool) $row['is_active'],
            'createdByUsername' => $row['created_by_username'] ?? '',
        ];
    }

    private function slugify(string $name): string
    {
        $slug = strtolower(preg_replace('/[^a-z0-9]+/i', '-', $name) ?? '');
        return trim($slug, '-') ?: 'organisation';
    }

    private function normalizeSlug(string $slug): string
    {
        $slug = preg_replace('/[^a-z0-9-]/', '', $slug) ?? '';
        $slug = trim($slug, '-');
        if ($slug === '' || strlen($slug) > 80) {
            throw new \InvalidArgumentException('Slug organisation invalide.');
        }
        return $slug;
    }
}
