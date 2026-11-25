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
& $InstallerPath /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-
if ($LASTEXITCODE -ne 0) { throw "The system-wide installer returned exit code $LASTEXITCODE." }

if (-not (Test-Path -LiteralPath (Join-Path $installDirectory 'MDGsm.Desktop.exe'))) { throw "Desktop is not under Program Files\MD-Gsm." }
if (-not (Test-Path -LiteralPath (Join-Path $installDirectory 'Api\MDGsm.Api.exe'))) { throw "API is not under Program Files\MD-Gsm\Api." }
if (-not (Test-Path -LiteralPath $keyFile -PathType Leaf)) { throw "The DPAPI key was not provisioned under ProgramData." }
if (Get-ChildItem -LiteralPath $keyDirectory -Recurse -File | Where-Object { $_.Extension -in '.exe','.dll' }) {
    throw "Executable application files must not be installed under ProgramData."
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
$keyHashBeforeRepair = (Get-FileHash -LiteralPath $keyFile -Algorithm SHA256).Hash

Write-Host '== Run service installer again (repair simulation) =='
& (Join-Path $installDirectory 'Install-MDGsmApiService.ps1') -InstallDirectory $installDirectory
if ($LASTEXITCODE -ne 0) { throw "Repair simulation failed." }
$keyHashAfterRepair = (Get-FileHash -LiteralPath $keyFile -Algorithm SHA256).Hash
if ($keyHashBeforeRepair -ne $keyHashAfterRepair) { throw "Repair replaced or modified the DPAPI key file." }

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
if (-not (Test-Path -LiteralPath $keyFile -PathType Leaf)) { throw "Uninstall removed the protected ProgramData key." }
$keyHashAfterUninstall = (Get-FileHash -LiteralPath $keyFile -Algorithm SHA256).Hash
if ($keyHashBeforeRepair -ne $keyHashAfterUninstall) { throw "Uninstall modified the ProgramData key." }

Write-Host 'PASS: Program Files install, LocalService service SID, DPAPI startup read, repair no-overwrite, and uninstall key preservation.'
