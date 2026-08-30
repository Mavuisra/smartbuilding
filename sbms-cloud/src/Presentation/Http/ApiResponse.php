<?php

declare(strict_types=1);

namespace Sbms\Cloud\Presentation\Http;

use Psr\Http\Message\ResponseInterface as Response;
use Slim\Psr7\Response as SlimResponse;

final class ApiResponse
{
    public static function ok(mixed $data = null, ?string $message = null, int $status = 200): Response
    {
        return self::json([
            'success' => true,
            'data' => $data,
            'message' => $message,
            'errors' => null,
        ], $status);
    }

    public static function fail(string $message, mixed $errors = null, int $status = 400): Response
    {
        return self::json([
            'success' => false,
            'data' => null,
            'message' => $message,
            'errors' => $errors,
        ], $status);
    }

    /** Réponse pull sync — SANS enveloppe (compat desktop WPF). */
    public static function raw(array $payload, int $status = 200): Response
    {
        return self::json($payload, $status);
    }

    private static function json(array $body, int $status): Response
    {
        $response = new SlimResponse($status);
        $response->getBody()->write((string) json_encode($body, JSON_UNESCAPED_UNICODE | JSON_THROW_ON_ERROR));
        return $response->withHeader('Content-Type', 'application/json; charset=utf-8');
    }
}
