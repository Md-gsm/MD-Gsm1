[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $InstallerPath,
    [Parameter(Mandatory = $true)][string] $ClientInstallerPath
)

$ErrorActionPreference = 'Stop'
$serviceName = 'MDGsmApi'
$installDirectory = Join-Path $env:ProgramFiles 'MD-Gsm'
$keyFile = Join-Path (Join-Path $env:ProgramData 'MD-Gsm\Keys\Persons\MDGsmApi') 'workflow-key.dpapi'
$keyMarkerPath = 'HKLM:\SOFTWARE\MD-Gsm\Persons\WorkflowEncryption'
$keyMarkerName = 'Provisioned'
$serviceSetupScript = Join-Path $installDirectory 'Install-MDGsmApiService.ps1'
$serviceStatePath = Join-Path $env:TEMP 'MD-Gsm-Service-Rollback-Test.json'
$powerShell51 = Join-Path $env:windir 'System32\WindowsPowerShell\v1.0\powershell.exe'

if (-not (Test-Path -LiteralPath $InstallerPath -PathType Leaf)) {
    throw 'Installer file not found.'
}
if (-not (Test-Path -LiteralPath $ClientInstallerPath -PathType Leaf)) {
    throw 'Client-only installer file not found.'
}

$initialMarker = Get-ItemProperty -LiteralPath $keyMarkerPath -Name $keyMarkerName -ErrorAction SilentlyContinue
if ($null -ne $initialMarker -and [int]$initialMarker.$keyMarkerName -eq 1) {
    throw 'The hosted Windows runner is expected to be clean; refusing to reuse a prior key-provisioned marker.'
}

$resolvedInstaller = (Resolve-Path -LiteralPath $InstallerPath).Path
$uninstaller = Join-Path $installDirectory 'unins000.exe'

function Invoke-ServiceSetupPhase([string] $Phase) {
    if (-not (Test-Path -LiteralPath $serviceSetupScript -PathType Leaf)) {
        throw "The service setup script is missing while testing phase '$Phase'."
    }
    $output = & $powerShell51 -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $serviceSetupScript -InstallDirectory $installDirectory -ServiceStatePath $serviceStatePath -Phase $Phase 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw ("Service setup phase '$Phase' failed with exit code $exitCode." + [Environment]::NewLine + ($output -join [Environment]::NewLine))
    }
    Write-Host ("PASS: service setup phase '$Phase' completed.")
}

function Assert-KeyProvisionedMarker([string] $Phase) {
    $marker = Get-ItemProperty -LiteralPath $keyMarkerPath -Name $keyMarkerName -ErrorAction SilentlyContinue
    if ($null -eq $marker -or [int]$marker.$keyMarkerName -ne 1) {
        throw "The non-secret key-provisioned registry marker is missing after phase '$Phase'."
    }
}

function Invoke-SystemInstaller([string] $Phase) {
    $log = Join-Path $env:TEMP ("MD-Gsm-Setup-" + $Phase + ".log")
    if (Test-Path -LiteralPath $log) { Remove-Item -LiteralPath $log -Force }
    $arguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR="' +
        $installDirectory + '" /LOG="' + $log + '"'
    $process = Start-Process -FilePath $resolvedInstaller -ArgumentList $arguments -Wait -PassThru
    if ($null -eq $process -or $process.ExitCode -ne 0) {
        $tail = if (Test-Path -LiteralPath $log) {
            (Get-Content -LiteralPath $log -Tail 80) -join [Environment]::NewLine
        } else {
            'Inno Setup did not create an installation log.'
        }
        $code = if ($null -eq $process) { 'no-process-result' } else { [string]$process.ExitCode }
        throw ("Installer phase '" + $Phase + "' failed with exit code " + $code + "." +
            [Environment]::NewLine + $tail)
    }
    if (-not (Test-Path -LiteralPath $log -PathType Leaf)) {
        throw "Installer phase '$Phase' returned success without an Inno Setup log."
    }
    Write-Host ("PASS: installer phase '" + $Phase + "' completed with exit code " + $process.ExitCode)
}

function Get-ServiceFailureDiagnostics([string] $Phase) {
    $sections = [System.Collections.Generic.List[string]]::new()
    $service = Get-CimInstance Win32_Service -Filter "Name='$serviceName'" -ErrorAction SilentlyContinue
    if ($null -eq $service) {
        $sections.Add("Win32_Service: MDGsmApi registration not found.")
    } else {
        $sections.Add("Win32_Service details:")
        $sections.Add(($service | Select-Object Name, State, Status, StartName, StartMode, ProcessId,
            ExitCode, ServiceSpecificExitCode, PathName | Format-List | Out-String))
    }

    $scExe = Join-Path $env:windir 'System32\sc.exe'
    $sections.Add("sc.exe queryex:")
    $sections.Add((& $scExe queryex $serviceName 2>&1 | Out-String))
    $sections.Add("sc.exe qc:")
    $sections.Add((& $scExe qc $serviceName 2>&1 | Out-String))

    try {
        $listeners = @(Get-NetTCPConnection -LocalPort 5080 -State Listen -ErrorAction Stop)
        if ($listeners.Count -eq 0) {
            $sections.Add("TCP listener: no process is listening on port 5080.")
        }
        foreach ($listener in $listeners) {
            $owner = Get-CimInstance Win32_Process -Filter ("ProcessId={0}" -f $listener.OwningProcess)
            $sections.Add(("TCP listener: Address={0}; Port={1}; PID={2}; Process={3}; Executable={4}" -f
                $listener.LocalAddress, $listener.LocalPort, $listener.OwningProcess, $owner.Name, $owner.ExecutablePath))
        }
    } catch {
        $sections.Add("Could not inspect TCP listener ownership: $($_.Exception.Message)")
    }

    $eventStart = (Get-Date).AddMinutes(-10)
    foreach ($logName in @('System', 'Application')) {
        try {
            # Capture all recent warnings/errors, regardless of provider/source name.
            # EventLog provider source names can differ across Windows/.NET versions.
            $events = Get-WinEvent -FilterHashtable @{
                LogName = $logName
                StartTime = $eventStart
                Level = @(1, 2, 3)
            } -ErrorAction Stop |
                Sort-Object TimeCreated -Descending |
                Select-Object -First 30
            if ($events.Count -gt 0) {
                $sections.Add("Recent $logName events:")
                foreach ($event in $events) {
                    $message = [string]$event.Message
                    if ($message.Length -gt 3000) { $message = $message.Substring(0, 3000) }
                    $sections.Add(("{0:o} [{1}] EventId={2} Level={3}{4}{5}" -f
                        $event.TimeCreated, $event.ProviderName, $event.Id, $event.LevelDisplayName,
                        [Environment]::NewLine, $message))

                    # Some EventLog providers emit an EventId=0 record whose
                    # localized Message is empty. Preserve its structured data.
                    if ([string]::IsNullOrWhiteSpace($message) -and
                        $event.ProviderName -match 'Application|MD-Gsm|\.NET Runtime') {
                        $properties = @($event.Properties | ForEach-Object { [string]$_.Value }) -join ' | '
                        if ($properties.Length -gt 3000) { $properties = $properties.Substring(0, 3000) }
                        $sections.Add("Event property data: " + $properties)
                        $eventXml = [string]$event.ToXml()
                        if ($eventXml.Length -gt 6000) { $eventXml = $eventXml.Substring(0, 6000) }
                        $sections.Add("Event XML: " + $eventXml)
                    }
                }
            } else {
                $sections.Add("No matching recent events in $logName.")
            }
        } catch {
            $sections.Add("Could not read $logName event log: $($_.Exception.Message)")
        }
    }

    $serviceSetupLog = Join-Path $env:TEMP 'MD-Gsm-ApiServiceSetup.log'
    if (Test-Path -LiteralPath $serviceSetupLog -PathType Leaf) {
        $sections.Add("PowerShell API service setup log:")
        $sections.Add((Get-Content -LiteralPath $serviceSetupLog -Tail 120 | Out-String))
    }

    $setupLog = Join-Path $env:TEMP ("MD-Gsm-Setup-" + $Phase + ".log")
    if (Test-Path -LiteralPath $setupLog -PathType Leaf) {
        $sections.Add("Inno Setup log tail:")
        $sections.Add((Get-Content -LiteralPath $setupLog -Tail 50 | Out-String))
    }

    return ($sections -join [Environment]::NewLine)
}

function Assert-ServiceReady([string] $Phase) {
    $service = Get-CimInstance Win32_Service -Filter "Name='$serviceName'" -ErrorAction SilentlyContinue
    if ($null -eq $service -or $service.State -ne 'Running') {
        $diagnostics = Get-ServiceFailureDiagnostics $Phase
        Write-Host ("SERVICE FAILURE DIAGNOSTICS after phase '$Phase':" + [Environment]::NewLine + $diagnostics)
        throw "MDGsmApi is not running after phase '$Phase'. See service state, exit codes, SCM/.NET events, and installer log above."
    }
    if ($service.StartName -ne 'NT AUTHORITY\LocalService') {
        $diagnostics = Get-ServiceFailureDiagnostics $Phase
        Write-Host ("SERVICE IDENTITY DIAGNOSTICS after phase '$Phase':" + [Environment]::NewLine + $diagnostics)
        throw "MDGsmApi is not running as LocalService after phase '$Phase'."
    }
    try {
        $response = Invoke-WebRequest -UseBasicParsing -SkipHttpErrorCheck -Uri 'http://127.0.0.1:5080/health/live' -TimeoutSec 5
        if ($response.StatusCode -ne 200) {
            $body = [string]$response.Content
            if ($body.Length -gt 2048) { $body = $body.Substring(0, 2048) }
            $diagnostics = Get-ServiceFailureDiagnostics $Phase
            Write-Host ("LIVENESS HTTP RESPONSE: Status=$($response.StatusCode); Body=$body")
            Write-Host ("SERVICE LIVENESS DIAGNOSTICS after phase '$Phase':" + [Environment]::NewLine + $diagnostics)
            throw "MDGsmApi liveness returned HTTP $($response.StatusCode): $body"
        }

        $payload = $response.Content | ConvertFrom-Json
        if ($payload.product -ne 'MD-Gsm' -or $payload.status -ne 'alive' -or $payload.scope -ne 'process-liveness-only') {
            throw "The liveness endpoint did not return the expected MD-Gsm process identity payload."
        }

        # Compare a second lightweight route using the same middleware pipeline.
        $systemResponse = Invoke-WebRequest -UseBasicParsing -SkipHttpErrorCheck -Uri 'http://127.0.0.1:5080/api/v1/system/info' -TimeoutSec 5
        if ($systemResponse.StatusCode -ne 200) {
            $systemBody = [string]$systemResponse.Content
            if ($systemBody.Length -gt 2048) { $systemBody = $systemBody.Substring(0, 2048) }
            throw "The independent system-info route returned HTTP $($systemResponse.StatusCode): $systemBody"
        }
        $systemPayload = $systemResponse.Content | ConvertFrom-Json
        if ($systemPayload.data.product -ne 'MD-Gsm') {
            throw "The independent system-info route did not identify MD-Gsm."
        }

        $readyResponse = Invoke-WebRequest -UseBasicParsing -SkipHttpErrorCheck -Uri 'http://127.0.0.1:5080/health/ready' -TimeoutSec 10
        if ($readyResponse.StatusCode -ne 200) {
            throw "The API readiness endpoint returned HTTP $($readyResponse.StatusCode); the installer must not report success without PostgreSQL."
        }
        $readyPayload = $readyResponse.Content | ConvertFrom-Json
        if ($readyPayload.status -ne 'ready' -or $readyPayload.dependency -ne 'postgresql' -or
            $readyPayload.identity -ne 'verified' -or $readyPayload.serverMajor -ne 18) {
            throw 'The API readiness response did not prove PostgreSQL 18 runtime identity.'
        }

        $listeners = @(Get-NetTCPConnection -LocalPort 5080 -State Listen -ErrorAction Stop)
        if (-not ($listeners.OwningProcess -contains [uint32]$service.ProcessId)) {
            throw "Port 5080 is not owned by the registered MDGsmApi service process (service PID $($service.ProcessId))."
        }
    } catch {
        $diagnostics = Get-ServiceFailureDiagnostics $Phase
        Write-Host ("SERVICE LIVENESS DIAGNOSTICS after phase '$Phase':" + [Environment]::NewLine + $diagnostics)
        throw "MDGsmApi liveness failed after phase '$Phase': $($_.Exception.Message)"
    }
    Write-Host ("PASS: MDGsmApi is running as LocalService after phase '$Phase'.")
}
function Assert-DatabaseProvisioning([string] $Phase) {
    $pgService = Get-Service -Name 'MDGsmPostgreSQL18' -ErrorAction SilentlyContinue
    if ($null -eq $pgService -or $pgService.Status -ne 'Running') {
        throw "Managed PostgreSQL 18 is not running after '$Phase'."
    }

    $pgRoot = Join-Path $env:ProgramFiles 'MD-Gsm-PostgreSQL\18'
    $psql = Join-Path $pgRoot 'bin\psql.exe'
    $statePath = Join-Path $env:ProgramData 'MD-Gsm\Provisioning\PostgreSQL\cluster-state.json'
    $adminSecretPath = Join-Path $env:ProgramData 'MD-Gsm\Provisioning\PostgreSQL\cluster-admin.dpapi'
    $connectionFile = Join-Path $env:ProgramData 'MD-Gsm\Secrets\Api\database-connection.dpapi'
    foreach ($required in @($psql, $statePath, $adminSecretPath, $connectionFile)) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
            throw "A required central database provisioning file is missing after '$Phase'."
        }
    }

    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    if ($state.Status -ne 'ready' -or $state.PostgreSqlMajor -ne 18 -or
        $state.DatabaseName -ne 'mdgsm' -or $state.RuntimeLogin -ne 'mdgsm_api_runtime') {
        throw "The central PostgreSQL state does not record the approved ready identity after '$Phase'."
    }

    $connectionHash = (Get-FileHash -LiteralPath $connectionFile -Algorithm SHA256).Hash
    if ($null -eq $script:initialRuntimeConnectionHash) {
        $script:initialRuntimeConnectionHash = $connectionHash
    } elseif ($connectionHash -ne $script:initialRuntimeConnectionHash) {
        throw "The protected API database credential changed during '$Phase'; Repair/Reinstall must preserve it."
    }

    $adminBundle = (Unprotect-TestSecret $adminSecretPath) | ConvertFrom-Json
    $connectionString = Unprotect-TestSecret $connectionFile
    $pattern = '^Host=127\.0\.0\.1;Port=(\d{4,5});Database=mdgsm;Username=mdgsm_api_runtime;Password=([A-Za-z0-9_-]{40,80});Timeout=15;Command Timeout=30;Pooling=true$'
    if ($connectionString -notmatch $pattern -or [int]$Matches[1] -ne [int]$state.Port) {
        throw "The protected API connection secret does not match the expected local product identity after '$Phase'."
    }
    $runtimePassword = $Matches[2]
    $adminPassword = [string]$adminBundle.SuperuserPassword
    if ([string]::IsNullOrWhiteSpace($adminPassword)) { throw 'The protected PostgreSQL maintenance credential is invalid.' }

    $oldPassword = $env:PGPASSWORD
    try {
        $env:PGPASSWORD = $adminPassword
        $listen = & $psql -X -w -h 127.0.0.1 -p ([string]$state.Port) -U postgres -d postgres -t -A -c 'SHOW listen_addresses;'
        if ($LASTEXITCODE -ne 0 -or "$listen".Trim() -ne '127.0.0.1') {
            throw 'PostgreSQL is not restricted to IPv4 loopback.'
        }
        $role = & $psql -X -w -h 127.0.0.1 -p ([string]$state.Port) -U postgres -d postgres -t -A -c "SELECT (rolcanlogin AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolreplication AND NOT rolbypassrls AND rolinherit)::text FROM pg_roles WHERE rolname='mdgsm_api_runtime';"
        if ($LASTEXITCODE -ne 0 -or "$role".Trim() -ne 'true') {
            throw 'The API LOGIN does not have the required least-privilege role attributes.'
        }
        $membership = & $psql -X -w -h 127.0.0.1 -p ([string]$state.Port) -U postgres -d postgres -t -A -c "SELECT (m.inherit_option AND NOT m.set_option AND NOT m.admin_option)::text FROM pg_auth_members m JOIN pg_roles p ON p.oid=m.roleid JOIN pg_roles u ON u.oid=m.member WHERE p.rolname='mdgsm_runtime' AND u.rolname='mdgsm_api_runtime';"
        if ($LASTEXITCODE -ne 0 -or "$membership".Trim() -ne 'true') {
            throw 'The API LOGIN membership is not limited to INHERIT without SET or ADMIN.'
        }
        $migrationHistory = & $psql -X -w -h 127.0.0.1 -p ([string]$state.Port) -U postgres -d mdgsm -t -A -c "SELECT count(*) FROM pg_tables WHERE schemaname='public' AND tablename='mdgsm_migration_history';"
        if ($LASTEXITCODE -ne 0 -or "$migrationHistory".Trim() -ne '0') {
            throw 'Installer unexpectedly applied application schema migrations.'
        }

        $env:PGPASSWORD = $runtimePassword
        $identity = & $psql -X -w -h 127.0.0.1 -p ([string]$state.Port) -U mdgsm_api_runtime -d mdgsm -t -A -c "SELECT session_user || '|' || current_user || '|' || current_database() || '|' || (current_setting('server_version_num')::integer / 10000)::text || '|' || inet_server_addr()::text;"
        if ($LASTEXITCODE -ne 0 -or "$identity".Trim() -ne 'mdgsm_api_runtime|mdgsm_api_runtime|mdgsm|18|127.0.0.1') {
            throw 'The API PostgreSQL LOGIN failed the exact identity, version and loopback proof.'
        }
    }
    finally {
        if ($null -eq $oldPassword) { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }
        else { $env:PGPASSWORD = $oldPassword }
        $runtimePassword = $null
        $adminPassword = $null
        $connectionString = $null
        $adminBundle = $null
    }

    $secretAcl = Get-Acl -LiteralPath $connectionFile
    $identities = @($secretAcl.Access | ForEach-Object { $_.IdentityReference.Value })
    if (-not ($identities -contains 'NT SERVICE\MDGsmApi') -or
        ($identities -match 'BUILTIN\\Users|Everyone|Authenticated Users').Count -gt 0) {
        throw 'The protected API connection file ACL does not restrict access to the exact service SID.'
    }

    $apiSettings = Get-Content -LiteralPath (Join-Path $installDirectory 'Api\appsettings.Production.json') -Raw
    if ($apiSettings -match 'Password=|mdgsm_api_runtime.{0,30}Password') {
        throw 'The API checked-in production configuration contains a database credential.'
    }
    Write-Host ("PASS: PostgreSQL 18 identity, loopback-only listener, API LOGIN/membership, DPAPI secret preservation and no schema migrations verified after '$Phase'.")
}

function Unprotect-TestSecret([string] $Path) {
    $cipher = [System.IO.File]::ReadAllBytes($Path)
    $plain = $null
    try {
        $plain = [System.Security.Cryptography.ProtectedData]::Unprotect(
            $cipher, $null, [System.Security.Cryptography.DataProtectionScope]::LocalMachine)
        return [System.Text.Encoding]::UTF8.GetString($plain)
    }
    finally {
        [System.Array]::Clear($cipher, 0, $cipher.Length)
        if ($null -ne $plain) { [System.Array]::Clear($plain, 0, $plain.Length) }
    }
}

function Invoke-SystemUninstaller([string] $Phase) {
    if (-not (Test-Path -LiteralPath $uninstaller -PathType Leaf)) {
        throw "Inno Setup uninstaller was not found during phase '$Phase'."
    }
    $process = Start-Process -FilePath $uninstaller -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -Wait -PassThru
    if ($null -eq $process -or $process.ExitCode -ne 0) {
        $code = if ($null -eq $process) { 'no-process-result' } else { [string]$process.ExitCode }
        throw "Uninstaller phase '$Phase' failed with exit code $code."
    }

    $deadline = (Get-Date).AddSeconds(30)
    do {
        Start-Sleep -Milliseconds 500
        $service = Get-CimInstance Win32_Service -Filter "Name='$serviceName'" -ErrorAction SilentlyContinue
    } while ($null -ne $service -and (Get-Date) -lt $deadline)
    if ($null -ne $service) { throw "Windows service registration remains after uninstall phase '$Phase'." }
    if (Test-Path -LiteralPath $installDirectory) { throw "Program Files directory remains after uninstall phase '$Phase'." }
    Write-Host ("PASS: service and Program Files removed during phase '" + $Phase + "'.")
}

function Invoke-ClientOnlyInstallerTest {
    if (Get-Service -Name 'MDGsmApi' -ErrorAction SilentlyContinue) {
        throw 'The clean Windows runner unexpectedly already has MDGsmApi installed.'
    }
    if (Get-Service -Name 'MDGsmPostgreSQL18' -ErrorAction SilentlyContinue) {
        throw 'The clean Windows runner unexpectedly already has the product PostgreSQL service.'
    }

    $clientDirectory = Join-Path $env:ProgramFiles 'MD-Gsm-ClientTest'
    $clientUninstaller = Join-Path $clientDirectory 'unins000.exe'
    $resolvedClientInstaller = (Resolve-Path -LiteralPath $ClientInstallerPath).Path
    $log = Join-Path $env:TEMP 'MD-Gsm-Client-Install.log'
    $oldEndpoint = $env:MDGSM_CLIENT_API_BASEURL
    try {
        $env:MDGSM_CLIENT_API_BASEURL = 'https://central-test.example:5081/'
        $arguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /DIR="' +
            $clientDirectory + '" /LOG="' + $log + '"'
        $process = Start-Process -FilePath $resolvedClientInstaller -ArgumentList $arguments -Wait -PassThru
        if ($null -eq $process -or $process.ExitCode -ne 0) {
            throw 'The client-only installer failed in unattended CI mode.'
        }
        if (-not (Test-Path -LiteralPath (Join-Path $clientDirectory 'MDGsm.Desktop.exe') -PathType Leaf)) {
            throw 'Client-only setup did not install the Desktop executable.'
        }
        if (Test-Path -LiteralPath (Join-Path $clientDirectory 'Api')) {
            throw 'Client-only setup incorrectly installed the central API files.'
        }
        $clientConfig = Get-Content -LiteralPath (Join-Path $clientDirectory 'appsettings.json') -Raw
        if ($clientConfig -notmatch '"BaseUrl":"https://central-test\.example:5081/"') {
            throw 'Client-only setup did not persist the HTTPS central API endpoint.'
        }
        if (Get-Service -Name 'MDGsmApi' -ErrorAction SilentlyContinue) {
            throw 'Client-only setup incorrectly registered the MDGsmApi service.'
        }
        if (Get-Service -Name 'MDGsmPostgreSQL18' -ErrorAction SilentlyContinue) {
            throw 'Client-only setup incorrectly installed PostgreSQL.'
        }
        if (Test-Path -LiteralPath (Join-Path $env:ProgramData 'MD-Gsm\Provisioning\PostgreSQL\cluster-state.json')) {
            throw 'Client-only setup incorrectly created central PostgreSQL state.'
        }
        if (Test-Path -LiteralPath (Join-Path $env:ProgramData 'MD-Gsm\Secrets\Api\database-connection.dpapi')) {
            throw 'Client-only setup incorrectly created a PostgreSQL credential.'
        }

        if (-not (Test-Path -LiteralPath $clientUninstaller -PathType Leaf)) {
            throw 'Client-only setup did not create its uninstaller.'
        }
        $uninstall = Start-Process -FilePath $clientUninstaller -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -Wait -PassThru
        if ($null -eq $uninstall -or $uninstall.ExitCode -ne 0 -or (Test-Path -LiteralPath $clientDirectory)) {
            throw 'Client-only uninstall did not remove its Program Files client directory.'
        }
        Write-Host 'PASS: client-only install accepts HTTPS central API configuration and installs no API service, PostgreSQL, or DB secret.'
    }
    finally {
        if ($null -eq $oldEndpoint) { Remove-Item Env:MDGSM_CLIENT_API_BASEURL -ErrorAction SilentlyContinue }
        else { $env:MDGSM_CLIENT_API_BASEURL = $oldEndpoint }
    }
}

Write-Host '== Verify client-only install before central server setup =='
Invoke-ClientOnlyInstallerTest

Write-Host '== Initial system-wide installation =='
Invoke-SystemInstaller 'initial-install'

if (-not (Test-Path -LiteralPath (Join-Path $installDirectory 'MDGsm.Desktop.exe') -PathType Leaf)) {
    throw 'Desktop executable is not under Program Files\MD-Gsm.'
}
if (-not (Test-Path -LiteralPath (Join-Path $installDirectory 'Api\MDGsm.Api.exe') -PathType Leaf)) {
    throw 'API executable is not under Program Files\MD-Gsm\Api.'
}
Assert-KeyProvisionedMarker 'initial-install'

# The setup identity must not read the file. The installer itself verifies the
# exact file and directory DACLs before starting the service; service startup
# validates successful DPAPI unprotection under NT SERVICE\MDGsmApi.
Assert-ServiceReady 'initial-install'
Assert-DatabaseProvisioning 'initial-install'
$unexpectedKeyRead = $false
$keyBytes = $null
try {
    $keyBytes = [System.IO.File]::ReadAllBytes($keyFile)
    $unexpectedKeyRead = $true
}
catch [System.UnauthorizedAccessException] {
    # Expected: the test identity is not NT SERVICE\MDGsmApi or LocalSystem.
}
finally {
    if ($null -ne $keyBytes) {
        [System.Array]::Clear($keyBytes, 0, $keyBytes.Length)
    }
}
if ($unexpectedKeyRead) {
    throw 'The non-service installer/test identity can read the protected Persons key file.'
}

Write-Host '== Verify explicit Prepare/Restore preserves the prior Running state =='
Invoke-ServiceSetupPhase 'Prepare'
$preparedService = Get-CimInstance Win32_Service -Filter "Name='$serviceName'"
if ($null -eq $preparedService -or $preparedService.State -ne 'Stopped') {
    throw 'Prepare did not stop the running service before file replacement.'
}
Invoke-ServiceSetupPhase 'Restore'
Assert-ServiceReady 'rollback-restored-running'

Write-Host '== Verify explicit Prepare/Restore preserves a deliberately Stopped service =='
$scExe = Join-Path $env:windir 'System32\sc.exe'
& $scExe stop $serviceName | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not stop MDGsmApi for the stopped-state rollback test.' }
$stopDeadline = (Get-Date).AddSeconds(30)
do {
    Start-Sleep -Milliseconds 500
    $stoppedService = Get-CimInstance Win32_Service -Filter "Name='$serviceName'"
} while ($stoppedService.State -ne 'Stopped' -and (Get-Date) -lt $stopDeadline)
if ($stoppedService.State -ne 'Stopped') { throw 'MDGsmApi did not stop for the stopped-state rollback test.' }
Invoke-ServiceSetupPhase 'Prepare'
Invoke-ServiceSetupPhase 'Restore'
$restoredService = Get-CimInstance Win32_Service -Filter "Name='$serviceName'"
if ($null -eq $restoredService -or $restoredService.State -ne 'Stopped') {
    throw 'Rollback did not preserve the pre-install Stopped service state.'
}
& $scExe start $serviceName | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not restart MDGsmApi after the stopped-state rollback test.' }
Assert-ServiceReady 'rollback-restored-stopped'

Write-Host '== Repair by rerunning the actual system-wide installer =='
Invoke-SystemInstaller 'repair'
Assert-ServiceReady 'repair'
Assert-KeyProvisionedMarker 'repair'
Assert-DatabaseProvisioning 'repair'

Write-Host '== Uninstall and verify key-directory preservation =='
Invoke-SystemUninstaller 'first-uninstall'
Assert-KeyProvisionedMarker 'first-uninstall'
$pgServiceAfterUninstall = Get-Service -Name 'MDGsmPostgreSQL18' -ErrorAction SilentlyContinue
$connectionFileAfterUninstall = Join-Path $env:ProgramData 'MD-Gsm\Secrets\Api\database-connection.dpapi'
if ($null -eq $pgServiceAfterUninstall -or $pgServiceAfterUninstall.Status -ne 'Running' -or
    -not (Test-Path -LiteralPath $connectionFileAfterUninstall -PathType Leaf) -or
    (Get-FileHash -LiteralPath $connectionFileAfterUninstall -Algorithm SHA256).Hash -ne $script:initialRuntimeConnectionHash) {
    throw 'API uninstall removed or changed the central PostgreSQL cluster or protected runtime credential.'
}

Write-Host '== Reinstall after uninstall to prove the existing DPAPI key was preserved =='
# If the key file was deleted, Install-MDGsmApiService.ps1 sees the existing
# directory and fails closed instead of silently provisioning a replacement.
Invoke-SystemInstaller 'post-uninstall-reinstall'
Assert-ServiceReady 'post-uninstall-reinstall'
Assert-KeyProvisionedMarker 'post-uninstall-reinstall'
Assert-DatabaseProvisioning 'post-uninstall-reinstall'
$setupLogAfterReinstall = Join-Path $env:TEMP 'MD-Gsm-ApiServiceSetup.log'
if (-not (Test-Path -LiteralPath $setupLogAfterReinstall -PathType Leaf) -or
    -not (Get-Content -LiteralPath $setupLogAfterReinstall -Raw).Contains('Existing DPAPI key-provisioned marker found; one-time provisioning skipped.')) {
    throw 'Reinstall did not prove that the existing ProgramData key was reused without provisioning.'
}

Write-Host '== Final cleanup of the disposable Windows runner =='
Invoke-SystemUninstaller 'final-cleanup'
Write-Host 'PASS: Program Files install, service-token owner/DACL/DPAPI validation, non-service read denial, Running and Stopped rollback restoration, repair, uninstall marker preservation, and post-uninstall reinstall without reprovisioning.'
