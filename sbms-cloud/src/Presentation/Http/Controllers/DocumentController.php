<?php

declare(strict_types=1);

namespace Sbms\Cloud\Presentation\Http\Controllers;

use Psr\Http\Message\ResponseInterface;
use Psr\Http\Message\ServerRequestInterface;
use Sbms\Cloud\Infrastructure\Persistence\DocumentRepository;
use Sbms\Cloud\Infrastructure\Persistence\SyncEventRepository;
use Sbms\Cloud\Presentation\Http\ApiResponse;
use Slim\Psr7\Response;

final class DocumentController
{
    public function __construct(
        private readonly DocumentRepository $documents,
        private readonly SyncEventRepository $events,
    ) {
    }

    public function upload(ServerRequestInterface $request): ResponseInterface
    {
        $user = $request->getAttribute('user');
        $body = (array) ($request->getParsedBody() ?? []);

        $entityType = (string) ($body['entityType'] ?? $body['EntityType'] ?? '');
        $entityId = (string) ($body['entityId'] ?? $body['EntityId'] ?? '');
        $category = (string) ($body['category'] ?? $body['Category'] ?? 'rapports');
        $fileName = (string) ($body['fileName'] ?? $body['FileName'] ?? 'document.pdf');
        $mimeType = (string) ($body['mimeType'] ?? $body['MimeType'] ?? 'application/pdf');
        $contentB64 = (string) ($body['contentBase64'] ?? $body['ContentBase64'] ?? '');
        $addedBy = (string) ($body['addedBy'] ?? $body['AddedBy'] ?? '');
        $shaClient = strtolower((string) ($body['contentSha256'] ?? $body['ContentSha256'] ?? ''));

        if (!$entityType || !$entityId) {
            return ApiResponse::fail('entityType et entityId sont requis.', null, 400);
        }
        if (!$contentB64) {
            return ApiResponse::fail('contentBase64 est requis.', null, 400);
        }

        if (!preg_match('/^[0-9a-f-]{36}$/i', $entityId)) {
            return ApiResponse::fail('entityId invalide.', null, 400);
        }

        $raw = base64_decode($contentB64, true);
        if ($raw === false) {
            return ApiResponse::fail('contentBase64 invalide.', null, 400);
        }
        if (strlen($raw) > 20 * 1024 * 1024) {
            return ApiResponse::fail('Fichier trop volumineux (max 20 Mo).', null, 400);
        }

        $sha = hash('sha256', $raw);
        if ($shaClient && $shaClient !== $sha) {
            return ApiResponse::fail('Hash SHA256 incohérent.', null, 400);
        }

        $dup = $this->documents->findDuplicate($entityType, $entityId, $sha);
        if ($dup) {
            return ApiResponse::ok(['id' => $dup['id'], 'duplicate' => true]);
        }

        $this->documents->upsert(
            $entityId,
            $entityType,
            $entityId,
            $category,
            substr($fileName, 0, 260),
            substr($mimeType, 0, 120),
            $raw,
            $sha,
            substr($addedBy, 0, 150)
        );

        $this->events->log(
            $user['username'] ?? '',
            $user['role'] ?? '',
            'Documents',
            'push',
            1,
            true
        );

        return ApiResponse::ok(['id' => $entityId, 'fileSize' => strlen($raw), 'sha256' => $sha]);
    }

    public function download(ServerRequestInterface $request, array $args): ResponseInterface
    {
        $docId = $args['document_id'] ?? '';
        $doc = $this->documents->find($docId);
        if (!$doc) {
            return ApiResponse::fail('Document introuvable.', null, 404);
        }

        $response = new Response(200);
        $response->getBody()->write($doc['file_data']);
        return $response
            ->withHeader('Content-Type', $doc['mime_type'] ?: 'application/pdf')
            ->withHeader('Content-Disposition', 'inline; filename="' . ($doc['file_name'] ?: 'document.pdf') . '"');
    }
}
