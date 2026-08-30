<?php

declare(strict_types=1);

namespace Sbms\Cloud\Presentation\Http\Controllers;

use Psr\Http\Message\ResponseInterface;
use Psr\Http\Message\ServerRequestInterface;
use Sbms\Cloud\Infrastructure\Persistence\OrganizationRepository;
use Sbms\Cloud\Infrastructure\Security\OrganizationContext;
use Sbms\Cloud\Presentation\Http\ApiResponse;

final class OrganizationController
{
    public function __construct(
        private readonly OrganizationRepository $organizations,
        private readonly OrganizationContext $orgContext,
    ) {
    }

    public function register(ServerRequestInterface $request): ResponseInterface
    {
        $user = $request->getAttribute('user') ?? [];
        $body = (array) ($request->getParsedBody() ?? []);
        $orgId = trim((string) ($body['id'] ?? $body['Id'] ?? ''));

        if (!$this->isUuid($orgId)) {
            return ApiResponse::fail('Identifiant organisation (id) invalide.', null, 400);
        }

        try {
            $org = $this->organizations->register($orgId, $body, (string) ($user['username'] ?? ''));
            return ApiResponse::ok([
                'registered' => true,
                'organization' => $org,
            ]);
        } catch (\InvalidArgumentException $e) {
            return ApiResponse::fail($e->getMessage(), null, 400);
        } catch (\RuntimeException $e) {
            return ApiResponse::fail($e->getMessage(), null, 403);
        }
    }

    public function list(ServerRequestInterface $request): ResponseInterface
    {
        $user = $request->getAttribute('user') ?? [];
        $rows = $this->organizations->listActive();

        if (!$this->orgContext->isSuperAdmin($user)) {
            $username = strtolower((string) ($user['username'] ?? ''));
            $rows = array_values(array_filter(
                $rows,
                fn ($o) => strtolower((string) ($o['createdByUsername'] ?? '')) === $username
                    || $o['id'] === OrganizationRepository::DEFAULT_ORG_ID
            ));
        }

        return ApiResponse::ok($rows);
    }

    private function isUuid(string $value): bool
    {
        return (bool) preg_match(
            '/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i',
            $value
        );
    }
}
