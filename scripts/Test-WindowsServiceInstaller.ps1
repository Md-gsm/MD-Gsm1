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
# Setup/admin is deliberately not allowed to read the protected key. Inspect only
# the security descriptors; the API's hosted startup validator proves actual read/unprotect.
$keyDirectoryAclBeforeRepair = Get-Acl -LiteralPath $keyDirectory
$keyFileAclBeforeRepair = Get-Acl -LiteralPath $keyFile
if (-not $keyDirectoryAclBeforeRepair.AreAccessRulesProtected -or -not $keyFileAclBeforeRepair.AreAccessRulesProtected) {
    throw "The Persons key directory and file must both have protected DACLs."
}

$service = Get-CimInstance Win32_Service -Filter "Name='$serviceName'"
if ($null -eq $service -or $service.State -ne 'Running') { throw "The installed API Windows service is not running." }
if ($service.StartName -ne 'NT AUTHORITY\LocalService') { throw "The API service is not running as LocalService." }

$serviceSid = ([Security.Principal.NTAccount]::new("NT SERVICE\$serviceName")).Translate([Security.Principal.SecurityIdentifier]).Value
$systemSid = ([Security.Principal.NTAccount]::new('NT AUTHORITY\SYSTEM')).Translate([Security.Principal.SecurityIdentifier]).Value
foreach ($path in @($keyDirectory, $keyFile)) {
    $acl = Get-Acl -LiteralPath $path
    if (-not $acl.AreAccessRulesProtected) { throw "Protected ACL is not enabled on $path." }
    $allowed = @($acl.Access | Where-Object { $_.AccessControlType -eq [System.Security.AccessControl.AccessControlType]::Allow })
    if ($allowed.Count -ne 2) { throw "Unexpected ACL principal count on $path." }
    $sids = @($allowed | ForEach-Object {
        $_.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value
    } | Sort-Object -Unique)
    if (($sids -join ',') -ne ((@($serviceSid,$systemSid) | Sort-Object -Unique) -join ',')) {
        throw "Only the service-specific SID and LocalSystem may have access to $path."
    }
}
$directorySddlBeforeRepair = $keyDirectoryAclBeforeRepair.Sddl
$fileSddlBeforeRepair = $keyFileAclBeforeRepair.Sddl

Write-Host '== Run service installer again (repair simulation) =='
& (Join-Path $installDirectory 'Install-MDGsmApiService.ps1') -InstallDirectory $installDirectory
if ($LASTEXITCODE -ne 0) { throw "Repair simulation failed." }
$keyDirectoryAclAfterRepair = Get-Acl -LiteralPath $keyDirectory
$keyFileAclAfterRepair = Get-Acl -LiteralPath $keyFile
if ($directorySddlBeforeRepair -ne $keyDirectoryAclAfterRepair.Sddl -or
    $fileSddlBeforeRepair -ne $keyFileAclAfterRepair.Sddl) {
    throw "Repair changed the protected Persons key DACL."
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
# Use the security descriptor as the existence check: the setup identity must
# remain unable to open key contents after application uninstall.
$keyFileAclAfterUninstall = Get-Acl -LiteralPath $keyFile
$keyDirectoryAclAfterUninstall = Get-Acl -LiteralPath $keyDirectory
if ($fileSddlBeforeRepair -ne $keyFileAclAfterUninstall.Sddl -or
    $directorySddlBeforeRepair -ne $keyDirectoryAclAfterUninstall.Sddl) {
    throw "Uninstall removed or modified the protected ProgramData key security objects."
}

Write-Host 'PASS: Program Files install, LocalService service SID, DPAPI startup read, repair ACL preservation, and uninstall key preservation.'
