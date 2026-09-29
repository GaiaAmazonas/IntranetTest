[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [switch]$Execute,
    [string]$ExpectedEnvironmentHost = 'org16eb6fd8.api.crm2.dynamics.com',
    [string]$Confirmation,
    [ValidateSet('DeviceCode', 'Application')]
    [string]$AuthenticationMethod = 'DeviceCode'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$requiredConfirmation = 'BORRAR DATOS DE PRUEBA HELPDESK'
$projectRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $projectRoot 'src\Gaia.Api\Gaia.Api.csproj'
$developmentSettings = Join-Path $projectRoot 'src\Gaia.Api\appsettings.Development.json'

if (-not (Test-Path -LiteralPath $apiProject)) {
    throw "No se encontró el proyecto de la API en $apiProject."
}

function Get-UserSecrets {
    $json = & dotnet user-secrets list --json --project $apiProject 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($json -join ''))) {
        throw 'No fue posible leer User Secrets para la API.'
    }
    return ($json -join [Environment]::NewLine | ConvertFrom-Json -AsHashtable)
}

function Get-RequiredValue([hashtable]$Secrets, [string]$Name) {
    $value = $Secrets[$Name]
    if ([string]::IsNullOrWhiteSpace([string]$value)) {
        throw "Falta la configuración local requerida: $Name."
    }
    return [string]$value
}

function Invoke-Dataverse([string]$Method, [string]$Path, [string]$Token) {
    $headers = @{
        Authorization = "Bearer $Token"
        Accept = 'application/json'
        'OData-MaxVersion' = '4.0'
        'OData-Version' = '4.0'
    }
    $uri = if ($Path.StartsWith('https://', [StringComparison]::OrdinalIgnoreCase)) { $Path } else { "$script:webApi/$Path" }
    return Invoke-RestMethod -Method $Method -Uri $uri -Headers $headers -ContentType 'application/json'
}

function Get-TableMetadata([string]$LogicalName, [string]$Token) {
    $path = "EntityDefinitions(LogicalName='$LogicalName')?`$select=LogicalName,EntitySetName,PrimaryIdAttribute"
    try { return Invoke-Dataverse -Method Get -Path $path -Token $Token }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -eq 404) { return $null }
        throw
    }
}

function Invoke-OAuthForm([string]$Uri, [hashtable]$Values) {
    $pairs = [System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string,string]]]::new()
    foreach ($entry in $Values.GetEnumerator()) {
        $pairs.Add([System.Collections.Generic.KeyValuePair[string,string]]::new([string]$entry.Key, [string]$entry.Value))
    }
    $content = [System.Net.Http.FormUrlEncodedContent]::new($pairs)
    $client = [System.Net.Http.HttpClient]::new()
    try {
        $response = $client.PostAsync($Uri, $content).GetAwaiter().GetResult()
        $json = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) { throw "OAuth rechazó la solicitud ($([int]$response.StatusCode)): $json" }
        return ($json | ConvertFrom-Json)
    }
    finally {
        $content.Dispose()
        $client.Dispose()
    }
}

function Get-DeviceCodeToken([string]$TenantId, [string]$ClientId, [string]$Scope) {
    $device = Invoke-OAuthForm -Uri "https://login.microsoftonline.com/$TenantId/oauth2/v2.0/devicecode" -Values @{
        client_id = $ClientId
        scope = "$Scope offline_access openid profile"
    }
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds([int]$device.expires_in)
    $announced = $false
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        try {
            $tokenJson = & curl.exe -sS -X POST "https://login.microsoftonline.com/$TenantId/oauth2/v2.0/token" `
                --data-urlencode 'grant_type=urn:ietf:params:oauth:grant-type:device_code' `
                --data-urlencode "client_id=$ClientId" `
                --data-urlencode "client_secret=$script:oauthClientSecret" `
                --data-urlencode "device_code=$($device.device_code)"
            if ($LASTEXITCODE -ne 0) { throw 'No fue posible canjear el código de dispositivo.' }
            $token = $tokenJson | ConvertFrom-Json
            if ($token.error) { throw ($tokenJson -join [Environment]::NewLine) }
            if ($token.access_token) { return [string]$token.access_token }
        }
        catch {
            $body = $_.Exception.Message
            if ($body -match 'authorization_pending') {
                if (-not $announced) {
                    Write-Host $device.message -ForegroundColor Yellow
                    Write-Host "DEVICE_CODE_READY user_code=$($device.user_code) verification_uri=$($device.verification_uri)" -ForegroundColor Cyan
                    $announced = $true
                }
                Start-Sleep -Seconds ([Math]::Max(5, [int]$device.interval))
                continue
            }
            if ($body -match 'slow_down') { Start-Sleep -Seconds 5; continue }
            throw
        }
    }
    throw 'El código de dispositivo expiró antes de completar el inicio de sesión.'
}

function Get-AllIds($Metadata, [string]$Token) {
    $ids = [System.Collections.Generic.List[string]]::new()
    $next = "$($Metadata.EntitySetName)?`$select=$($Metadata.PrimaryIdAttribute)&`$orderby=$($Metadata.PrimaryIdAttribute)"
    while ($next) {
        $page = Invoke-Dataverse -Method Get -Path $next -Token $Token
        foreach ($row in $page.value) { $ids.Add([string]$row.($Metadata.PrimaryIdAttribute)) }
        $next = $page.'@odata.nextLink'
    }
    return $ids
}

$secrets = Get-UserSecrets
$tenantId = Get-RequiredValue $secrets 'MicrosoftEntra:TenantId'
$clientId = Get-RequiredValue $secrets 'MicrosoftEntra:ClientId'
$clientSecret = Get-RequiredValue $secrets 'MicrosoftEntra:ClientSecret'
$script:oauthClientSecret = $clientSecret
$script:webApi = (Get-RequiredValue $secrets 'Dataverse:WebApiEndpoint').TrimEnd('/')
$environmentUrl = (Get-RequiredValue $secrets 'Dataverse:EnvironmentUrl').TrimEnd('/')
$delegatedScope = Get-RequiredValue $secrets 'Dataverse:Scope'
$webApiUri = [Uri]$script:webApi

if ($webApiUri.Host -ne $ExpectedEnvironmentHost) {
    throw "Protección de ambiente: se esperaba $ExpectedEnvironmentHost y se encontró $($webApiUri.Host)."
}
if ((Get-Content -LiteralPath $developmentSettings -Raw | ConvertFrom-Json).Logging.LogLevel.Default -ne 'Information') {
    throw 'No se pudo validar la configuración Development del proyecto.'
}

$accessToken = if ($AuthenticationMethod -eq 'DeviceCode') {
    Get-DeviceCodeToken -TenantId $tenantId -ClientId $clientId -Scope $delegatedScope
}
else {
    $tokenResponse = Invoke-OAuthForm -Uri "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token" -Values @{
        client_id = $clientId
        client_secret = $clientSecret
        scope = "$environmentUrl/.default"
        grant_type = 'client_credentials'
    }
    [string]$tokenResponse.access_token
}
if ([string]::IsNullOrWhiteSpace($accessToken)) { throw 'Microsoft Entra no devolvió un token para Dataverse.' }

# Datos transaccionales y configuración creada de Helpdesk. Se conservan los catálogos estructurales.
$deleteOrder = @(
    'gaia_adjuntosolicitud',
    'gaia_respuestaopciongestion',
    'gaia_respuestacampogestion',
    'gaia_respuestaopcioncampo',
    'gaia_respuestacampo',
    'gaia_dependenciagestion',
    'gaia_historialsolicitud',
    'gaia_comentariosolicitud',
    'gaia_calificacionsolicitud',
    'gaia_gestionsolicitud',
    'gaia_instanciaflujo',
    'gaia_solicitud',
    'gaia_opcioncampoformulariopaso',
    'gaia_campoformulariopaso',
    'gaia_formulariopaso',
    'gaia_rutaflujo',
    'gaia_pasoflujo',
    'gaia_flujogestion',
    'gaia_opcioncampoformulario',
    'gaia_campoformulario',
    'gaia_formularioservicio',
    'gaia_servicio'
)

$inventory = [System.Collections.Generic.List[object]]::new()
foreach ($logicalName in $deleteOrder) {
    $metadata = Get-TableMetadata -LogicalName $logicalName -Token $accessToken
    if ($null -eq $metadata) {
        $inventory.Add([pscustomobject]@{ Table = $logicalName; EntitySet = '(no existe)'; Count = 0; Metadata = $null; Ids = @() })
        continue
    }
    $ids = @(Get-AllIds -Metadata $metadata -Token $accessToken)
    $inventory.Add([pscustomobject]@{ Table = $logicalName; EntitySet = $metadata.EntitySetName; Count = $ids.Count; Metadata = $metadata; Ids = $ids })
}

Write-Host "Ambiente validado: $($webApiUri.Host)" -ForegroundColor Cyan
$inventory | Select-Object Table, EntitySet, Count | Format-Table -AutoSize
$total = ($inventory | Measure-Object -Property Count -Sum).Sum
Write-Host "Total de registros de datos y configuración encontrados: $total" -ForegroundColor Yellow

if (-not $Execute) {
    Write-Host 'Vista previa completada. No se eliminó ningún registro.' -ForegroundColor Green
    Write-Host "Para ejecutar: .\scripts\Reset-HelpdeskTestData.ps1 -Execute -Confirmation '$requiredConfirmation'" -ForegroundColor DarkGray
    exit 0
}
if ($Confirmation -cne $requiredConfirmation) {
    throw "Confirmación inválida. Debe escribir exactamente: $requiredConfirmation"
}

foreach ($entry in $inventory) {
    if ($entry.Count -eq 0) { continue }
    Write-Host "Eliminando $($entry.Count) registro(s) de $($entry.Table)..." -ForegroundColor Yellow
    foreach ($id in $entry.Ids) {
        $resource = "$($entry.EntitySet)($id)"
        if ($PSCmdlet.ShouldProcess($resource, 'DELETE físico en Dataverse')) {
            Invoke-Dataverse -Method Delete -Path $resource -Token $accessToken | Out-Null
        }
    }
}

Write-Host 'Eliminación terminada. Verificando datos y configuración de Helpdesk...' -ForegroundColor Cyan
$remaining = 0
foreach ($entry in $inventory) {
    if ($null -eq $entry.Metadata) { continue }
    $count = @(Get-AllIds -Metadata $entry.Metadata -Token $accessToken).Count
    $remaining += $count
    Write-Host "$($entry.Table): $count"
}
if ($remaining -ne 0) { throw "La limpieza terminó con $remaining registro(s) pendientes." }
Write-Host 'Helpdesk quedó sin solicitudes ni configuración creada. Se conservaron las tablas y catálogos estructurales.' -ForegroundColor Green
