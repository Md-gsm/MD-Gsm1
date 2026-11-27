[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $InstallDirectory,
    [Parameter(Mandatory = $true)][string] $ServiceStatePath,
    [ValidateSet('Prepare', 'Install', 'Restore')][string] $Phase = 'Install'
)

$ErrorActionPreference = 'Stop'
$serviceName = 'MDGsmApi'
$serviceAccount = 'NT AUTHORITY\LocalService'
$apiExe = Join-Path $InstallDirectory 'Api\MDGsm.Api.exe'
$scExe = Join-Path $env:windir 'System32\sc.exe'
$quotedApiExe = [string]([char]34) + $apiExe + [char]34
$keyFile = Join-Path (Join-Path $env:ProgramData 'MD-Gsm\Keys\Persons\MDGsmApi') 'workflow-key.dpapi'
$keyMarkerPath = 'HKLM:\SOFTWARE\MD-Gsm\Persons\WorkflowEncryption'
$keyMarkerName = 'Provisioned'

$script:setupLogPath = Join-Path $env:TEMP 'MD-Gsm-ApiServiceSetup.log'
$script:setupStage = 'initializing'
try {
    Set-Content -LiteralPath $script:setupLogPath -Value ("{0:o} setup script started; phase={1}" -f (Get-Date), $Phase) -Encoding UTF8
} catch {
    # Continue with installation; logging must not mask the operational result.
}

function Write-SetupLog([string] $Message) {
    try {
        Add-Content -LiteralPath $script:setupLogPath -Value ("{0:o} [{1}] {2}" -f (Get-Date), $script:setupStage, $Message) -Encoding UTF8
    } catch {
        # Logging must never grant broader access or mask the installation exception.
    }
}

function Invoke-Sc([string[]] $Arguments) {
    Write-SetupLog ("SCM command: sc.exe " + ($Arguments -join ' '))
    $output = & $scExe @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    Write-SetupLog ("SCM exit code: " + $exitCode)
    foreach ($line in @($output)) { Write-SetupLog ("SCM output: " + [string]$line) }
    if ($exitCode -ne 0) {
        throw "Service Control Manager operation '$($Arguments[0])' failed (exit $exitCode): $($output -join ' ')"
    }
}

function Get-ServiceRecord {
    return Get-CimInstance Win32_Service -Filter "Name='$serviceName'" -ErrorAction SilentlyContinue
}

function Wait-ServiceState([string] $ExpectedState, [int] $TimeoutSeconds = 40) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $service = Get-ServiceRecord
        if ($null -eq $service) {
            if ($ExpectedState -eq 'Absent') { return }
            throw "MDGsmApi service registration disappeared while waiting for state '$ExpectedState'."
        }
        if ($service.State -eq $ExpectedState) { return }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)
    $service = Get-ServiceRecord
    $actual = if ($null -eq $service) { 'Absent' } else { $service.State }
    throw "MDGsmApi did not reach '$ExpectedState' state; current state is '$actual'."
}

function Get-StartupArgument([string] $StartMode) {
    switch ($StartMode) {
        'Auto' { return 'auto' }
        'Manual' { return 'demand' }
        'Disabled' { return 'disabled' }
        default { throw "Unsupported existing MDGsmApi startup mode '$StartMode'." }
    }
}

function Get-ConfiguredExecutable([string] $PathName) {
    $value = $PathName.Trim()
    if ($value.StartsWith([string][char]34)) {
        $closingQuote = $value.IndexOf([char]34, 1)
        if ($closingQuote -lt 2 -or $value.Substring($closingQuote + 1).Trim().Length -ne 0) {
            throw "The existing MDGsmApi service has an unexpected command line."
        }
        $value = $value.Substring(1, $closingQuote - 1)
    }
    return [System.IO.Path]::GetFullPath($value)
}

function Read-ServiceState {
    if (-not [System.IO.File]::Exists($ServiceStatePath)) {
        throw "The installer service-state record is missing; refusing to guess the pre-install service state."
    }
    $json = [System.IO.File]::ReadAllText($ServiceStatePath, [System.Text.Encoding]::UTF8)
    $state = $json | ConvertFrom-Json
    if ($state.SchemaVersion -ne 1 -or $null -eq $state.ServiceExisted -or $null -eq $state.WasRunning) {
        throw "The installer service-state record is invalid."
    }
    return $state
}

function Write-ServiceState($State) {
    $parent = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($ServiceStatePath))
    if (-not [System.IO.Directory]::Exists($parent)) {
        [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    }
    $json = $State | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText(
        [System.IO.Path]::GetFullPath($ServiceStatePath),
        $json,
        [System.Text.UTF8Encoding]::new($false))
}

function Test-KeyProvisionedMarker {
    if (-not (Test-Path -LiteralPath $keyMarkerPath)) { return $false }
    $marker = Get-ItemProperty -LiteralPath $keyMarkerPath -Name $keyMarkerName -ErrorAction SilentlyContinue
    return ($null -ne $marker -and [int]$marker.$keyMarkerName -eq 1)
}

function Set-KeyProvisionedMarker {
    New-Item -Path $keyMarkerPath -Force | Out-Null
    New-ItemProperty -LiteralPath $keyMarkerPath -Name $keyMarkerName -PropertyType DWord -Value 1 -Force | Out-Null
    Write-SetupLog 'Non-secret HKLM key-provisioned marker was committed. The DPAPI blob remains under ProgramData and is never read by Setup.'
}

function Get-ConfiguredApiBaseUrl {
    $networkConfigPath = Join-Path $env:ProgramData 'MD-Gsm\Network\api-network.json'
    if (-not (Test-Path -LiteralPath $networkConfigPath -PathType Leaf)) {
        # Compatibility path used only to attest a pre-network-configuration service
        # before a repair replaces it. New installations never fall back to HTTP.
        return 'http://127.0.0.1:5080/'
    }
    $networkConfig = Get-Content -LiteralPath $networkConfigPath -Raw | ConvertFrom-Json
    if ($networkConfig.Network.Mode -ne 'Https' -or
        $networkConfig.Network.Port -ne 5081 -or
        [string]::IsNullOrWhiteSpace([string]$networkConfig.Network.ApiBaseUrl) -or
        [string]::IsNullOrWhiteSpace([string]$networkConfig.Network.CertificateThumbprint)) {
        throw 'The installed API network configuration is incomplete or does not enforce HTTPS.'
    }
    return [string]$networkConfig.Network.ApiBaseUrl
}

function Wait-ApiLiveness {
    $baseUri = [Uri](Get-ConfiguredApiBaseUrl)
    $healthUri = [Uri]::new($baseUri, 'health/live').AbsoluteUri
    $deadline = (Get-Date).AddSeconds(40)
    $healthy = $false
    while (-not $healthy -and (Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $healthUri -TimeoutSec 4
            $healthy = ($response.StatusCode -eq 200)
        } catch {
            Start-Sleep -Milliseconds 500
        }
    }
    if (-not $healthy) { throw 'MDGsmApi did not return HTTP 200 from its configured API liveness endpoint.' }
}

function Wait-ApiReadiness {
    $baseUri = [Uri](Get-ConfiguredApiBaseUrl)
    $healthUri = [Uri]::new($baseUri, 'health/ready').AbsoluteUri
    $deadline = (Get-Date).AddSeconds(90)
    $healthy = $false
    while (-not $healthy -and (Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $healthUri -TimeoutSec 5
            if ($response.StatusCode -eq 200) {
                $payload = $response.Content | ConvertFrom-Json
                $healthy = ($payload.status -eq 'ready' -and
                    $payload.dependency -eq 'postgresql' -and
                    $payload.identity -eq 'verified' -and
                    $payload.serverMajor -eq 18)
            }
        } catch {
            Start-Sleep -Milliseconds 1000
        }
    }
    if (-not $healthy) {
        throw 'MDGsmApi did not prove PostgreSQL 18 readiness over the configured HTTPS listener within 90 seconds.'
    }
}

function Restore-ServiceState {
    $state = Read-ServiceState
    $service = Get-ServiceRecord

    if (-not [bool]$state.ServiceExisted) {
        if ($null -ne $service) {
            $actualExe = Get-ConfiguredExecutable ([string]$service.PathName)
            if ($service.StartName -ne $serviceAccount -or
                -not [string]::Equals($actualExe, [System.IO.Path]::GetFullPath($apiExe), [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Rollback found an unexpected MDGsmApi service identity; refusing to remove or take over that service."
            }
            if ($service.State -ne 'Stopped') {
                Invoke-Sc @('stop', $serviceName)
                Wait-ServiceState 'Stopped' 30
            }
            Invoke-Sc @('delete', $serviceName)
            Wait-ServiceState 'Absent' 30
            Write-SetupLog 'Rollback removed MDGsmApi because no service existed before Setup.'
        }
    } else {
        $startup = Get-StartupArgument ([string]$state.StartMode)
        $priorPath = [string]$state.PathName
        if ($null -ne $service) {
            $priorExecutable = Get-ConfiguredExecutable $priorPath
            $currentExecutable = Get-ConfiguredExecutable ([string]$service.PathName)
            if ($service.StartName -ne [string]$state.StartName -or
                -not [string]::Equals($currentExecutable, $priorExecutable, [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Rollback found that the pre-existing MDGsmApi registration was replaced; refusing to reconfigure an unexpected service."
            }
        }
        if (-not $priorPath.StartsWith([string][char]34)) {
            $priorPath = [string][char]34 + $priorPath + [string][char]34
        }
        if ($null -eq $service) {
            Invoke-Sc @('create', $serviceName, 'binPath=', $priorPath, 'start=', $startup, 'obj=', $serviceAccount, 'DisplayName=', ('"' + [string]$state.DisplayName + '"'))
            Write-SetupLog 'Rollback restored the pre-existing MDGsmApi service registration.'
        } else {
            Invoke-Sc @('config', $serviceName, 'binPath=', $priorPath, 'start=', $startup, 'obj=', $serviceAccount, 'DisplayName=', ('"' + [string]$state.DisplayName + '"'))
        }
        Invoke-Sc @('sidtype', $serviceName, 'unrestricted')

        $service = Get-ServiceRecord
        if ([bool]$state.WasRunning) {
            if ($service.State -ne 'Running') {
                Invoke-Sc @('start', $serviceName)
                Wait-ServiceState 'Running' 40
            }
            Wait-ApiLiveness
            Write-SetupLog 'Rollback restored the pre-install Running state and loopback API health.'
        } else {
            if ($service.State -ne 'Stopped') {
                Invoke-Sc @('stop', $serviceName)
                Wait-ServiceState 'Stopped' 30
            }
            Write-SetupLog 'Rollback restored the pre-install Stopped state.'
        }
    }

    Remove-Item -LiteralPath $ServiceStatePath -Force -ErrorAction Stop
}

function Invoke-PreparePhase {
    if ([System.IO.File]::Exists($ServiceStatePath)) {
        $script:setupStage = 'recover prior interrupted setup state'
        Write-SetupLog 'A prior service-state record exists. Restoring it before capturing this setup attempt.'
        Restore-ServiceState
        Write-SetupLog 'Prior interrupted Setup state restored; a fresh pre-install snapshot will be captured.'
    }

    $script:setupStage = 'capture pre-install service state'
    Write-SetupLog 'Capturing MDGsmApi service identity and state before any Program Files replacement.'
    $service = Get-ServiceRecord
    $state = [ordered]@{
        SchemaVersion = 1
        ServiceExisted = ($null -ne $service)
        WasRunning = ($null -ne $service -and $service.State -eq 'Running')
        StartMode = if ($null -ne $service) { [string]$service.StartMode } else { 'Auto' }
        StartName = if ($null -ne $service) { [string]$service.StartName } else { $serviceAccount }
        PathName = if ($null -ne $service) { [string]$service.PathName } else { $quotedApiExe }
        DisplayName = if ($null -ne $service) { [string]$service.DisplayName } else { 'MD-Gsm API' }
    }

    if ($null -ne $service) {
        if ($service.StartName -ne $serviceAccount) {
            throw "An existing MDGsmApi registration uses an unapproved logon account; Setup will not take it over."
        }
        $actualExe = Get-ConfiguredExecutable ([string]$service.PathName)
        if (-not [string]::Equals($actualExe, [System.IO.Path]::GetFullPath($apiExe), [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "An existing MDGsmApi registration points to an unexpected executable; Setup will not take it over."
        }
        if ($service.StartMode -notin @('Auto', 'Manual', 'Disabled')) {
            throw "An existing MDGsmApi registration has an unsupported startup mode."
        }
    }
    Write-ServiceState $state

    # Legacy installations created before the marker existed are attested by
    # starting the already-registered service and requiring its own startup key
    # validation plus loopback health. Setup never probes the protected file.
    if ($null -ne $service -and -not (Test-KeyProvisionedMarker)) {
        $script:setupStage = 'attest existing key through the service identity'
        if ($service.State -ne 'Running') {
            Invoke-Sc @('start', $serviceName)
            Wait-ServiceState 'Running' 40
        }
        Wait-ApiLiveness
        Set-KeyProvisionedMarker
        Write-SetupLog 'Existing key was attested by the API service before repair; provisioning will remain disabled.'
    }

    $service = Get-ServiceRecord
    if ($null -ne $service -and $service.State -ne 'Stopped') {
        $script:setupStage = 'stop service before file replacement'
        Invoke-Sc @('stop', $serviceName)
        Wait-ServiceState 'Stopped' 30
    }
    Write-SetupLog 'Prepare phase completed; MDGsmApi is stopped before Inno Setup replaces Program Files files.'
}

function Remove-NewNetworkProvisioningAfterFailedFirstInstall {
    # First-time Setup has no prior API endpoint to restore. Remove any
    # network exposure/bundle created before the failure; keep non-exportable
    # machine certificates so a controlled retry can reuse the same identity.
    try {
        Get-NetFirewallRule -Name 'MDGsmApiHttps5081' -ErrorAction SilentlyContinue |
            Remove-NetFirewallRule -ErrorAction SilentlyContinue
        Unregister-ScheduledTask -TaskName 'MD-Gsm API TLS Certificate Renewal' -Confirm:$false -ErrorAction SilentlyContinue

        $networkRoot = Join-Path $env:ProgramData 'MD-Gsm\Network'
        Remove-Item -LiteralPath (Join-Path $networkRoot 'api-network.json') -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath (Join-Path $networkRoot 'MD-Gsm-Client-Connection.mdgsm.json') -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath (Join-Path $env:PUBLIC 'Documents\MD-Gsm-Client-Connection.mdgsm.json') -Force -ErrorAction SilentlyContinue

        $rootStore = [System.Security.Cryptography.X509Certificates.X509Store]::new(
            [System.Security.Cryptography.X509Certificates.StoreName]::Root,
            [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
        try {
            $rootStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
            $machineRoots = Get-ChildItem -Path Cert:\LocalMachine\My |
                Where-Object { $_.Subject -eq 'CN=MD-Gsm Internal API Root CA' -and $_.HasPrivateKey }
            foreach ($machineRoot in $machineRoots) {
                $trusted = $rootStore.Certificates.Find(
                    [System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint,
                    $machineRoot.Thumbprint, $false)
                foreach ($certificate in $trusted) { $rootStore.Remove($certificate) }
            }
        } finally {
            $rootStore.Close()
            $rootStore.Dispose()
        }
        Write-SetupLog 'First-install network rollback removed the HTTPS firewall rule, renewal task, client bundle and public root trust. Non-exportable machine keys and PostgreSQL data were preserved.'
    } catch {
        Write-SetupLog ('WARNING: first-install network cleanup was incomplete: ' + $_.Exception.Message)
    }
}

function Invoke-InstallPhase {
    $state = Read-ServiceState
    $serviceExistedAtStart = [bool]$state.ServiceExisted

    try {
        $script:setupStage = 'validate installation and elevation'
        Write-SetupLog 'Validating API executable and elevated setup identity.'
        if (-not (Test-Path -LiteralPath $apiExe -PathType Leaf)) {
            throw "The API executable was not found under the selected Program Files installation."
        }
        $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
        if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
            throw "The MD-Gsm service installer must run elevated."
        }

        $script:setupStage = 'create or configure Windows service'
        $existingService = Get-ServiceRecord
        if ($serviceExistedAtStart) {
            if ($null -eq $existingService) {
                throw "The pre-existing MDGsmApi registration disappeared between Prepare and Install."
            }
            if ($existingService.State -ne 'Stopped') {
                throw "MDGsmApi was not stopped by the Prepare phase before file replacement."
            }
            Invoke-Sc @('config', $serviceName, 'binPath=', $quotedApiExe, 'start=', 'auto', 'obj=', $serviceAccount, 'DisplayName=', '"MD-Gsm API"')
        } else {
            if ($null -ne $existingService) {
                throw "An unexpected MDGsmApi service appeared after the Prepare phase."
            }
            Invoke-Sc @('create', $serviceName, 'binPath=', $quotedApiExe, 'start=', 'auto', 'obj=', $serviceAccount, 'DisplayName=', '"MD-Gsm API"')
        }

        $script:setupStage = 'configure service SID and recovery policy'
        Write-SetupLog 'Configuring service SID, description, and recovery actions.'
        Invoke-Sc @('sidtype', $serviceName, 'unrestricted')
        Invoke-Sc @('description', $serviceName, 'MD-Gsm local API. Runs as LocalService with a dedicated service SID.')
        Invoke-Sc @('failure', $serviceName, 'reset=', '86400', 'actions=', 'restart/5000/restart/15000/restart/60000')

        $script:setupStage = 'provision or preserve DPAPI key'
        if (-not (Test-KeyProvisionedMarker)) {
            if ($serviceExistedAtStart) {
                throw "An existing installation did not attest its protected key during Prepare; refusing to provision or replace it."
            }
            Write-SetupLog 'No trusted key-provisioned marker exists on first installation; invoking one-time provisioning.'
            $provisionOutput = & $apiExe '--provision-persons-workflow-key' $keyFile $serviceName 2>&1
            $provisionExitCode = $LASTEXITCODE
            foreach ($line in @($provisionOutput)) {
                Write-SetupLog ('Provisioning output: ' + [string]$line)
            }
            Write-SetupLog ('Provisioning exit code: ' + $provisionExitCode)
            if ($provisionExitCode -ne 0) {
                throw "One-time DPAPI key provisioning failed; the key was not replaced."
            }
            # Set the marker immediately after FileMode.CreateNew provisioning succeeds.
            # If startup later fails, a retry must validate the existing key, never replace it.
            Set-KeyProvisionedMarker
        } else {
            Write-SetupLog 'Existing DPAPI key-provisioned marker found; one-time provisioning skipped. The service will validate the protected blob and DACL at startup.'
        }

        $script:setupStage = 'register Windows Application Event Log source'
        $eventSource = 'MDGsm.Api'
        Write-SetupLog ("Ensuring Application Event Log source exists: " + $eventSource)
        if ([System.Diagnostics.EventLog]::SourceExists($eventSource)) {
            $eventLogName = [System.Diagnostics.EventLog]::LogNameFromSourceName($eventSource, '.')
            if ($eventLogName -ne 'Application') {
                throw "The MDGsm.Api event source is already registered to an unexpected Windows Event Log."
            }
            Write-SetupLog 'Application Event Log source already exists.'
        } else {
            # Setup is elevated; never ask the LocalService runtime identity to
            # create machine-wide Event Log registry entries.
            [System.Diagnostics.EventLog]::CreateEventSource($eventSource, 'Application')
            Write-SetupLog 'Application Event Log source was created by elevated Setup.'
        }

        $script:setupStage = 'provision central PostgreSQL database and API LOGIN'
        $provisionerPath = Join-Path $InstallDirectory 'Provision-MDGsmCentralDatabase.ps1'
        if (-not (Test-Path -LiteralPath $provisionerPath -PathType Leaf)) {
            throw "The central PostgreSQL provisioner is missing from the installation package."
        }
        Write-SetupLog 'Running central PostgreSQL 18 provision-or-verify. This step does not apply application schema migrations.'
        $provisionStdout = Join-Path $env:TEMP ('MDGsm-Postgres-Provision-' + [Guid]::NewGuid().ToString('N') + '.out.log')
        $provisionStderr = Join-Path $env:TEMP ('MDGsm-Postgres-Provision-' + [Guid]::NewGuid().ToString('N') + '.err.log')
        $provisionPowerShell = Join-Path $PSHOME 'powershell.exe'
        $provisionArguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
            $provisionerPath + '" -InstallDirectory "' + $InstallDirectory + '"'
        $provisionStart = @{
            FilePath = $provisionPowerShell
            ArgumentList = $provisionArguments
            Wait = $true
            PassThru = $true
            NoNewWindow = $true
            RedirectStandardOutput = $provisionStdout
            RedirectStandardError = $provisionStderr
        }
        try {
            $provisionProcess = Start-Process @provisionStart
            $provisionExitCode = $provisionProcess.ExitCode
        }
        finally {
            foreach ($provisionLog in @($provisionStdout, $provisionStderr)) {
                if (Test-Path -LiteralPath $provisionLog -PathType Leaf) {
                    foreach ($line in Get-Content -LiteralPath $provisionLog) {
                        Write-SetupLog ('Database provisioner: ' + [string]$line)
                    }
                    Remove-Item -LiteralPath $provisionLog -Force -ErrorAction SilentlyContinue
                }
            }
        }
        if ($provisionExitCode -ne 0) {
            throw "Central PostgreSQL provisioning failed (exit $provisionExitCode); API service start is blocked."
        }

        $script:setupStage = 'discover private network and provision API HTTPS'
        $networkScriptPath = Join-Path $InstallDirectory 'Configure-MDGsmNetwork.ps1'
        if (-not (Test-Path -LiteralPath $networkScriptPath -PathType Leaf)) {
            throw 'The central HTTPS network configuration script is missing from the installation package.'
        }
        Write-SetupLog 'Automatically inspecting Windows network profiles, selecting the lowest-metric private network, provisioning a machine-local TLS certificate and scoping Windows Firewall. Public profiles and ambiguous interfaces remain loopback-only.'
        $networkOutput = & $provisionPowerShell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $networkScriptPath -InstallDirectory $InstallDirectory 2>&1
        $networkExitCode = $LASTEXITCODE
        foreach ($line in @($networkOutput)) {
            $text = [string]$line
            if ($text -match '^MDGSM_CLIENT_CONNECTION_BUNDLE=') {
                Write-SetupLog ('Client connection bundle created: ' + ($text -replace '^MDGSM_CLIENT_CONNECTION_BUNDLE=', ''))
            } elseif ($text -match '^MDGSM_REMOTE_API_URL=') {
                Write-SetupLog ('API network endpoint: ' + ($text -replace '^MDGSM_REMOTE_API_URL=', ''))
            } else {
                Write-SetupLog ('Network provisioner: ' + $text)
            }
        }
        if ($networkExitCode -ne 0) {
            throw "Central API HTTPS network provisioning failed (exit $networkExitCode); the service will not be started."
        }

        $script:setupStage = 'validate service account and start service'
        $service = Get-ServiceRecord
        if ($service.StartName -ne $serviceAccount) {
            throw "MDGsmApi is not configured to run as the approved LocalService account."
        }
        Write-SetupLog 'Requesting MDGsmApi start through Service Control Manager.'
        Invoke-Sc @('start', $serviceName)
        Wait-ServiceState 'Running' 40

        $script:setupStage = 'verify loopback API liveness'
        Wait-ApiLiveness
        $script:setupStage = 'verify actual PostgreSQL runtime readiness'
        Wait-ApiReadiness
        Write-SetupLog 'Installation completed successfully; API liveness and PostgreSQL 18 runtime identity/readiness verified.'
        Remove-Item -LiteralPath $ServiceStatePath -Force -ErrorAction Stop
        Write-Host 'MD-Gsm API service is running as LocalService with the MDGsmApi service SID.'
        Write-Host 'The DPAPI key remains under ProgramData; Setup did not read the protected key file.'
    } catch {
        $originalFailure = $_
        Write-SetupLog ("FATAL: " + $originalFailure.Exception.ToString())
        Write-SetupLog ("FATAL category: " + [string]$originalFailure.CategoryInfo)
        Write-SetupLog ("FATAL script stack: " + [string]$originalFailure.ScriptStackTrace)
        if (-not $serviceExistedAtStart) {
            Remove-NewNetworkProvisioningAfterFailedFirstInstall
        }
        try {
            $script:setupStage = 'restore pre-install service state'
            Restore-ServiceState
        } catch {
            Write-SetupLog ("Rollback failed; original installation error is preserved. Rollback error: " + $_.Exception.ToString())
        }
        throw
    }
}

try {
    switch ($Phase) {
        'Prepare' {
            try {
                Invoke-PreparePhase
                exit 0
            } catch {
                Write-SetupLog ("PREPARE FATAL: " + $_.Exception.ToString())
                throw
            }
        }
        'Restore' {
            try {
                $script:setupStage = 'restore service after interrupted Setup'
                Restore-ServiceState
                Write-SetupLog 'Setup interruption rollback completed.'
                exit 0
            } catch {
                Write-SetupLog ("RESTORE FATAL: " + $_.Exception.ToString())
                throw
            }
        }
        'Install' {
            Invoke-InstallPhase
        }
    }
} catch {
    throw
}
