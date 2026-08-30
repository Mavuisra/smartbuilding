# Upload fiable vers LWS — retry + verification taille
param(
    [string[]]$Files
)

$ErrorActionPreference = 'Stop'
$pass = 'uB5_hC7QBQt!qPD'
$user = '2675681Mx8dpr'
$repoRoot = Split-Path (Split-Path $PSScriptRoot)
$base = Join-Path $repoRoot 'deploy\lws-sbms-upload'
$base = (Resolve-Path $base).Path

function Send-FtpFile {
    param([string]$LocalPath, [string]$RemoteRel)
    $localSize = (Get-Item $LocalPath).Length
    $url = "ftp://ftp.lasaveur.store/$RemoteRel"
    for ($attempt = 1; $attempt -le 5; $attempt++) {
        curl.exe --ftp-pasv --connect-timeout 60 --max-time 900 -s `
            -u "${user}:$pass" --ftp-create-dirs -T $LocalPath $url | Out-Null
        if ($LASTEXITCODE -ne 0) {
            Write-Host "  tentative $attempt : curl exit $LASTEXITCODE"
            Start-Sleep -Seconds (5 * $attempt)
            continue
        }
        $tmp = Join-Path $env:TEMP ("ftp-verify-" + [Guid]::NewGuid().ToString('N'))
        curl.exe --ftp-pasv --connect-timeout 60 --max-time 120 -s `
            -u "${user}:$pass" $url -o $tmp | Out-Null
        if ($LASTEXITCODE -eq 0 -and (Test-Path $tmp)) {
            $remoteSize = (Get-Item $tmp).Length
            Remove-Item $tmp -Force -ErrorAction SilentlyContinue
            if ($remoteSize -eq $localSize) {
                return $true
            }
            Write-Host "  tentative $attempt : taille remote=$remoteSize local=$localSize"
        }
        Start-Sleep -Seconds (5 * $attempt)
    }
    return $false
}

if (-not $Files -or $Files.Count -eq 0) {
    $Files = @(
        'bootstrap/container.php',
        'bootstrap/app.php',
        'index.php',
        'scripts/migrate_parity.php',
        'scripts/parity_once.php',
        'migrations/002_desktop_full_schema.sql',
        'migrations/003_cloud_extensions.sql',
        'migrations/004_organizations_multitenant.sql',
        'src/Infrastructure/Persistence/OrganizationRepository.php',
        'src/Infrastructure/Security/OrganizationContext.php',
        'src/Presentation/Http/Controllers/OrganizationController.php',
        'src/Application/Auth/LoginUseCase.php',
        'src/Infrastructure/Persistence/SyncStoreRepository.php',
        'src/Infrastructure/Sync/SyncRegistry.php',
        'src/Presentation/Http/Controllers/SyncController.php'
    )
}

$failed = @()
foreach ($rel in $Files) {
    $local = Join-Path $base ($rel -replace '/', '\')
    if (-not (Test-Path $local)) {
        Write-Host "MISSING $rel"
        $failed += $rel
        continue
    }
    Write-Host "Upload $rel ..."
    if (Send-FtpFile -LocalPath $local -RemoteRel ($rel -replace '\\','/')) {
        Write-Host "  OK"
    } else {
        Write-Host "  FAIL"
        $failed += $rel
    }
}

if ($failed.Count -gt 0) {
    Write-Host "Echecs: $($failed -join ', ')"
    exit 1
}
Write-Host 'Tous les fichiers verifies OK.'
