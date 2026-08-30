<?php

declare(strict_types=1);

namespace Sbms\Cloud\Infrastructure\Persistence;

use PDO;

final class UserRepository
{
    public function __construct(private readonly PDO $pdo)
    {
    }

    public function findByUsernameInsensitive(string $username): ?array
    {
        $stmt = $this->pdo->prepare(
            'SELECT * FROM users WHERE LOWER(Username) = LOWER(?) AND DeletedAt IS NULL ORDER BY UpdatedAt DESC LIMIT 1'
        );
        $stmt->execute([trim($username)]);
        $row = $stmt->fetch();
        return $row ? $this->normalize($row) : null;
    }

    public function findById(string $id): ?array
    {
        $stmt = $this->pdo->prepare('SELECT * FROM users WHERE Id = ? AND DeletedAt IS NULL LIMIT 1');
        $stmt->execute([$id]);
        $row = $stmt->fetch();
        return $row ? $this->normalize($row) : null;
    }

    /** @param array<string, mixed> $user */
    public function verifyPassword(array $user, string $password): bool
    {
        $hash = $user['password_hash'] ?? '';
        if ($hash === '') {
            return false;
        }
        if (password_verify($password, $hash)) {
            return true;
        }
        if (str_starts_with($hash, '$2b$')) {
            return password_verify($password, '$2y$' . substr($hash, 4));
        }
        return false;
    }

    public function setPasswordHash(string $userId, string $rawPassword): void
    {
        $hash = password_hash($rawPassword, PASSWORD_BCRYPT);
        $stmt = $this->pdo->prepare('UPDATE users SET PasswordHash = ?, UpdatedAt = NOW(6) WHERE Id = ?');
        $stmt->execute([$hash, $userId]);
    }

    public function updateLastLogin(string $userId): void
    {
        $stmt = $this->pdo->prepare('UPDATE users SET LastLoginAt = NOW(6), UpdatedAt = NOW(6) WHERE Id = ?');
        $stmt->execute([$userId]);
    }

    public function upsertBootstrapAdmin(string $password): array
    {
        return $this->upsertPortalUser('admin', $password, 1, 'Administrateur SBMS');
    }

    public function upsertSeedUser(string $username, string $password, string $role, string $fullName): void
    {
        $this->upsertPortalUser($username, $password, \Sbms\Cloud\Infrastructure\Security\RoleMapper::intFromLabel($role), $fullName);
    }

    private function upsertPortalUser(string $username, string $password, int $roleInt, string $fullName): array
    {
        $existing = $this->findByUsernameInsensitive($username);
        $hash = password_hash($password, PASSWORD_BCRYPT);
        $id = $existing['id'] ?? $this->uuid();

        if ($existing) {
            $stmt = $this->pdo->prepare(
                'UPDATE users SET FullName = ?, Role = ?, IsActive = 1, PasswordHash = ?,
                 DeletedAt = NULL, UpdatedAt = NOW(6) WHERE Id = ?'
            );
            $stmt->execute([$fullName, $roleInt, $hash, $id]);
        } else {
            $stmt = $this->pdo->prepare(
                'INSERT INTO users (Id, Username, Email, PasswordHash, FullName, Role, IsActive,
                 CreatedAt, UpdatedAt, IsSynced)
                 VALUES (?, ?, ?, ?, ?, ?, 1, NOW(6), NOW(6), 0)'
            );
            $stmt->execute([$id, $username, "{$username}@sbms.local", $hash, $fullName, $roleInt]);
        }

        return $this->findById($id) ?? [];
    }

    public function listActive(int $limit = 300): array
    {
        $stmt = $this->pdo->prepare(
            'SELECT Id, Username, Email, FullName, Role, IsActive, LastLoginAt, CreatedAt
             FROM users WHERE DeletedAt IS NULL ORDER BY Username LIMIT ?'
        );
        $stmt->bindValue(1, $limit, PDO::PARAM_INT);
        $stmt->execute();
        return array_map(fn ($r) => $this->normalize($r), $stmt->fetchAll());
    }

    /** @param array<string, mixed> $row */
    private function normalize(array $row): array
    {
        $roleInt = (int) ($row['Role'] ?? 4);
        $username = (string) ($row['Username'] ?? '');
        $roleLabel = strtolower($username) === 'pdg'
            ? 'PDG'
            : \Sbms\Cloud\Infrastructure\Security\RoleMapper::labelFromInt($roleInt);
        return [
            'id' => $row['Id'],
            'username' => $username,
            'email' => $row['Email'] ?? '',
            'full_name' => $row['FullName'] ?? '',
            'password_hash' => $row['PasswordHash'] ?? '',
            'role' => $roleLabel,
            'role_int' => $roleInt,
            'is_active' => (bool) ($row['IsActive'] ?? true),
            'last_login_at' => $row['LastLoginAt'] ?? null,
        ];
    }

    private function uuid(): string
    {
        $data = random_bytes(16);
        $data[6] = chr(ord($data[6]) & 0x0f | 0x40);
        $data[8] = chr(ord($data[8]) & 0x3f | 0x80);
        return vsprintf('%s%s-%s-%s-%s-%s%s%s', str_split(bin2hex($data), 4));
    }
}
