<?php

declare(strict_types=1);

namespace Sbms\Cloud\Infrastructure\Security;

final class RoleMapper
{
    private const INT_TO_LABEL = [
        0 => 'Gestionnaire',
        1 => 'Administrateur',
        2 => 'Comptable',
        3 => 'Technique',
        4 => 'Gestionnaire',
        5 => 'Réceptionniste',
    ];

    private const LABEL_TO_INT = [
        'Administrateur' => 1,
        'PDG' => 1,
        'Comptable' => 2,
        'Technique' => 3,
        'Gestionnaire' => 4,
        'Réceptionniste' => 5,
        'Receptionniste' => 5,
    ];

    public static function labelFromInt(int $role): string
    {
        return self::INT_TO_LABEL[$role] ?? 'Gestionnaire';
    }

    public static function intFromLabel(string $role): int
    {
        return self::LABEL_TO_INT[$role] ?? 4;
    }
}
