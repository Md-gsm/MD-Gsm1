[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'

# EDB PostgreSQL 18.6-5 Windows x64 installer. EDB confirmed this SHA-256:
# https://github.com/EnterpriseDB/edb-installers/issues/746
$installerUrl = 'https://get.enterprisedb.com/postgresql/postgresql-18.6-5-windows-x64.exe'
$expectedSha256 = '227e2bc08091b4c2b8338c7d61b11d057f895711480b006f90bfcfcd2410a58a'
$port = 55432
$serviceName = 'mdgsm-postgresql-x64-18-ci'
$installRoot = 'C:\MDGsm-CI\PostgreSQL\18'
$dataDirectory = Join-Path $installRoot 'data'
$downloadDirectory = Join-Path $env:RUNNER_TEMP 'mdgsm-postgresql18-installer'
$installerPath = Join-Path $downloadDirectory 'postgresql-18.6-5-windows-x64.exe'
$runId = if ($env:GITHUB_RUN_ID -match '^[0-9]+$') { $env:GITHUB_RUN_ID } else { '1' }
$runAttempt = if ($env:GITHUB_RUN_ATTEMPT -match '^[0-9]+$') { $env:GITHUB_RUN_ATTEMPT } else { '1' }
$databaseName = "mdgsm_ci_disposable_{0}_{1}" -f $runId, $runAttempt
if ($databaseName -notmatch '^mdgsm_ci_disposable_[0-9]+_[0-9]+$') { throw 'Disposable database name guard failed.' }

# Independent credentials for this isolated hosted-runner instance.
# The EDB installer receives them through a temporary ACL-restricted option file,
# not command-line arguments. The file is deleted immediately after installation.
$databasePassword = 'MdGsmDb' + [Guid]::NewGuid().ToString('N')
$servicePassword = 'MdGsmSvc' + [Guid]::NewGuid().ToString('N')
$optionFilePath = Join-Path $downloadDirectory 'postgresql-installer-options.txt'
$previousPgPassword = $env:PGPASSWORD
$previousConnectionString = $env:ConnectionStrings__MDGsmDatabase
$databaseCreated = $false
$psqlPath = Join-Path $installRoot 'bin\psql.exe'
$pgIsReadyPath = Join-Path $installRoot 'bin\pg_isready.exe'

try {
    New-Item -ItemType Directory -Path $downloadDirectory -Force | Out-Null
    Write-Host 'Downloading the pinned PostgreSQL 18.6-5 Windows installer...'
    Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
    $actualSha256 = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualSha256 -ne $expectedSha256) {
        throw "PostgreSQL installer SHA-256 mismatch. Expected $expectedSha256; received $actualSha256."
    }

    if (Test-Path -LiteralPath $installRoot) {
        throw "PostgreSQL installation directory already exists on this runner: $installRoot"
    }

    # Create an empty response file, lock down its ACL, and only then write per-run values.
    $file = [System.IO.File]::Create($optionFilePath)
    $file.Dispose()
    $currentUserSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
    $fileAcl = New-Object System.Security.AccessControl.FileSecurity
    $fileAcl.SetOwner($currentUserSid)
    $fileAcl.SetAccessRuleProtection($true, $false)
    $ownerRule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        $currentUserSid,
        [System.Security.AccessControl.FileSystemRights]::FullControl,
        [System.Security.AccessControl.AccessControlType]::Allow
    )
    $fileAcl.AddAccessRule($ownerRule)
    Set-Acl -LiteralPath $optionFilePath -AclObject $fileAcl

    $installerOptions = @(
        'mode=unattended',
        'unattendedmodeui=none',
        'superaccount=postgres',
        "superpassword=$databasePassword",
        'serviceaccount=postgres',
        "servicepassword=$servicePassword",
        "serverport=$port",
        "prefix=$installRoot",
        "datadir=$dataDirectory",
        "servicename=$serviceName",
        'enable-components=server,commandlinetools',
        'enable_acledit=1'
    )
    [System.IO.File]::WriteAllLines($optionFilePath, $installerOptions, [System.Text.Encoding]::ASCII)

    Write-Host 'Installing the verified PostgreSQL 18 server and command-line tools using a protected option file...'
    $installerProcess = Start-Process -FilePath $installerPath -ArgumentList @('--mode', 'unattended', '--unattendedmodeui', 'none', '--optionfile', $optionFilePath) -Wait -PassThru -NoNewWindow
    if (Test-Path -LiteralPath $optionFilePath) {
        Remove-Item -LiteralPath $optionFilePath -Force
    }
    if ($installerProcess.ExitCode -ne 0) {
        throw "PostgreSQL installer exited with code $($installerProcess.ExitCode)."
    }

    $service = Get-Service -Name $serviceName -ErrorAction Stop
    if ($service.Status -ne 'Running') { Start-Service -Name $serviceName }
    if (-not (Test-Path -LiteralPath $psqlPath) -or -not (Test-Path -LiteralPath $pgIsReadyPath)) {
        throw 'PostgreSQL 18 command-line tools were not found after installation.'
    }

    $env:PGPASSWORD = $databasePassword
    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        & $pgIsReadyPath -h 127.0.0.1 -p "$port" -U postgres -d postgres | Out-Null
        if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        Start-Sleep -Seconds 2
    }
    if (-not $ready) { throw 'PostgreSQL 18 Windows service did not become ready within 120 seconds.' }

    $version = & $psqlPath -h 127.0.0.1 -p "$port" -U postgres -d postgres -t -A -v ON_ERROR_STOP=1 -c 'SHOW server_version;'
    if ($LASTEXITCODE -ne 0 -or "$version".Trim() -notmatch '^18\.') {
        throw "Expected a PostgreSQL 18 server; received '$version'."
    }
    Write-Host "PostgreSQL server verified: $("$version".Trim())"

    & $psqlPath -h 127.0.0.1 -p "$port" -U postgres -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE $databaseName;"
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the uniquely named disposable PostgreSQL database.' }
    $databaseCreated = $true
    & $psqlPath -h 127.0.0.1 -p "$port" -U postgres -d postgres -v ON_ERROR_STOP=1 -c "COMMENT ON DATABASE $databaseName IS 'MDGSM_DISPOSABLE_TEST_DB; PostgreSQL 18 Windows CI';"
    if ($LASTEXITCODE -ne 0) { throw 'Could not apply the disposable database marker.' }

    # For application migrations, the connection string is supplied only through process environment.
    $env:ConnectionStrings__MDGsmDatabase = "Host=127.0.0.1;Port=$port;Database=$databaseName;Username=postgres;Password=$databasePassword;Timeout=15;Command Timeout=120"
    $migratorProject = Join-Path $PSScriptRoot '..\tools\MDGsm.DbMigrator\MDGsm.DbMigrator.csproj'
    if (-not (Test-Path -LiteralPath $migratorProject)) { throw "Application migrator project not found: $migratorProject" }

    Write-Host 'Executing the application-owned migration runner for the first time...'
    $firstRun = @(& dotnet run --project $migratorProject --configuration Release 2>&1)
    $firstExitCode = $LASTEXITCODE
    $firstRun | ForEach-Object { Write-Host $_ }
    if ($firstExitCode -ne 0 -or (($firstRun -join "`n") -notmatch 'newlyApplied=1')) {
        throw 'The first application migration run did not apply exactly one migration.'
    }

    Write-Host 'Re-running the application-owned migration runner to verify idempotence and checksum validation...'
    $secondRun = @(& dotnet run --project $migratorProject --configuration Release 2>&1)
    $secondExitCode = $LASTEXITCODE
    $secondRun | ForEach-Object { Write-Host $_ }
    if ($secondExitCode -ne 0 -or (($secondRun -join "`n") -notmatch 'newlyApplied=0')) {
        throw 'The second application migration run was not idempotent.'
    }

    $history = & $psqlPath -h 127.0.0.1 -p "$port" -U postgres -d $databaseName -t -A -v ON_ERROR_STOP=1 -c "SELECT count(*)::text || '|' || max(migration_version)::text FROM mdgsm_migration_history;"
    if ($LASTEXITCODE -ne 0 -or "$history".Trim() -ne '1|1') { throw "Migration history verification failed: '$history'." }
    $schemaVersion = & $psqlPath -h 127.0.0.1 -p "$port" -U postgres -d $databaseName -t -A -v ON_ERROR_STOP=1 -c "SELECT count(*)::text || '|' || max(version)::text FROM mdgsm_schema_version;"
    if ($LASTEXITCODE -ne 0 -or "$schemaVersion".Trim() -ne '1|1') { throw "Infrastructure schema version verification failed: '$schemaVersion'." }

    Write-Host 'PASS: PostgreSQL 18 on Windows, EDB installer checksum, disposable database marker, app-owned migrations, checksum history and second-run idempotence.' -ForegroundColor Green
}
finally {
    if ($databaseCreated -and (Test-Path -LiteralPath $psqlPath) -and $databaseName -match '^mdgsm_ci_disposable_[0-9]+_[0-9]+$') {
        try {
            & $psqlPath -h 127.0.0.1 -p "$port" -U postgres -d postgres -v ON_ERROR_STOP=1 -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$databaseName' AND pid <> pg_backend_pid();" | Out-Null
            & $psqlPath -h 127.0.0.1 -p "$port" -U postgres -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS $databaseName;" | Out-Null
        } catch {
            Write-Warning 'Disposable PostgreSQL database cleanup did not complete; the hosted runner VM is temporary.'
        }
    }

    if ($null -eq $previousPgPassword) { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }
    else { $env:PGPASSWORD = $previousPgPassword }
    if ($null -eq $previousConnectionString) { Remove-Item Env:ConnectionStrings__MDGsmDatabase -ErrorAction SilentlyContinue }
    else { $env:ConnectionStrings__MDGsmDatabase = $previousConnectionString }
    if (Test-Path -LiteralPath $optionFilePath) { Remove-Item -LiteralPath $optionFilePath -Force -ErrorAction SilentlyContinue }
    $databasePassword = $null
    $servicePassword = $null
    Remove-Item -LiteralPath $downloadDirectory -Recurse -Force -ErrorAction SilentlyContinue
}
