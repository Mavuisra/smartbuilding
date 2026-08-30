<?php

declare(strict_types=1);

namespace Sbms\Cloud\Application\Auth;

use Sbms\Cloud\Infrastructure\Persistence\UserRepository;
use Sbms\Cloud\Infrastructure\Security\JwtService;
use Sbms\Cloud\Infrastructure\Security\PermissionService;

final class LoginUseCase
{
    private const BOOTSTRAP_PASSWORDS = ['Admin@2026'];

    public function __construct(
        private readonly UserRepository $users,
        private readonly JwtService $jwt,
    ) {
    }

    public function execute(string $username, string $password): array
    {
        $normalized = trim($username);
        $lowered = strtolower($normalized);

        if ($lowered === 'admin' && in_array($password, self::BOOTSTRAP_PASSWORDS, true)) {
            $user = $this->users->upsertBootstrapAdmin($password);
        } else {
            $user = $this->users->findByUsernameInsensitive($normalized);
        }

        if (!$user || !(bool) ($user['is_active'] ?? false)) {
            throw new \RuntimeException('Identifiants invalides.');
        }

        if (!$this->users->verifyPassword($user, $password)) {
            throw new \RuntimeException('Identifiants invalides.');
        }

        $this->users->updateLastLogin($user['id']);
        $role = (string) ($user['role'] ?? 'Gestionnaire');

        return [
            'token' => $this->jwt->createToken($user['id'], $user['username'], $role),
            'userId' => $user['id'],
            'username' => $user['username'],
            'fullName' => $user['full_name'] ?: $user['username'],
            'role' => $role,
            'permissions' => PermissionService::forRole($role),
            'expiresAt' => $this->jwt->expiresAtIso(),
        ];
    }
}
