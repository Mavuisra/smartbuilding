<?php

declare(strict_types=1);

namespace Sbms\Cloud\Presentation\Http\Controllers;

use Psr\Http\Message\ResponseInterface;
use Psr\Http\Message\ServerRequestInterface;
use Sbms\Cloud\Presentation\Http\ApiResponse;

final class HealthController
{
    public function get(ServerRequestInterface $request): ResponseInterface
    {
        return ApiResponse::ok([
            'status' => 'ok',
            'service' => 'sbms-cloud-php',
            'php' => PHP_VERSION,
        ]);
    }
}
