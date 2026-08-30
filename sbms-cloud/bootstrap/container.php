<?php

declare(strict_types=1);

use Dotenv\Dotenv;
use Sbms\Cloud\Application\Auth\LoginUseCase;
use Sbms\Cloud\Infrastructure\Persistence\Database;
use Sbms\Cloud\Infrastructure\Persistence\DocumentRepository;
use Sbms\Cloud\Infrastructure\Persistence\OrganizationRepository;
use Sbms\Cloud\Infrastructure\Persistence\SyncEventRepository;
use Sbms\Cloud\Infrastructure\Persistence\SyncStoreRepository;
use Sbms\Cloud\Infrastructure\Persistence\UserRepository;
use Sbms\Cloud\Infrastructure\Security\JwtService;
use Sbms\Cloud\Infrastructure\Security\OrganizationContext;
use Sbms\Cloud\Infrastructure\Services\DashboardService;
use Sbms\Cloud\Infrastructure\Services\ModuleHandlerRegistry;
use Sbms\Cloud\Infrastructure\Sync\Materializers;
use Sbms\Cloud\Infrastructure\Sync\SyncRegistry;
use Sbms\Cloud\Presentation\Http\Controllers\AuthController;
use Sbms\Cloud\Presentation\Http\Controllers\DocumentController;
use Sbms\Cloud\Presentation\Http\Controllers\ExecutiveController;
use Sbms\Cloud\Presentation\Http\Controllers\HealthController;
use Sbms\Cloud\Presentation\Http\Controllers\OrganizationController;
use Sbms\Cloud\Presentation\Http\Controllers\SyncController;
use Sbms\Cloud\Presentation\Http\Middleware\JwtAuthMiddleware;
use Sbms\Cloud\Presentation\Web\ExecutiveWebController;
use Sbms\Cloud\Presentation\Web\TwigFactory;

$root = dirname(__DIR__);

if (file_exists($root . '/.env')) {
    Dotenv::createImmutable($root)->safeLoad();
}

$jwtKey = $_ENV['JWT_SIGNING_KEY'] ?? 'SmartBuilding_SuperSecret_Key_Min32Chars_2026!';
$jwtTtl = (int) ($_ENV['JWT_TTL_HOURS'] ?? 8);
$jwt = new JwtService($jwtKey, $jwtTtl);
$orgContext = new OrganizationContext();

/** Connexion PDO lazy — /health fonctionne sans MySQL. */
$pdoHolder = new class {
    public ?\PDO $pdo = null;
    public function get(): \PDO
    {
        return $this->pdo ??= Database::pdo();
    }
};

$lazy = static function () use ($pdoHolder, $orgContext): array {
    $pdo = $pdoHolder->get();
    $store = new SyncStoreRepository($pdo);
    $events = new SyncEventRepository($pdo);
    $users = new UserRepository($pdo);
    $organizations = new OrganizationRepository($pdo);
    $documents = new DocumentRepository($pdo);
    $materializers = Materializers::get($pdo, $store);
    $syncRegistry = new SyncRegistry($pdo, $store, $materializers);
    $dashboard = new DashboardService($pdo, $store, $events);
    $moduleHandlers = new ModuleHandlerRegistry($pdo, $store, $events, $documents, $users, $dashboard);

    return compact(
        'pdo',
        'store',
        'events',
        'users',
        'organizations',
        'documents',
        'syncRegistry',
        'dashboard',
        'moduleHandlers',
        'orgContext'
    );
};

return [
    'pdoHolder' => $pdoHolder,
    'lazy' => $lazy,
    'jwt' => $jwt,
    'orgContext' => $orgContext,
    'twig' => TwigFactory::create($root . '/templates/executive'),
    HealthController::class => new HealthController(),
    AuthController::class => static function () use ($lazy, $jwt, $orgContext) {
        $s = $lazy();
        return new AuthController(
            new LoginUseCase($s['users'], $s['organizations'], $orgContext, $jwt),
            $jwt,
            $s['users']
        );
    },
    OrganizationController::class => static function () use ($lazy, $orgContext) {
        $s = $lazy();
        return new OrganizationController($s['organizations'], $orgContext);
    },
    SyncController::class => static function () use ($lazy, $orgContext) {
        $s = $lazy();
        return new SyncController($s['syncRegistry'], $s['events'], $orgContext);
    },
    DocumentController::class => static function () use ($lazy) {
        $s = $lazy();
        return new DocumentController($s['documents'], $s['events']);
    },
    ExecutiveController::class => static function () use ($lazy) {
        $s = $lazy();
        return new ExecutiveController($s['dashboard'], $s['moduleHandlers'], $s['users'], $s['pdo']);
    },
    ExecutiveWebController::class => new ExecutiveWebController(TwigFactory::create($root . '/templates/executive')),
    JwtAuthMiddleware::class => static function () use ($lazy, $jwt) {
        $s = $lazy();
        return new JwtAuthMiddleware($jwt, $s['users']);
    },
];
