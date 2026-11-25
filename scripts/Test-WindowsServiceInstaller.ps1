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

function Assert-ServiceReady([string] $Phase) {
    $service = Get-CimInstance Win32_Service -Filter "Name='$serviceName'" -ErrorAction SilentlyContinue
    if ($null -eq $service -or $service.State -ne 'Running') {
        throw "MDGsmApi is not running after phase '$Phase'."
    }
    if ($service.StartName -ne 'NT AUTHORITY\LocalService') {
        throw "MDGsmApi is not running as LocalService after phase '$Phase'."
    }
    $response = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:5080/health/live' -TimeoutSec 5
    if ($response.StatusCode -ne 200) {
        throw "MDGsmApi liveness failed after phase '$Phase'."
    }
    Write-Host ("PASS: MDGsmApi is running as LocalService after phase '" + $Phase + "'.")
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
