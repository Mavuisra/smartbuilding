<?php

declare(strict_types=1);

namespace Sbms\Cloud\Presentation\Http\Controllers;

use PDO;
use Psr\Http\Message\ResponseInterface;
use Psr\Http\Message\ServerRequestInterface;
use Sbms\Cloud\Infrastructure\Persistence\UserRepository;
use Sbms\Cloud\Infrastructure\Security\PermissionService;
use Sbms\Cloud\Infrastructure\Services\DashboardService;
use Sbms\Cloud\Infrastructure\Services\ModuleHandlerRegistry;
use Sbms\Cloud\Infrastructure\Services\ModuleRegistry;
use Sbms\Cloud\Infrastructure\Sync\SyncUtils;
use Sbms\Cloud\Presentation\Http\ApiResponse;

final class ExecutiveController
{
    public function __construct(
        private readonly DashboardService $dashboard,
        private readonly ModuleHandlerRegistry $modules,
        private readonly UserRepository $users,
        private readonly PDO $pdo,
    ) {
    }

    public function summary(ServerRequestInterface $request): ResponseInterface
    {
        return ApiResponse::ok($this->dashboard->executiveSummary());
    }

    public function overview(ServerRequestInterface $request): ResponseInterface
    {
        return ApiResponse::ok($this->dashboard->executiveSummary());
    }

    public function syncHealth(ServerRequestInterface $request): ResponseInterface
    {
        return ApiResponse::ok($this->dashboard->syncHealth());
    }

    public function tenants(ServerRequestInterface $request): ResponseInterface
    {
        $stmt = $this->pdo->query(
            'SELECT Name, Email, Phone, Company, RentalStatus, TenantCategory FROM tenants
             WHERE DeletedAt IS NULL ORDER BY Name LIMIT 200'
        );
        return ApiResponse::ok(array_map(fn ($r) => [
            'name' => $r['Name'],
            'email' => $r['Email'],
            'phone' => $r['Phone'],
            'company' => $r['Company'],
            'status' => $r['RentalStatus'],
            'category' => $r['TenantCategory'],
        ], $stmt->fetchAll()));
    }

    public function incidents(ServerRequestInterface $request): ResponseInterface
    {
        $stmt = $this->pdo->query(
            'SELECT Code, Title, Severity, Status, Location, ReportedAt FROM incidents
             WHERE DeletedAt IS NULL ORDER BY ReportedAt DESC LIMIT 200'
        );
        return ApiResponse::ok($stmt->fetchAll());
    }

    public function syncLogs(ServerRequestInterface $request): ResponseInterface
    {
        $stmt = $this->pdo->query(
            'SELECT username, user_role, entity_type, direction, records_count, success, created_at
             FROM server_sync_events ORDER BY created_at DESC LIMIT 100'
        );
        return ApiResponse::ok($stmt->fetchAll());
    }

    public function navigation(ServerRequestInterface $request): ResponseInterface
    {
        $user = $request->getAttribute('user');
        $role = $user['role'] ?? 'Administrateur';
        return ApiResponse::ok(ModuleRegistry::buildNavigation($role));
    }

    public function moduleData(ServerRequestInterface $request, array $args): ResponseInterface
    {
        $slug = ModuleRegistry::resolveSlug($args['slug'] ?? '');
        return ApiResponse::ok($this->modules->handle($slug));
    }

    public function notifications(ServerRequestInterface $request): ResponseInterface
    {
        $markRead = in_array(strtolower((string) ($request->getQueryParams()['markRead'] ?? '')), ['1', 'true', 'yes'], true);
        if ($markRead) {
            $this->pdo->exec('UPDATE executive_notifications SET is_read = 1 WHERE is_read = 0');
        }
        $stmt = $this->pdo->query(
            'SELECT id, title, message, severity, created_at, is_read FROM executive_notifications
             ORDER BY created_at DESC LIMIT 50'
        );
        return ApiResponse::ok($stmt->fetchAll());
    }

    public function databaseInfo(ServerRequestInterface $request): ResponseInterface
    {
        $user = $request->getAttribute('user');
        if (!PermissionService::roleHas($user['role'] ?? '', 'users.manage')) {
            return ApiResponse::fail('Accès refusé.', null, 403);
        }
        $counts = [];
        foreach (['users', 'synced_entity_store', 'financialtransactions', 'tenants'] as $table) {
            $counts[$table] = (int) $this->pdo->query("SELECT COUNT(*) FROM {$table}")->fetchColumn();
        }
        return ApiResponse::ok([
            'engine' => 'MySQL',
            'database' => $_ENV['DB_NAME'] ?? '',
            'tables' => $counts,
        ]);
    }

    public function resetDatabase(ServerRequestInterface $request): ResponseInterface
    {
        $user = $request->getAttribute('user');
        if (!in_array($user['role'] ?? '', ['Administrateur', 'PDG'], true)) {
            return ApiResponse::fail('Accès refusé.', null, 403);
        }
        $body = (array) ($request->getParsedBody() ?? []);
        if (($body['confirm'] ?? '') !== 'RESET') {
            return ApiResponse::fail('Confirmation RESET requise.', null, 400);
        }

        $tables = [
            'rentpayments', 'leasecontracts', 'premises', 'tenants', 'buildings',
            'financialtransactions', 'employees', 'suppliers', 'incidents', 'equipment',
            'consumptionrecords', 'visitors', 'inventoryitems',
            'synced_entity_store', 'synced_documents', 'server_sync_events', 'executive_notifications',
        ];
        $this->pdo->exec('SET FOREIGN_KEY_CHECKS = 0');
        foreach ($tables as $t) {
            $this->pdo->exec("TRUNCATE TABLE {$t}");
        }
        $this->pdo->exec('SET FOREIGN_KEY_CHECKS = 1');

        return ApiResponse::ok(['reset' => true, 'at' => SyncUtils::isoZ(SyncUtils::nowUtc())]);
    }

    public function userDetail(ServerRequestInterface $request, ?array $args = null): ResponseInterface
    {
        $user = $request->getAttribute('user');
        if (!PermissionService::roleHas($user['role'] ?? '', 'users.manage')) {
            return ApiResponse::fail('Accès refusé.', null, 403);
        }

        $method = strtoupper($request->getMethod());
        $userId = $args['user_id'] ?? null;

        if ($method === 'POST' && !$userId) {
            return $this->createUser($request);
        }
        if ($method === 'PATCH' && $userId) {
            return $this->patchUser($request, (string) $userId);
        }
        if ($method === 'GET' && $userId) {
            $row = $this->users->findById((string) $userId);
            return $row ? ApiResponse::ok($row) : ApiResponse::fail('Utilisateur introuvable.', null, 404);
        }

        return ApiResponse::fail('Méthode non supportée.', null, 405);
    }

    private function createUser(ServerRequestInterface $request): ResponseInterface
    {
        $body = (array) ($request->getParsedBody() ?? []);
        $username = trim((string) ($body['username'] ?? ''));
        $password = (string) ($body['password'] ?? 'ChangeMe@2026');
        $role = (string) ($body['role'] ?? 'Gestionnaire');
        $fullName = (string) ($body['fullName'] ?? $username);
        if (!$username) {
            return ApiResponse::fail('username requis.', null, 400);
        }
        $this->users->upsertSeedUser($username, $password, $role, $fullName);
        return ApiResponse::ok(['created' => true, 'username' => $username]);
    }

    private function patchUser(ServerRequestInterface $request, string $userId): ResponseInterface
    {
        $body = (array) ($request->getParsedBody() ?? []);
        $target = $this->users->findById($userId);
        if (!$target) {
            return ApiResponse::fail('Utilisateur introuvable.', null, 404);
        }

        $action = $body['action'] ?? '';
        if ($action === 'toggle_active') {
            $active = !empty($body['isActive']) ? 1 : 0;
            $stmt = $this->pdo->prepare('UPDATE users SET IsActive = ?, UpdatedAt = NOW(6) WHERE Id = ?');
            $stmt->execute([$active, $userId]);
            return ApiResponse::ok(['isActive' => (bool) $active]);
        }
        if ($action === 'reset_password') {
            $pwd = (string) ($body['password'] ?? '');
            if (strlen($pwd) < 6) {
                return ApiResponse::fail('Mot de passe trop court.', null, 400);
            }
            $this->users->setPasswordHash($userId, $pwd);
            return ApiResponse::ok(['passwordReset' => true]);
        }

        $fields = [];
        $params = [];
        if (isset($body['fullName'])) {
            $fields[] = 'FullName = ?';
            $params[] = $body['fullName'];
        }
        if (isset($body['email'])) {
            $fields[] = 'Email = ?';
            $params[] = $body['email'];
        }
        if (isset($body['role'])) {
            $fields[] = 'Role = ?';
            $params[] = \Sbms\Cloud\Infrastructure\Security\RoleMapper::intFromLabel((string) $body['role']);
        }
        if ($fields) {
            $params[] = $userId;
            $sql = 'UPDATE users SET ' . implode(', ', $fields) . ', UpdatedAt = NOW(6) WHERE Id = ?';
            $stmt = $this->pdo->prepare($sql);
            $stmt->execute($params);
        }
        return ApiResponse::ok(['updated' => true]);
    }

    public function expenseValidation(ServerRequestInterface $request, array $args): ResponseInterface
    {
        $expenseId = $args['expense_id'] ?? '';
        $action = strtolower($args['action'] ?? '');
        if (!in_array($action, ['approve', 'reject'], true)) {
            return ApiResponse::fail('Action invalide.', null, 400);
        }

        $user = $request->getAttribute('user');
        $approved = $action === 'approve';
        $stmt = $this->pdo->prepare(
            'UPDATE financialtransactions SET RequiresPdgApproval = 0, Status = ?,
             ApprovedAt = NOW(6), ApprovedBy = ?, UpdatedAt = NOW(6) WHERE Id = ?'
        );
        $stmt->execute([$approved ? 'Validé PDG' : 'Refusé PDG', $user['username'] ?? '', $expenseId]);

        return ApiResponse::ok(['expenseId' => $expenseId, 'action' => $action]);
    }
}
