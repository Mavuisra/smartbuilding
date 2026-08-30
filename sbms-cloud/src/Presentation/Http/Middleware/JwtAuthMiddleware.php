<?php

declare(strict_types=1);

namespace Sbms\Cloud\Presentation\Http\Middleware;

use Psr\Http\Message\ResponseInterface;
use Psr\Http\Message\ServerRequestInterface;
use Psr\Http\Server\MiddlewareInterface;
use Psr\Http\Server\RequestHandlerInterface;
use Sbms\Cloud\Infrastructure\Persistence\UserRepository;
use Sbms\Cloud\Infrastructure\Security\JwtService;
use Sbms\Cloud\Presentation\Http\ApiResponse;

final class JwtAuthMiddleware implements MiddlewareInterface
{
    public function __construct(
        private readonly JwtService $jwt,
        private readonly UserRepository $users,
    ) {
    }

    public function process(ServerRequestInterface $request, RequestHandlerInterface $handler): ResponseInterface
    {
        $header = $request->getHeaderLine('Authorization');
        if (!preg_match('/Bearer\s+(\S+)/i', $header, $m)) {
            return ApiResponse::fail('Authentification requise.', null, 401);
        }

        try {
            $payload = $this->jwt->decode($m[1]);
            $userId = (string) ($payload->user_id ?? '');
            $user = $this->users->findById($userId);
            if (!$user) {
                return ApiResponse::fail('Session invalide.', null, 401);
            }
            return $handler->handle($request->withAttribute('user', $user));
        } catch (\Throwable) {
            return ApiResponse::fail('Token invalide ou expiré.', null, 401);
        }
    }
}
