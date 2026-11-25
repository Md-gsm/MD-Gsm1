[CmdletBinding()]
param([Parameter(Mandatory = $true)][string] $InstallerPath)

$ErrorActionPreference = 'Stop'
$serviceName = 'MDGsmApi'
$installDirectory = Join-Path $env:ProgramFiles 'MD-Gsm'
$keyDirectory = Join-Path $env:ProgramData 'MD-Gsm\Keys\Persons\MDGsmApi'
$keyFile = Join-Path $keyDirectory 'workflow-key.dpapi'

if (-not (Test-Path -LiteralPath $InstallerPath -PathType Leaf)) {
    throw 'Installer file not found.'
}
if (Test-Path -LiteralPath $keyDirectory) {
    throw 'The hosted Windows runner is expected to be clean; refusing to reuse existing ProgramData key storage.'
}

$resolvedInstaller = (Resolve-Path -LiteralPath $InstallerPath).Path
$uninstaller = Join-Path $installDirectory 'unins000.exe'

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

Write-Host '== Initial system-wide installation =='
Invoke-SystemInstaller 'initial-install'

if (-not (Test-Path -LiteralPath (Join-Path $installDirectory 'MDGsm.Desktop.exe') -PathType Leaf)) {
    throw 'Desktop executable is not under Program Files\MD-Gsm.'
}
if (-not (Test-Path -LiteralPath (Join-Path $installDirectory 'Api\MDGsm.Api.exe') -PathType Leaf)) {
    throw 'API executable is not under Program Files\MD-Gsm\Api.'
}
if (-not (Test-Path -LiteralPath $keyDirectory -PathType Container)) {
    throw 'The Persons key directory was not created under ProgramData.'
}

# The setup identity must not read the file. The installer itself verifies the
# exact file and directory DACLs before starting the service; service startup
# validates successful DPAPI unprotection under NT SERVICE\MDGsmApi.
Assert-ServiceReady 'initial-install'
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

Write-Host '== Repair by rerunning the actual system-wide installer =='
Invoke-SystemInstaller 'repair'
Assert-ServiceReady 'repair'

Write-Host '== Uninstall and verify key-directory preservation =='
Invoke-SystemUninstaller 'first-uninstall'
if (-not (Test-Path -LiteralPath $keyDirectory -PathType Container)) {
    throw 'Uninstall removed the protected Persons key directory from ProgramData.'
}

Write-Host '== Reinstall after uninstall to prove the existing DPAPI key was preserved =='
# If the key file was deleted, Install-MDGsmApiService.ps1 sees the existing
# directory and fails closed instead of silently provisioning a replacement.
Invoke-SystemInstaller 'post-uninstall-reinstall'
Assert-ServiceReady 'post-uninstall-reinstall'

Write-Host '== Final cleanup of the disposable Windows runner =='
Invoke-SystemUninstaller 'final-cleanup'
Write-Host 'PASS: Program Files install, service SID/DPAPI startup, non-service read denial, repair, uninstall key preservation, and post-uninstall reinstall.'
