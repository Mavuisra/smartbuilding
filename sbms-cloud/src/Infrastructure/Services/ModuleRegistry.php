<?php

declare(strict_types=1);

namespace Sbms\Cloud\Infrastructure\Services;

use Sbms\Cloud\Infrastructure\Security\PermissionService;

final class ModuleRegistry
{
    private const WEB_PORTAL = ['dashboard', 'rapports', 'documents', 'utilisateurs', 'parametres', 'synchronisation', 'journal'];

    public static function buildNavigation(string $role): array
    {
        $modules = [
            ['id' => 'dashboard', 'title' => 'Tableau de bord', 'icon' => 'ViewDashboard', 'section' => 'main', 'permission' => 'dashboard.view'],
            ['id' => 'rapports', 'title' => 'Rapports', 'icon' => 'FileChart', 'section' => 'main', 'permission' => 'reports.export'],
            ['id' => 'documents', 'title' => 'Documents', 'icon' => 'FileEarmarkPdf', 'section' => 'main', 'permission' => 'dashboard.view'],
            ['id' => 'utilisateurs', 'title' => 'Utilisateurs', 'icon' => 'People', 'section' => 'admin', 'permission' => 'users.manage'],
            ['id' => 'parametres', 'title' => 'Paramètres', 'icon' => 'Gear', 'section' => 'admin', 'permission' => 'dashboard.view'],
            ['id' => 'synchronisation', 'title' => 'Synchronisation', 'icon' => 'ArrowRepeat', 'section' => 'supervision', 'permission' => 'sync.manage'],
            ['id' => 'journal', 'title' => 'Journal', 'icon' => 'JournalText', 'section' => 'supervision', 'permission' => 'dashboard.view'],
        ];

        return array_values(array_filter($modules, fn (array $m) => PermissionService::roleHas($role, $m['permission'])));
    }

    public static function isWebPortalModule(string $slug): bool
    {
        return in_array(self::resolveSlug($slug), self::WEB_PORTAL, true);
    }

    public static function resolveSlug(string $slug): string
    {
        $slug = str_replace('_', '-', strtolower(trim($slug)));
        $aliases = [
            'finance' => 'rapports',
            'finances' => 'rapports',
            'activites-logs' => 'journal',
        ];
        return $aliases[$slug] ?? $slug;
    }

    public static function moduleMeta(string $slug): array
    {
        $slug = self::resolveSlug($slug);
        foreach (self::buildNavigation('Administrateur') as $m) {
            if ($m['id'] === $slug) {
                return $m;
            }
        }
        return ['id' => $slug, 'title' => ucfirst($slug), 'icon' => 'Grid', 'section' => 'main'];
    }
}
