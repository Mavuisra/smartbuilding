<?php

declare(strict_types=1);

use Sbms\Cloud\Presentation\Http\Controllers\AuthController;
use Sbms\Cloud\Presentation\Http\Controllers\DocumentController;
use Sbms\Cloud\Presentation\Http\Controllers\ExecutiveController;
use Sbms\Cloud\Presentation\Http\Controllers\HealthController;
use Sbms\Cloud\Presentation\Http\Controllers\SyncController;
use Sbms\Cloud\Presentation\Http\Middleware\JwtAuthMiddleware;
use Sbms\Cloud\Presentation\Web\ExecutiveWebController;
use Slim\App;
use Slim\Factory\AppFactory;

$container = require __DIR__ . '/container.php';

$resolve = static function (string $key) use ($container) {
    $item = $container[$key];
    return is_callable($item) ? $item() : $item;
};

AppFactory::setContainer(new class($container, $resolve) implements \Psr\Container\ContainerInterface {
    public function __construct(private array $items, private \Closure $resolve)
    {
    }

    public function get(string $id)
    {
        if (!isset($this->items[$id])) {
            throw new \RuntimeException("Service {$id} introuvable.");
        }
        $item = $this->items[$id];
        return is_callable($item) ? ($this->resolve)($id) : $item;
    }

    public function has(string $id): bool
    {
        return isset($this->items[$id]);
    }
});

$app = AppFactory::create();
$app->addBodyParsingMiddleware();
$app->addRoutingMiddleware();
$app->addErrorMiddleware(
    filter_var($_ENV['APP_DEBUG'] ?? false, FILTER_VALIDATE_BOOLEAN),
    true,
    true
);

$auth = fn ($req, $handler) => $resolve(JwtAuthMiddleware::class)->process($req, $handler);

$health = $resolve(HealthController::class);
$web = $resolve(ExecutiveWebController::class);

$app->get('/health', [$health, 'get']);
$app->get('/health/', [$health, 'get']);

$app->post('/api/auth/login', fn ($req, $res) => $resolve(AuthController::class)->login($req));
$app->post('/api/auth/login/', fn ($req, $res) => $resolve(AuthController::class)->login($req));
$app->post('/api/auth/logout', fn ($req, $res) => $resolve(AuthController::class)->logout($req))->add($auth);
$app->post('/api/auth/logout/', fn ($req, $res) => $resolve(AuthController::class)->logout($req))->add($auth);
$app->get('/api/auth/session', fn ($req, $res) => $resolve(AuthController::class)->session($req));
$app->get('/api/auth/session/', fn ($req, $res) => $resolve(AuthController::class)->session($req));

$app->post('/api/sync/push', fn ($req, $res) => $resolve(SyncController::class)->push($req))->add($auth);
$app->post('/api/sync/push/', fn ($req, $res) => $resolve(SyncController::class)->push($req))->add($auth);
$app->get('/api/sync/pull', fn ($req, $res) => $resolve(SyncController::class)->pull($req))->add($auth);
$app->get('/api/sync/pull/', fn ($req, $res) => $resolve(SyncController::class)->pull($req))->add($auth);
$app->get('/api/sync/status', fn ($req, $res) => $resolve(SyncController::class)->status($req))->add($auth);
$app->get('/api/sync/status/', fn ($req, $res) => $resolve(SyncController::class)->status($req))->add($auth);
$app->post('/api/sync/documents/upload', fn ($req, $res) => $resolve(DocumentController::class)->upload($req))->add($auth);
$app->post('/api/sync/documents/upload/', fn ($req, $res) => $resolve(DocumentController::class)->upload($req))->add($auth);
$app->get('/api/documents/{document_id}', fn ($req, $res, $args) => $resolve(DocumentController::class)->download($req, $args))->add($auth);
$app->get('/api/documents/{document_id}/', fn ($req, $res, $args) => $resolve(DocumentController::class)->download($req, $args))->add($auth);

$exec = static fn () => $resolve(ExecutiveController::class);
$app->get('/api/dashboard/summary', fn ($req, $res) => $exec()->summary($req))->add($auth);
$app->get('/api/dashboard/summary/', fn ($req, $res) => $exec()->summary($req))->add($auth);
$app->get('/api/executive/overview', fn ($req, $res) => $exec()->overview($req))->add($auth);
$app->get('/api/executive/overview/', fn ($req, $res) => $exec()->overview($req))->add($auth);
$app->get('/api/executive/tenants', fn ($req, $res) => $exec()->tenants($req))->add($auth);
$app->get('/api/executive/tenants/', fn ($req, $res) => $exec()->tenants($req))->add($auth);
$app->get('/api/executive/incidents', fn ($req, $res) => $exec()->incidents($req))->add($auth);
$app->get('/api/executive/incidents/', fn ($req, $res) => $exec()->incidents($req))->add($auth);
$app->get('/api/executive/sync-logs', fn ($req, $res) => $exec()->syncLogs($req))->add($auth);
$app->get('/api/executive/sync-logs/', fn ($req, $res) => $exec()->syncLogs($req))->add($auth);
$app->get('/api/executive/navigation', fn ($req, $res) => $exec()->navigation($req))->add($auth);
$app->get('/api/executive/navigation/', fn ($req, $res) => $exec()->navigation($req))->add($auth);
$app->get('/api/executive/modules/{slug}', fn ($req, $res, $args) => $exec()->moduleData($req, $args))->add($auth);
$app->get('/api/executive/modules/{slug}/', fn ($req, $res, $args) => $exec()->moduleData($req, $args))->add($auth);
$app->get('/api/executive/notifications', fn ($req, $res) => $exec()->notifications($req))->add($auth);
$app->get('/api/executive/notifications/', fn ($req, $res) => $exec()->notifications($req))->add($auth);
$app->get('/api/executive/admin/database-info', fn ($req, $res) => $exec()->databaseInfo($req))->add($auth);
$app->get('/api/executive/admin/database-info/', fn ($req, $res) => $exec()->databaseInfo($req))->add($auth);
$app->post('/api/executive/admin/reset-database', fn ($req, $res) => $exec()->resetDatabase($req))->add($auth);
$app->post('/api/executive/admin/reset-database/', fn ($req, $res) => $exec()->resetDatabase($req))->add($auth);
$app->get('/api/executive/users/{user_id}', fn ($req, $res, $args) => $exec()->userDetail($req, $args))->add($auth);
$app->get('/api/executive/users/{user_id}/', fn ($req, $res, $args) => $exec()->userDetail($req, $args))->add($auth);
$app->post('/api/executive/users', fn ($req, $res, $args) => $exec()->userDetail($req, null))->add($auth);
$app->post('/api/executive/users/', fn ($req, $res, $args) => $exec()->userDetail($req, null))->add($auth);
$app->patch('/api/executive/users/{user_id}', fn ($req, $res, $args) => $exec()->userDetail($req, $args))->add($auth);
$app->patch('/api/executive/users/{user_id}/', fn ($req, $res, $args) => $exec()->userDetail($req, $args))->add($auth);
$app->post('/api/executive/validations/expenses/{expense_id}/{action}', fn ($req, $res, $args) => $exec()->expenseValidation($req, $args))->add($auth);
$app->post('/api/executive/validations/expenses/{expense_id}/{action}/', fn ($req, $res, $args) => $exec()->expenseValidation($req, $args))->add($auth);

$app->get('/', [$web, 'home']);
$app->get('/login', [$web, 'login']);
$app->get('/login/', [$web, 'login']);
$app->get('/{slug}', [$web, 'module']);
$app->get('/{slug}/', [$web, 'module']);

return $app;
