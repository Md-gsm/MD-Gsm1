[CmdletBinding()]
param([Parameter(Mandatory = $true)][string] $InstallerPath)

$ErrorActionPreference = 'Stop'
$serviceName = 'MDGsmApi'
$installDirectory = Join-Path $env:ProgramFiles 'MD-Gsm'
$keyDirectory = Join-Path $env:ProgramData 'MD-Gsm\Keys\Persons\MDGsmApi'
$keyFile = Join-Path $keyDirectory 'workflow-key.dpapi'

if (-not (Test-Path -LiteralPath $InstallerPath -PathType Leaf)) { throw "Installer file not found." }
if (Test-Path -LiteralPath $keyDirectory) {
    throw "The hosted Windows runner is expected to be clean; refusing to reuse existing ProgramData key storage."
}

Write-Host '== Install the system-wide package =='
$resolvedInstaller = (Resolve-Path -LiteralPath $InstallerPath).Path
$installerLog = Join-Path $env:TEMP 'MD-Gsm-Setup-install.log'
if (Test-Path -LiteralPath $installerLog) { Remove-Item -LiteralPath $installerLog -Force }
$installerArguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP- /LOG="' + $installerLog + '"'
$installerProcess = Start-Process -FilePath $resolvedInstaller -ArgumentList $installerArguments -Wait -PassThru
if ($null -eq $installerProcess -or $installerProcess.ExitCode -ne 0) {
    $logTail = if (Test-Path -LiteralPath $installerLog) {
        (Get-Content -LiteralPath $installerLog -Tail 80) -join [Environment]::NewLine
    } else {
        'Inno Setup did not create an installation log.'
    }
    $exitCode = if ($null -eq $installerProcess) { 'no-process-result' } else { [string]$installerProcess.ExitCode }
    throw ('The system-wide installer failed with exit code ' + $exitCode + '. Inno Setup log:' + [Environment]::NewLine + $logTail)
}
if (-not (Test-Path -LiteralPath $installerLog)) {
    throw 'Inno Setup returned success but did not create its expected installation log.'
}
Write-Host ('Inno Setup completed with exit code ' + $installerProcess.ExitCode + '. Log: ' + $installerLog)

if (-not (Test-Path -LiteralPath (Join-Path $installDirectory 'MDGsm.Desktop.exe'))) { throw "Desktop is not under Program Files\MD-Gsm." }
if (-not (Test-Path -LiteralPath (Join-Path $installDirectory 'Api\MDGsm.Api.exe'))) { throw "API is not under Program Files\MD-Gsm\Api." }
if (-not (Test-Path -LiteralPath $keyDirectory -PathType Container)) {
    throw "The Persons key directory was not created under ProgramData."
}
if (-not (Test-Path -LiteralPath $keyFile -PathType Leaf)) {
    throw "The DPAPI key file was not created under ProgramData."
}

# The elevated Setup script performs the exact two-principal DACL verification
# before it starts the service. This outer test deliberately proves that the
# non-service runner cannot read key bytes, while the service startup validator
# proves that the intended service identity can read and unprotect those bytes.
$unexpectedKeyRead = $false
$keyBytes = $null
try {
    $keyBytes = [System.IO.File]::ReadAllBytes($keyFile)
    $unexpectedKeyRead = $true
}
catch [System.UnauthorizedAccessException] {
    # Expected: the test identity is not the API service SID or LocalSystem.
}
finally {
    if ($null -ne $keyBytes) {
        [System.Security.Cryptography.CryptographicOperations]::ZeroMemory($keyBytes)
    }
}
if ($unexpectedKeyRead) {
    throw "The non-service installer/test identity can read the protected Persons key file."
}

$service = Get-CimInstance Win32_Service -Filter "Name='$serviceName'"
if ($null -eq $service -or $service.State -ne 'Running') { throw "The installed API Windows service is not running." }
if ($service.StartName -ne 'NT AUTHORITY\LocalService') { throw "The API service is not running as LocalService." }

# Reaching this point means Setup's Assert-ProtectedAcl checks for both the
# directory and key file passed, and the API's startup validator unprotected
# the key while running under NT SERVICE\MDGsmApi.

Write-Host '== Run service installer again (repair simulation) =='
& (Join-Path $installDirectory 'Install-MDGsmApiService.ps1') -InstallDirectory $installDirectory
if ($LASTEXITCODE -ne 0) { throw "Repair simulation failed." }
if (-not (Test-Path -LiteralPath $keyDirectory -PathType Container) -or
    -not (Test-Path -LiteralPath $keyFile -PathType Leaf)) {
    throw "Repair removed the existing protected Persons key instead of preserving it."
}

$service = Get-CimInstance Win32_Service -Filter "Name='$serviceName'"
if ($null -eq $service -or $service.State -ne 'Running') { throw "The API service did not restart after repair." }

Write-Host '== Uninstall while preserving ProgramData key =='
$uninstaller = Join-Path $installDirectory 'unins000.exe'
if (-not (Test-Path -LiteralPath $uninstaller)) { throw "Inno Setup uninstaller is missing." }
& $uninstaller /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
if ($LASTEXITCODE -ne 0) { throw "Uninstaller returned exit code $LASTEXITCODE." }

$deadline = (Get-Date).AddSeconds(20)
do {
    Start-Sleep -Milliseconds 500
    $service = Get-CimInstance Win32_Service -Filter "Name='$serviceName'" -ErrorAction SilentlyContinue
} while ($null -ne $service -and (Get-Date) -lt $deadline)
if ($null -ne $service) { throw "Windows service registration remains after uninstall." }
if (Test-Path -LiteralPath $installDirectory) { throw "Program Files installation directory remains after uninstall." }
# Uninstall must leave the protected key file in ProgramData. Do not
# inspect its DACL or contents as the non-service test identity.
if (-not (Test-Path -LiteralPath $keyDirectory -PathType Container) -or
    -not (Test-Path -LiteralPath $keyFile -PathType Leaf)) {
    throw "Uninstall removed the protected Persons key from ProgramData."
}

Write-Host 'PASS: Program Files install, LocalService service SID, DPAPI startup read, repair ACL preservation, and uninstall key preservation.'
