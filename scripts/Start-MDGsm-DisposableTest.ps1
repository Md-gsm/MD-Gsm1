[CmdletBinding()]
param(
    [string] $PsqlPath = 'C:\Program Files\PostgreSQL\18\bin\psql.exe',
    [string] $PostgresHost = '127.0.0.1',
    [int] $PostgresPort = 5432,
    [string] $AdminUser = 'postgres',
    [string] $ApiProjectPath = '',
    [string] $DesktopProjectPath = '',
    [string] $MigrationPath = ''
)
$ErrorActionPreference = 'Stop'

# Resolve defaults only after param binding; $PSScriptRoot is reliable here.
$scriptRoot = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptRoot)) {
    throw 'Could not resolve the script directory. Run this file directly with -File.'
}
if ([string]::IsNullOrWhiteSpace($ApiProjectPath)) {
    $ApiProjectPath = Join-Path $scriptRoot '..\src\MDGsm.Api\MDGsm.Api.csproj'
}
if ([string]::IsNullOrWhiteSpace($DesktopProjectPath)) {
    $DesktopProjectPath = Join-Path $scriptRoot '..\src\MDGsm.Desktop\MDGsm.Desktop.csproj'
}
if ([string]::IsNullOrWhiteSpace($MigrationPath)) {
    $MigrationPath = Join-Path $scriptRoot '..\database\migrations\0001_infrastructure_schema_version.sql'
}


# Safety guard: this convenience runner must never target a remote server.
if ($PostgresHost -notin @('127.0.0.1', 'localhost', '::1')) {
    throw 'Safety stop: this test runner only permits a local PostgreSQL server (127.0.0.1, localhost, or ::1).'
}
foreach ($path in @($PsqlPath, $ApiProjectPath, $DesktopProjectPath, $MigrationPath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required path not found: $path" }
}

$securePassword = Read-Host "Enter PostgreSQL password for local test admin '$AdminUser' (input hidden)" -AsSecureString
$ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
$plainPassword = $null
$api = $null
$desktop = $null
$originalEnv = @{
    PGPASSWORD = $env:PGPASSWORD
    PGCONNECT_TIMEOUT = $env:PGCONNECT_TIMEOUT
    ConnectionStrings__MDGsmDatabase = $env:ConnectionStrings__MDGsmDatabase
    ASPNETCORE_URLS = $env:ASPNETCORE_URLS
}
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$dbName = "mdgsm_stage7_disposable_$stamp"
if ($dbName -notmatch '^mdgsm_stage7_disposable_[0-9]{8}_[0-9]{6}$') { throw 'Disposable database name guard failed.' }
$apiUrl = 'http://127.0.0.1:5080'

try {
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    if ([string]::IsNullOrEmpty($plainPassword)) { throw 'An empty PostgreSQL password was supplied.' }
    $env:PGPASSWORD = $plainPassword
    $env:PGCONNECT_TIMEOUT = '5'

    Write-Host "Checking local PostgreSQL $PostgresHost`:$PostgresPort ..."
    & $PsqlPath -h $PostgresHost -p "$PostgresPort" -U $AdminUser -d postgres -v ON_ERROR_STOP=1 -c 'SELECT version();'
    if ($LASTEXITCODE -ne 0) { throw 'Could not authenticate to the local PostgreSQL server.' }

    Write-Host "Creating a new disposable database: $dbName"
    # Name is generated internally and constrained by the guard above; no user-provided SQL identifier is used.
    & $PsqlPath -h $PostgresHost -p "$PostgresPort" -U $AdminUser -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE `"$dbName`";"
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the new disposable database. It is not dropped automatically.' }
    & $PsqlPath -h $PostgresHost -p "$PostgresPort" -U $AdminUser -d postgres -v ON_ERROR_STOP=1 -c "COMMENT ON DATABASE `"$dbName`" IS 'MDGSM_DISPOSABLE_TEST_DB; created by Stage7 test runner';"
    if ($LASTEXITCODE -ne 0) { throw "Could not mark '$dbName' as disposable. Stop the test and inspect the new database." }

    Write-Host 'Applying the project-owned infrastructure migration...'
    $migrationSql = Get-Content -LiteralPath $MigrationPath -Raw
    $migrationSql | & $PsqlPath -h $PostgresHost -p "$PostgresPort" -U $AdminUser -d $dbName -v ON_ERROR_STOP=1
    if ($LASTEXITCODE -ne 0) { throw "Migration failed. The database '$dbName' is intentionally retained for inspection." }

    $version = & $PsqlPath -h $PostgresHost -p "$PostgresPort" -U $AdminUser -d $dbName -t -A -v ON_ERROR_STOP=1 -c 'SELECT version FROM mdgsm_schema_version ORDER BY version DESC LIMIT 1;'
    if ($LASTEXITCODE -ne 0 -or "$version".Trim() -ne '1') { throw "Migration verification failed. Found version: $version. Database '$dbName' is retained." }
    Write-Host "PASS: migration version 1 verified in $dbName" -ForegroundColor Green

    # Test-only connection string. Production must use a least-privilege runtime role and managed secret.
    $env:ConnectionStrings__MDGsmDatabase = "Host=$PostgresHost;Port=$PostgresPort;Database=$dbName;Username=$AdminUser;Password=$plainPassword;Timeout=5;Command Timeout=5"
    $env:ASPNETCORE_URLS = $apiUrl
    Write-Host 'Starting API with the new disposable database connection...'
    $api = Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--project', ('"' + $ApiProjectPath + '"'), '--no-launch-profile') -PassThru -WindowStyle Hidden

    $live = $false
    for ($i = 0; $i -lt 45; $i++) {
        Start-Sleep -Seconds 1
        try {
            $liveResult = Invoke-RestMethod "$apiUrl/health/live" -TimeoutSec 2
            if ($liveResult.status -eq 'alive') { $live = $true; break }
        } catch { }
        if ($api.HasExited) { throw "API exited before becoming live (exit code $($api.ExitCode))." }
    }
    if (-not $live) { throw 'API did not become live within 45 seconds.' }

    $readyResult = Invoke-RestMethod "$apiUrl/health/ready" -TimeoutSec 5
    if ($readyResult.status -ne 'ready') { throw 'API readiness did not report ready for PostgreSQL.' }
    $infoResult = Invoke-RestMethod "$apiUrl/api/v1/system/info" -TimeoutSec 5
    if ($infoResult.data.product -ne 'MD-Gsm') { throw 'System info endpoint returned an unexpected product.' }
    Write-Host 'PASS: API liveness, PostgreSQL readiness, and system-info endpoint.' -ForegroundColor Green
    Write-Host "Database retained for inspection: $dbName"
    Write-Host 'Launching MD-Gsm Desktop. Click its API connection test button; the API will stay running until the Desktop process exits.'

    $desktop = Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--project', ('"' + $DesktopProjectPath + '"'), '--no-launch-profile') -PassThru
    $desktop.WaitForExit()
    if ($desktop.ExitCode -ne 0) { throw "Desktop process exited with code $($desktop.ExitCode)." }
    Write-Host 'Desktop test session finished.' -ForegroundColor Green
}
finally {
    if ($desktop -and -not $desktop.HasExited) { Stop-Process -Id $desktop.Id -Force -ErrorAction SilentlyContinue }
    if ($api -and -not $api.HasExited) { Stop-Process -Id $api.Id -Force -ErrorAction SilentlyContinue }
    foreach ($name in $originalEnv.Keys) {
        $value = $originalEnv[$name]
        if ($null -eq $value) { Remove-Item "Env:$name" -ErrorAction SilentlyContinue }
        else { Set-Item "Env:$name" $value }
    }
    if ($null -ne $plainPassword) { $plainPassword = $null }
    if ($ptr -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
    if ($securePassword) { $securePassword.Dispose() }
}
