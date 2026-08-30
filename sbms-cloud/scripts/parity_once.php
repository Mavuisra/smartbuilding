<?php

/**
 * Migration parité one-shot (hébergement LWS sans SSH).
 * Déployé à la racine FTP puis supprimé après succès.
 *
 * Accès : /parity_once.php?token=SmartBuilding_Parity_2026
 */

$token = $_GET['token'] ?? '';
if ($token !== 'SmartBuilding_Parity_2026') {
    http_response_code(403);
    header('Content-Type: text/plain; charset=utf-8');
    echo "Forbidden\n";
    exit;
}

header('Content-Type: text/plain; charset=utf-8');
ob_implicit_flush(true);

require __DIR__ . '/scripts/migrate_parity.php';

@unlink(__FILE__);
