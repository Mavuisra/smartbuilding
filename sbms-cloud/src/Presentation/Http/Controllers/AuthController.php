<?php

declare(strict_types=1);

namespace Sbms\Cloud\Presentation\Http\Controllers;

use Psr\Http\Message\ResponseInterface;
use Psr\Http\Message\ServerRequestInterface;
use Sbms\Cloud\Application\Auth\LoginUseCase;
use Sbms\Cloud\Infrastructure\Persistence\UserRepository;
use Sbms\Cloud\Infrastructure\Security\JwtService;
use Sbms\Cloud\Presentation\Http\ApiResponse;

final class AuthController
{
    public function __construct(
        private readonly LoginUseCase $login,
        private readonly JwtService $jwt,
        private readonly UserRepository $users,
    ) {
    }

    public function login(ServerRequestInterface $request): ResponseInterface
    {
        $body = (array) ($request->getParsedBody() ?? []);
        if (!$body && $request->getBody()->getSize()) {
            $raw = json_decode((string) $request->getBody(), true);
            if (is_array($raw)) {
                $body = $raw;
            }
        }

        $username = $body['username'] ?? $body['Username'] ?? null;
        $password = $body['password'] ?? $body['Password'] ?? null;

        if (!$username || !$password) {
            return ApiResponse::fail('Identifiants requis.', ['username' => 'Requis', 'password' => 'Requis'], 400);
        }

        try {
            return ApiResponse::ok($this->login->execute((string) $username, (string) $password));
        } catch (\RuntimeException $e) {
            return ApiResponse::fail($e->getMessage(), null, 401);
        }
    }

    public function logout(ServerRequestInterface $request): ResponseInterface
    {
        return ApiResponse::ok(['loggedOut' => true]);
    }

    public function session(ServerRequestInterface $request): ResponseInterface
    {
        $user = $request->getAttribute('user');
        if (!$user) {
            $header = $request->getHeaderLine('Authorization');
            if (preg_match('/Bearer\s+(\S+)/i', $header, $m)) {
                try {
                    $payload = $this->jwt->decode($m[1]);
                    $user = $this->users->findById((string) ($payload->user_id ?? ''));
                } catch (\Throwable) {
                    $user = null;
                }
            }
        }

        if (!$user) {
            return ApiResponse::ok(['authenticated' => false]);
        }

        return ApiResponse::ok([
            'authenticated' => true,
            'username' => $user['username'],
            'fullName' => $user['full_name'] ?: $user['username'],
            'role' => $user['role'],
        ]);
    }
}
