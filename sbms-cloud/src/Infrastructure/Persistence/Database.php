<?php

declare(strict_types=1);

namespace Sbms\Cloud\Infrastructure\Persistence;

use PDO;
use PDOException;
use RuntimeException;

final class Database
{
    private static ?PDO $pdo = null;
    private static ?string $lastError = null;

    public static function pdo(): PDO
    {
        if (self::$pdo === null) {
            self::$lastError = null;
            $host = $_ENV['DB_HOST'] ?? 'localhost';
            if ($host === 'localhost') {
                $host = '127.0.0.1';
            }
            $port = $_ENV['DB_PORT'] ?? '3306';
            $name = $_ENV['DB_NAME'] ?? 'sbms_cloud';
            $user = $_ENV['DB_USER'] ?? 'root';
            $pass = $_ENV['DB_PASS'] ?? '';

            $dsn = "mysql:host={$host};port={$port};dbname={$name};charset=utf8mb4";
            try {
                self::$pdo = new PDO($dsn, $user, $pass, [
                    PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
                    PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
                    PDO::ATTR_EMULATE_PREPARES => false,
                ]);
            } catch (PDOException $e) {
                self::$lastError = $e->getMessage();
                throw new RuntimeException('Connexion MySQL impossible : ' . $e->getMessage(), (int) $e->getCode(), $e);
            }
        }

        return self::$pdo;
    }

    public static function lastError(): ?string
    {
        return self::$lastError;
    }
}
