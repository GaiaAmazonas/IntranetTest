$ErrorActionPreference = 'Stop'

$violations = [System.Collections.Generic.List[string]]::new()
$tracked = @(git ls-files)

$tracked | Where-Object { $_ -match '(^|/)App_Data/' -and $_ -notmatch '/\.gitkeep$' } | ForEach-Object {
    $violations.Add("Archivo operativo versionado: $_")
}
$tracked | Where-Object {
    $_ -match '(^|/)(\.env($|\.)|secrets\.json$)|\.(pfx|p12|key|pem)$' -and
    $_ -notmatch '(^|/)\.env\.example$'
} | ForEach-Object {
    $violations.Add("Archivo sensible versionado: $_")
}

$configuration = Get-Content (Join-Path $PSScriptRoot '../src/Gaia.Api/appsettings.json') -Raw
if ($configuration -match '"AllowedHosts"\s*:\s*"\*"') { $violations.Add('AllowedHosts no puede aceptar cualquier host.') }
if ($configuration -match 'admin@example\.org') { $violations.Add('La configuración base contiene una identidad privilegiada de ejemplo.') }

Get-ChildItem (Join-Path $PSScriptRoot '../.github/workflows') -Filter '*.yml' | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    if ($content -match 'uses:\s*[^\s]+@v\d') { $violations.Add("$($_.Name) usa una acción sin SHA inmutable.") }
}

$demoArchive = Join-Path $PSScriptRoot '../entregables/Gaia-Demo-Portable.zip'
$demoChecksum = Join-Path $PSScriptRoot '../entregables/Gaia-Demo-Portable.zip.sha256'
if (-not (Test-Path $demoArchive) -or -not (Test-Path $demoChecksum)) {
    $violations.Add('El demo portable debe publicarse junto con su checksum SHA-256.')
} else {
    $expectedHash = ((Get-Content $demoChecksum -Raw).Trim() -split '\s+')[0]
    $actualHash = (Get-FileHash $demoArchive -Algorithm SHA256).Hash
    if ($actualHash -ne $expectedHash) { $violations.Add('El checksum del demo portable no coincide con el ZIP versionado.') }
}

if ($violations.Count -gt 0) {
    $violations | ForEach-Object { Write-Error $_ }
    exit 1
}
Write-Host 'Línea base de seguridad verificada.'
