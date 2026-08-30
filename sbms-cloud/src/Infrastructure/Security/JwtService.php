<?php

declare(strict_types=1);

namespace Sbms\Cloud\Infrastructure\Security;

use Firebase\JWT\JWT;
use Firebase\JWT\Key;

final class JwtService
{
    public function __construct(
        private readonly string $signingKey,
        private readonly int $ttlHours = 8,
    ) {
    }

    public function createToken(string $userId, string $username, string $role): string
    {
        $now = time();
        $payload = [
            'token_type' => 'access',
            'exp' => $now + ($this->ttlHours * 3600),
            'iat' => $now,
            'jti' => bin2hex(random_bytes(16)),
            'user_id' => $userId,
            'username' => $username,
            'role' => $role,
        ];
        return JWT::encode($payload, $this->signingKey, 'HS256');
    }

    public function decode(string $token): object
    {
        return JWT::decode($token, new Key($this->signingKey, 'HS256'));
    }

    public function expiresAtIso(): string
    {
        return gmdate('Y-m-d\TH:i:s\Z', time() + ($this->ttlHours * 3600));
    }
}
