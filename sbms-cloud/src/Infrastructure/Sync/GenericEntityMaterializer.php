<?php

declare(strict_types=1);

namespace Sbms\Cloud\Infrastructure\Sync;

use PDO;
use PDOException;

/**
 * Matérialise toute entité sync vers sa table EF (colonnes PascalCase).
 * Lit le schéma via INFORMATION_SCHEMA — alignement automatique desktop ↔ cloud.
 */
final class GenericEntityMaterializer
{
    /** @var array<string, array<string, string>> table => column => mysql_type */
    private array $columnCache = [];

    public function __construct(private readonly PDO $pdo)
    {
    }

    public function materialize(string $entityType, array $data): void
    {
        $table = EntityTableMap::tableFor($entityType);
        if ($table === null) {
            return;
        }

        $uid = SyncUtils::pick($data, 'Id', 'id');
        if (!$uid) {
            return;
        }

        $columns = $this->getColumns($table);
        if ($columns === []) {
            return;
        }

        $row = [];
        foreach ($columns as $col => $type) {
            if ($col === 'Id') {
                $row['Id'] = (string) $uid;
                continue;
            }
            $value = $this->resolveValue($data, $col, $type);
            if ($value !== '__SKIP__') {
                $row[$col] = $value;
            }
        }

        if (!isset($row['Id'])) {
            $row['Id'] = (string) $uid;
        }

        $now = date('Y-m-d H:i:s.u');
        if (isset($columns['CreatedAt']) && !isset($row['CreatedAt'])) {
            $row['CreatedAt'] = $now;
        }
        if (isset($columns['UpdatedAt'])) {
            $row['UpdatedAt'] = $now;
        }
        if (isset($columns['IsSynced']) && !isset($row['IsSynced'])) {
            $row['IsSynced'] = 1;
        }

        $this->upsert($table, $row);
    }

    private function resolveValue(array $data, string $column, string $mysqlType): mixed
    {
        $raw = SyncUtils::pick($data, $column, lcfirst($column));
        if ($raw === null) {
            $snake = $this->camelToSnake($column);
            $raw = SyncUtils::pick($data, $snake);
        }

        if ($raw === null) {
            return '__SKIP__';
        }

        if (str_contains($mysqlType, 'int') && !str_contains($mysqlType, 'point')) {
            if (is_bool($raw)) {
                return $raw ? 1 : 0;
            }
            if (is_string($raw) && !is_numeric($raw)) {
                return $this->enumStringToInt($column, $raw);
            }
            return SyncUtils::parseInt($raw, 0);
        }

        if (str_contains($mysqlType, 'decimal')) {
            return SyncUtils::parseDecimal($raw, 0);
        }

        if (str_contains($mysqlType, 'tinyint(1)')) {
            return SyncUtils::parseBool($raw, false) ? 1 : 0;
        }

        if (str_contains($mysqlType, 'datetime')) {
            $dt = SyncUtils::normalizeSyncDatetime($raw);
            return $dt ? SyncUtils::toMysqlDatetime($dt) : null;
        }

        if (str_contains($mysqlType, 'char(36)')) {
            return $raw !== null && $raw !== '' ? (string) $raw : null;
        }

        return is_scalar($raw) || $raw === null ? $raw : json_encode($raw, JSON_UNESCAPED_UNICODE);
    }

    private function enumStringToInt(string $column, string $value): int
    {
        $maps = [
            'Role' => ['' => 0, 'Administrateur' => 1, 'Comptable' => 2, 'Technique' => 3, 'Gestionnaire' => 4, 'Réceptionniste' => 5, 'Receptionniste' => 5, 'PDG' => 1],
            'Status' => ['Brouillon' => 0, 'Actif' => 1, 'Résilié' => 2, 'Resilie' => 2, 'Expiré' => 3, 'Expire' => 3],
            'Severity' => ['Faible' => 1, 'Moyenne' => 2, 'Élevée' => 3, 'Elevee' => 3, 'Critique' => 4],
            'Type' => ['Recette' => 1, 'Depense' => 2, 'Dépense' => 2, 'Expense' => 2, 'Income' => 1],
        ];
        if (isset($maps[$column])) {
            $low = strtolower($value);
            foreach ($maps[$column] as $k => $v) {
                if (strtolower($k) === $low) {
                    return $v;
                }
            }
        }
        return is_numeric($value) ? (int) $value : 0;
    }

    private function camelToSnake(string $input): string
    {
        return strtolower(preg_replace('/([a-z])([A-Z])/', '$1_$2', $input) ?? $input);
    }

    /** @return array<string, string> */
    private function getColumns(string $table): array
    {
        if (isset($this->columnCache[$table])) {
            return $this->columnCache[$table];
        }

        $stmt = $this->pdo->prepare(
            'SELECT COLUMN_NAME, COLUMN_TYPE FROM INFORMATION_SCHEMA.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? ORDER BY ORDINAL_POSITION'
        );
        $stmt->execute([$table]);
        $cols = [];
        foreach ($stmt->fetchAll() as $row) {
            $cols[$row['COLUMN_NAME']] = $row['COLUMN_TYPE'];
        }
        $this->columnCache[$table] = $cols;
        return $cols;
    }

    private function upsert(string $table, array $row): void
    {
        $cols = array_keys($row);
        $placeholders = implode(', ', array_fill(0, count($cols), '?'));
        $updates = implode(', ', array_map(fn ($c) => "`{$c}` = VALUES(`{$c}`)", array_diff($cols, ['Id'])));

        $sql = sprintf(
            'INSERT INTO `%s` (%s) VALUES (%s) ON DUPLICATE KEY UPDATE %s',
            $table,
            implode(', ', array_map(fn ($c) => "`{$c}`", $cols)),
            $placeholders,
            $updates ?: '`UpdatedAt` = VALUES(`UpdatedAt`)'
        );

        try {
            $stmt = $this->pdo->prepare($sql);
            $stmt->execute(array_values($row));
        } catch (PDOException $e) {
            error_log("Materialize {$table}: " . $e->getMessage());
        }
    }
}
