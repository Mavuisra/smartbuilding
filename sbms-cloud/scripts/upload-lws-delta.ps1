# Upload delta SBMS cloud to LWS FTP
$ErrorActionPreference = 'Stop'
$pass = 'uB5_hC7QBQt!qPD'
$user = '2675681Mx8dpr'
$base = 'c:\Users\hp\Music\smartbuilding-main\smartbuilding-main\deploy\lws-sbms-upload'
$root = Join-Path $base ''
$patterns = @(
  'index.php','parity_once.php','.env','.htaccess',
  'bootstrap\*','src\*','migrations\*','scripts\*'
)
$files = @()
foreach ($p in $patterns) {
  $files += Get-ChildItem -Path (Join-Path $base $p) -Recurse -File -Force -ErrorAction SilentlyContinue
}
$files = $files | Sort-Object FullName -Unique
$i = 0
foreach ($f in $files) {
  $i++
  $rel = $f.FullName.Substring($base.Length + 1).Replace('\','/')
  curl.exe -s -u "${user}:$pass" --ftp-create-dirs -T $f.FullName "ftp://ftp.lasaveur.store/$rel" | Out-Null
  if ($LASTEXITCODE -ne 0) { Write-Host "FAIL $rel"; exit 1 }
  if ($i % 25 -eq 0) { Write-Host "$i / $($files.Count)" }
}
Write-Host "Uploaded $($files.Count) files"
