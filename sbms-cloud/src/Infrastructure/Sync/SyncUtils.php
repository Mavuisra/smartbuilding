<?php

declare(strict_types=1);

namespace Sbms\Cloud\Infrastructure\Sync;

use DateTimeImmutable;
use DateTimeInterface;
use DateTimeZone;

final class SyncUtils
{
    public const MIN_SYNC_DATETIME = '1970-01-01T00:00:00+00:00';

    public static function pick(array $data, string ...$keys): mixed
    {
        foreach ($keys as $key) {
            if (array_key_exists($key, $data)) {
                return $data[$key];
            }
        }
        return null;
    }

    public static function parseUuid(mixed $value): ?string
    {
        if ($value === null) {
            return null;
        }
        return (string) $value;
    }

    public static function parseDatetime(mixed $value): ?DateTimeImmutable
    {
        if ($value === null) {
            return null;
        }
        if ($value instanceof DateTimeInterface) {
            return DateTimeImmutable::createFromInterface($value);
        }
        $text = str_replace('Z', '+00:00', (string) $value);
        try {
            return new DateTimeImmutable($text);
        } catch (\Exception) {
            return null;
        }
    }

    public static function normalizeSyncDatetime(mixed $value, ?DateTimeImmutable $default = null): ?DateTimeImmutable
    {
        $dt = self::parseDatetime($value);
        if ($dt === null) {
            return $default;
        }
        $utc = $dt->setTimezone(new DateTimeZone('UTC'));
        $min = new DateTimeImmutable(self::MIN_SYNC_DATETIME);
        if ($utc < $min) {
            return $default ?? $min;
        }
        return $utc;
    }

    public static function parseDate(mixed $value): ?string
    {
        $dt = self::normalizeSyncDatetime($value);
        return $dt?->format('Y-m-d');
    }

    public static function parseDecimal(mixed $value, float $default = 0.0): string
    {
        if ($value === null) {
            return number_format($default, 2, '.', '');
        }
        if (!is_numeric($value)) {
            return number_format($default, 2, '.', '');
        }
        return number_format((float) $value, 2, '.', '');
    }

    public static function injectEntityId(?array $data, string $entityId): array
    {
        $payload = is_array($data) ? $data : [];
        if (!self::pick($payload, 'Id', 'id')) {
            $payload['Id'] = $entityId;
            $payload['id'] = $entityId;
        }
        return $payload;
    }

    public static function parseBool(mixed $value, bool $default = false): bool
    {
        if (is_bool($value)) {
            return $value;
        }
        if ($value === null) {
            return $default;
        }
        if (is_string($value)) {
            return in_array(strtolower($value), ['true', '1', 'yes'], true);
        }
        return (bool) $value;
    }

    public static function parseInt(mixed $value, int $default = 0): int
    {
        if (is_int($value)) {
            return $value;
        }
        if (is_numeric($value)) {
            return (int) $value;
        }
        return $default;
    }

    public static function mergeSyncPayload(?array $existing, array $incoming): array
    {
        if (!$existing) {
            return $incoming;
        }
        $merged = array_merge($existing, $incoming);
        foreach ($incoming as $key => $value) {
            if (self::isEmptySyncValue($value) && !self::isEmptySyncValue($existing[$key] ?? null)) {
                $merged[$key] = $existing[$key];
            }
        }
        return $merged;
    }

    private static function isEmptySyncValue(mixed $value): bool
    {
        if ($value === null) {
            return true;
        }
        if (is_string($value)) {
            return trim($value) === '';
        }
        if (is_array($value)) {
            return count($value) === 0;
        }
        return false;
    }

    public static function toMysqlDatetime(?DateTimeImmutable $dt): ?string
    {
        return $dt?->format('Y-m-d H:i:s');
    }

    public static function nowUtc(): DateTimeImmutable
    {
        return new DateTimeImmutable('now', new DateTimeZone('UTC'));
    }

    public static function isoZ(DateTimeImmutable $dt): string
    {
        return $dt->setTimezone(new DateTimeZone('UTC'))->format('Y-m-d\TH:i:s\Z');
    }
}
