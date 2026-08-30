# Prepare flat LWS upload folder for FileZilla
# Usage: .\scripts\prepare-lws-deploy.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$out = Join-Path (Split-Path -Parent $root) "deploy\lws-sbms-upload"

Write-Host "Source: $root"
Write-Host "Target: $out"

if (Test-Path $out) { Remove-Item -Recurse -Force $out }
New-Item -ItemType Directory -Force -Path $out | Out-Null

$dirs = @("bootstrap", "src", "templates", "migrations", "scripts", "vendor")
foreach ($d in $dirs) {
    Copy-Item -Recurse -Force (Join-Path $root $d) (Join-Path $out $d)
}

Copy-Item (Join-Path $root "composer.json") $out
Copy-Item (Join-Path $root "composer.lock") $out
Copy-Item (Join-Path $root "public\.htaccess") $out

$indexPhp = @'
<?php

use Slim\App;

require __DIR__ . '/vendor/autoload.php';

/** @var App $app */
$app = require __DIR__ . '/bootstrap/app.php';
$app->run();

'@
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText((Join-Path $out "index.php"), $indexPhp, $utf8NoBom)

Copy-Item (Join-Path $root "scripts\parity_once.php") (Join-Path $out "parity_once.php") -Force
Copy-Item (Join-Path $root ".env.example") (Join-Path $out ".env") -Force

Write-Host ""
Write-Host "Ready: $out"
Write-Host "Upload this folder contents to FTP root (ftp.lasaveur.store)"
Write-Host "Delete default_index.html on server after upload"
