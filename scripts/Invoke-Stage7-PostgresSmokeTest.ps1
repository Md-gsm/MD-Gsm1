[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $PsqlPath,
    [Parameter(Mandatory = $true)] [string] $PostgresHost,
    [int] $PostgresPort = 5432,
    [Parameter(Mandatory = $true)] [string] $AdminUser,
    [string] $ApiProjectPath = '',
    [string] $MigratorProjectPath = ''
)
$ErrorActionPreference = 'Stop'

# This smoke test may create only a new, explicitly named disposable database on loopback.
if ($PostgresHost -notin @('127.0.0.1', 'localhost', '::1')) {
    throw 'Safety stop: this test runner only permits a local PostgreSQL server (127.0.0.1, localhost, or ::1).'
}
if ($PostgresPort -lt 1 -or $PostgresPort -gt 65535) {
    throw 'PostgreSQL port must be between 1 and 65535.'
}

$scriptRoot = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($scriptRoot)) {
    throw 'Could not resolve the script directory. Run this file directly with -File.'
}
if ([string]::IsNullOrWhiteSpace($ApiProjectPath)) {
    $ApiProjectPath = Join-Path $scriptRoot '..\src\MDGsm.Api\MDGsm.Api.csproj'
}
if ([string]::IsNullOrWhiteSpace($MigratorProjectPath)) {
    $MigratorProjectPath = Join-Path $scriptRoot '..\tools\MDGsm.DbMigrator\MDGsm.DbMigrator.csproj'
}
foreach ($path in @($PsqlPath, $ApiProjectPath, $MigratorProjectPath)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Required path not found: $path" }
}

$securePassword = $null
$ptr = [IntPtr]::Zero
$plainPassword = $null
$api = $null
$originalEnv = @{
    PGPASSWORD = $env:PGPASSWORD
    PGCONNECT_TIMEOUT = $env:PGCONNECT_TIMEOUT
    ConnectionStrings__MDGsmDatabase = $env:ConnectionStrings__MDGsmDatabase
    ASPNETCORE_URLS = $env:ASPNETCORE_URLS
}

try {
    $securePassword = Read-Host "Enter PostgreSQL password for local test admin '$AdminUser' (input hidden)" -AsSecureString
    if (-not $securePassword -or $securePassword.Length -eq 0) { throw 'An empty PostgreSQL password was supplied.' }
    $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    if ([string]::IsNullOrEmpty($plainPassword)) { throw 'An empty PostgreSQL password was supplied.' }

    $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $dbName = "mdgsm_stage7_disposable_$stamp"
    if ($dbName -notmatch '^mdgsm_stage7_disposable_[0-9]{8}_[0-9]{6}$') { throw 'Disposable database name guard failed.' }

    $env:PGPASSWORD = $plainPassword
    $env:PGCONNECT_TIMEOUT = '5'
    $baseArgs = @('-h', $PostgresHost, '-p', "$PostgresPort", '-U', $AdminUser, '-d', 'postgres', '-v', 'ON_ERROR_STOP=1')
    & $PsqlPath @baseArgs -c 'SELECT version();'
    if ($LASTEXITCODE -ne 0) { throw 'Could not authenticate to the local PostgreSQL server.' }

    & $PsqlPath @baseArgs -c "CREATE DATABASE `"$dbName`";"
    if ($LASTEXITCODE -ne 0) { throw "Could not create disposable database '$dbName'. It was not dropped automatically." }
    & $PsqlPath @baseArgs -c "COMMENT ON DATABASE `"$dbName`" IS 'MDGSM_DISPOSABLE_TEST_DB; created by Stage7 smoke test';"
    if ($LASTEXITCODE -ne 0) { throw "Could not mark '$dbName' as disposable. Do not use it for any non-test purpose." }

    # Let the platform's connection-string builder escape punctuation correctly.
    $connectionBuilder = [System.Data.Common.DbConnectionStringBuilder]::new()
    $connectionBuilder['Host'] = $PostgresHost
    $connectionBuilder['Port'] = $PostgresPort
    $connectionBuilder['Database'] = $dbName
    $connectionBuilder['Username'] = $AdminUser
    $connectionBuilder['Password'] = $plainPassword
    $connectionBuilder['Timeout'] = 5
    $connectionBuilder['Command Timeout'] = 120
    $env:ConnectionStrings__MDGsmDatabase = $connectionBuilder.ConnectionString

    Write-Host 'Applying pending SQL through the application-owned migration runner...'
    & dotnet run --project $MigratorProjectPath --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Application migration runner failed. Database '$dbName' is retained." }
    $history = & $PsqlPath -h $PostgresHost -p "$PostgresPort" -U $AdminUser -d $dbName -t -A -v ON_ERROR_STOP=1 -c "SELECT count(*)::text || '|' || max(migration_version)::text FROM mdgsm_migration_history;"
    if ($LASTEXITCODE -ne 0 -or "$history".Trim() -ne '1|1') { throw "Schema migration history verification failed. Found: $history. Database '$dbName' is retained." }
    $env:ASPNETCORE_URLS = 'http://127.0.0.1:5080'
    $api = Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--project', ('"' + $ApiProjectPath + '"'), '--no-launch-profile') -PassThru -WindowStyle Hidden
    try {
        $live = $false
        for ($i = 0; $i -lt 30; $i++) {
            Start-Sleep -Seconds 1
            try {
                $liveResponse = Invoke-RestMethod 'http://127.0.0.1:5080/health/live' -TimeoutSec 2
                if ($liveResponse.status -eq 'alive') { $live = $true; break }
            } catch { }
            if ($api.HasExited) { throw "API exited early with code $($api.ExitCode)." }
        }
        if (-not $live) { throw 'API liveness endpoint did not respond within 30 seconds.' }
        $ready = Invoke-RestMethod 'http://127.0.0.1:5080/health/ready' -TimeoutSec 5
        if ($ready.status -ne 'ready') { throw 'API readiness did not report ready.' }
        $info = Invoke-RestMethod 'http://127.0.0.1:5080/api/v1/system/info' -TimeoutSec 5
        if ($info.data.product -ne 'MD-Gsm') { throw 'System info contract returned unexpected product.' }
        Write-Host 'PASS: PostgreSQL connection, infrastructure migration, API liveness/readiness, and system-info endpoint.' -ForegroundColor Green
        Write-Host "Database retained for inspection: $dbName"
    }
    finally {
        if ($api -and -not $api.HasExited) { Stop-Process -Id $api.Id -Force -ErrorAction SilentlyContinue }
    }
}
finally {
    foreach ($name in $originalEnv.Keys) {
        $value = $originalEnv[$name]
        if ($null -eq $value) { Remove-Item "Env:$name" -ErrorAction SilentlyContinue }
        else { Set-Item "Env:$name" $value }
    }
    $plainPassword = $null
    if ($ptr -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr) }
    if ($securePassword) { $securePassword.Dispose() }
}
