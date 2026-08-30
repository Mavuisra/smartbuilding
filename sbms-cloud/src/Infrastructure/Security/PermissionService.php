<?php

declare(strict_types=1);

namespace Sbms\Cloud\Infrastructure\Security;

final class PermissionService
{
    private const ROLE_PERMISSIONS = [
        'Administrateur' => ['*'],
        'PDG' => ['*'],
        'Comptable' => [
            'dashboard.view', 'finance.manage', 'finance.view', 'location.manage',
            'suppliers.manage', 'reports.export', 'personnel.view',
        ],
        'Technique' => [
            'dashboard.view', 'technical.manage', 'incidents.manage', 'consumption.manage',
            'inventory.manage', 'personnel.view',
        ],
        'Gestionnaire' => [
            'dashboard.view', 'location.manage', 'visitors.manage', 'incidents.manage',
            'personnel.manage', 'consumption.manage', 'email.manage', 'reports.export',
        ],
        'Réceptionniste' => ['visitors.manage'],
        'Receptionniste' => ['visitors.manage'],
    ];

    private const ALL_CODES = [
        'dashboard.view', 'personnel.manage', 'personnel.view', 'technical.manage',
        'location.manage', 'finance.manage', 'finance.view', 'suppliers.manage',
        'incidents.manage', 'consumption.manage', 'visitors.manage', 'inventory.manage',
        'email.manage', 'users.manage', 'sync.manage', 'reports.export',
    ];

    public static function forRole(string $role): array
    {
        $perms = self::ROLE_PERMISSIONS[$role] ?? [];
        if (in_array('*', $perms, true)) {
            return self::ALL_CODES;
        }
        return $perms;
    }

    public static function roleHas(string $role, ?string $code): bool
    {
        if ($code === null || $code === '') {
            return true;
        }
        $perms = self::ROLE_PERMISSIONS[$role] ?? [];
        if (in_array('*', $perms, true)) {
            return true;
        }
        return in_array($code, $perms, true);
    }
}
