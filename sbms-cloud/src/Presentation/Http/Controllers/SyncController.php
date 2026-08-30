<?php

declare(strict_types=1);

namespace Sbms\Cloud\Presentation\Http\Controllers;

use Psr\Http\Message\ResponseInterface;
use Psr\Http\Message\ServerRequestInterface;
use Sbms\Cloud\Domain\Sync\SyncEntityTypes;
use Sbms\Cloud\Infrastructure\Persistence\SyncEventRepository;
use Sbms\Cloud\Infrastructure\Sync\SyncRegistry;
use Sbms\Cloud\Infrastructure\Sync\SyncUtils;
use Sbms\Cloud\Presentation\Http\ApiResponse;

final class SyncController
{
    public function __construct(
        private readonly SyncRegistry $registry,
        private readonly SyncEventRepository $events,
    ) {
    }

    public function push(ServerRequestInterface $request): ResponseInterface
    {
        $user = $request->getAttribute('user');
        $body = (array) ($request->getParsedBody() ?? []);
        $entityType = (string) ($body['entityType'] ?? $body['EntityType'] ?? '');
        $entities = $body['entities'] ?? $body['Entities'] ?? [];

        if (!$entityType || !is_array($entities)) {
            return ApiResponse::fail('Payload de synchronisation invalide.', null, 400);
        }
        if (!SyncEntityTypes::isSyncable($entityType)) {
            return ApiResponse::fail("Type de sync inconnu : {$entityType}", null, 400);
        }

        try {
            $applied = $this->registry->applyPush($entityType, $entities);
            $this->events->log(
                $user['username'] ?? '',
                $user['role'] ?? '',
                $entityType,
                'push',
                $applied,
                true
            );
            return ApiResponse::ok($applied);
        } catch (\Throwable $e) {
            $this->events->log(
                $user['username'] ?? '',
                $user['role'] ?? '',
                $entityType,
                'push',
                0,
                false,
                $e->getMessage()
            );
            return ApiResponse::fail($e->getMessage(), null, 500);
        }
    }

    public function pull(ServerRequestInterface $request): ResponseInterface
    {
        $user = $request->getAttribute('user');
        $params = $request->getQueryParams();
        $entityType = (string) ($params['entityType'] ?? $params['EntityType'] ?? '');
        $sinceRaw = $params['since'] ?? $params['Since'] ?? SyncUtils::MIN_SYNC_DATETIME;

        if (!$entityType) {
            return ApiResponse::fail('Paramètres de synchronisation invalides.', null, 400);
        }
        if (!SyncEntityTypes::isSyncable($entityType)) {
            return ApiResponse::fail("Type de sync inconnu : {$entityType}", null, 400);
        }

        $since = SyncUtils::normalizeSyncDatetime(
            $sinceRaw,
            new \DateTimeImmutable(SyncUtils::MIN_SYNC_DATETIME)
        ) ?? new \DateTimeImmutable(SyncUtils::MIN_SYNC_DATETIME);

        $entities = $this->registry->getChangesSince($entityType, $since);
        $this->events->log(
            $user['username'] ?? '',
            $user['role'] ?? '',
            $entityType,
            'pull',
            count($entities),
            true
        );

        return ApiResponse::raw([
            'serverTimestamp' => SyncUtils::isoZ(SyncUtils::nowUtc()),
            'entities' => $entities,
        ]);
    }

    public function status(ServerRequestInterface $request): ResponseInterface
    {
        return ApiResponse::ok([
            'syncableTypes' => SyncEntityTypes::ALL,
            'serverTime' => SyncUtils::isoZ(SyncUtils::nowUtc()),
        ]);
    }
}
