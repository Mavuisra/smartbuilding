<?php

declare(strict_types=1);

namespace Sbms\Cloud\Infrastructure\Security;

use Psr\Http\Message\ServerRequestInterface;
use Sbms\Cloud\Infrastructure\Persistence\OrganizationRepository;

final class OrganizationContext
{
    private ?string $organizationId = null;

    public function resolve(ServerRequestInterface $request, ?array $user = null): string
    {
        $header = trim($request->getHeaderLine('X-Organization-Id'));
        $query = trim((string) ($request->getQueryParams()['organizationId'] ?? ''));
        $raw = $header !== '' ? $header : $query;

        if ($raw !== '' && $this->isUuid($raw)) {
            $this->organizationId = $raw;
            return $raw;
        }

        $this->organizationId = OrganizationRepository::DEFAULT_ORG_ID;
        return OrganizationRepository::DEFAULT_ORG_ID;
    }

    public function current(): ?string
    {
        return $this->organizationId;
    }

    public function isSuperAdmin(?array $user): bool
    {
        $username = strtolower((string) ($user['username'] ?? ''));
        return $username === 'jessica';
    }

    private function isUuid(string $value): bool
    {
        return (bool) preg_match(
            '/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i',
            $value
        );
    }
}
