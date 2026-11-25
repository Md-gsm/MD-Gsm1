[CmdletBinding()]
param([Parameter(Mandatory = $true)][string] $InstallDirectory)

$ErrorActionPreference = 'Stop'
$serviceName = 'MDGsmApi'
$serviceAccount = 'NT AUTHORITY\LocalService'
$apiExe = Join-Path $InstallDirectory 'Api\MDGsm.Api.exe'
$keyDirectory = Join-Path $env:ProgramData 'MD-Gsm\Keys\Persons\MDGsmApi'
$keyFile = Join-Path $keyDirectory 'workflow-key.dpapi'
$scExe = Join-Path $env:windir 'System32\sc.exe'
$quotedApiExe = [string]([char]34) + $apiExe + [char]34

$script:setupLogPath = Join-Path $env:TEMP 'MD-Gsm-ApiServiceSetup.log'
$script:setupStage = 'initializing'
try {
    Set-Content -LiteralPath $script:setupLogPath -Value ("{0:o} setup script started" -f (Get-Date)) -Encoding UTF8
} catch {
    # Continue with installation; the original operational failure remains authoritative.
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

function Get-AccountSid([string] $AccountName) {
    return ([System.Security.Principal.NTAccount]::new($AccountName)).Translate(
        [System.Security.Principal.SecurityIdentifier])
}

function Assert-ProtectedAcl([string] $Path, [string] $ServiceSid, [string] $SystemSid, [bool] $IsDirectory) {
    $acl = Get-Acl -LiteralPath $Path
    Write-SetupLog ("ACL inspected: Path=$Path; Protected=$($acl.AreAccessRulesProtected)")
    foreach ($entry in @($acl.Access)) {
        $sidText = try { $entry.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value } catch { [string]$entry.IdentityReference }
        Write-SetupLog ("ACL ACE: SID=$sidText; Type=$($entry.AccessControlType); Rights=$($entry.FileSystemRights); Inherited=$($entry.IsInherited)")
    }
    if (-not $acl.AreAccessRulesProtected) { throw "The protected ACL flag is missing on the Persons workflow key location." }
    $rules = @($acl.Access | Where-Object { $_.AccessControlType -eq [System.Security.AccessControl.AccessControlType]::Allow })
    if ($rules.Count -ne 2) { throw "The Persons workflow key ACL must contain exactly the service SID and LocalSystem." }

    $ruleSids = @($rules | ForEach-Object {
        $_.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value
    } | Sort-Object -Unique)
    $expectedSids = @($ServiceSid, $SystemSid) | Sort-Object -Unique
    if (($ruleSids -join ',') -ne ($expectedSids -join ',')) { throw "The Persons workflow key ACL contains an unexpected principal." }

    $serviceRule = $rules | Where-Object {
        $_.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value -eq $ServiceSid
    } | Select-Object -First 1
    if ($null -eq $serviceRule) { throw "The API service SID has no key access rule." }

    $rights = [int]$serviceRule.FileSystemRights
    if ($IsDirectory) {
        $required = [int][System.Security.AccessControl.FileSystemRights]::ReadAndExecute
        if (($rights -band $required) -ne $required) { throw "The API service SID lacks directory read/execute access." }
    } else {
        $required = [int][System.Security.AccessControl.FileSystemRights]::ReadData
        $write = [int][System.Security.AccessControl.FileSystemRights]::WriteData
        if (($rights -band $required) -ne $required -or ($rights -band $write) -ne 0) {
            throw "The API service SID must have read-only access to the protected key file."
        }
    }
}

try {
$script:setupStage = 'validate installation and elevation'
Write-SetupLog 'Validating API executable and elevated setup identity.'
if (-not (Test-Path -LiteralPath $apiExe -PathType Leaf)) { throw "The API executable was not found under the selected Program Files installation." }
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw "The MD-Gsm service installer must run elevated." }

$script:setupStage = 'create or configure Windows service'
Write-SetupLog 'Checking whether MDGsmApi service already exists.'
$existingService = Get-CimInstance Win32_Service -Filter "Name='$serviceName'" -ErrorAction SilentlyContinue
if ($null -eq $existingService) {
    Invoke-Sc @('create', $serviceName, 'binPath=', $quotedApiExe, 'start=', 'auto', 'obj=', $serviceAccount, 'DisplayName=', '"MD-Gsm API"')
} else {
    if ($existingService.State -eq 'Running') {
        Invoke-Sc @('stop', $serviceName)
        $deadline = (Get-Date).AddSeconds(30)
        do {
            Start-Sleep -Milliseconds 500
            $existingService = Get-CimInstance Win32_Service -Filter "Name='$serviceName'"
        } while ($existingService.State -ne 'Stopped' -and (Get-Date) -lt $deadline)
        if ($existingService.State -ne 'Stopped') { throw "The existing MD-Gsm API service did not stop for repair." }
    }
    Invoke-Sc @('config', $serviceName, 'binPath=', $quotedApiExe, 'start=', 'auto', 'obj=', $serviceAccount, 'DisplayName=', '"MD-Gsm API"')
}

$script:setupStage = 'configure service SID and recovery policy'
Write-SetupLog 'Configuring service SID, description, and recovery actions.'
Invoke-Sc @('sidtype', $serviceName, 'unrestricted')
Invoke-Sc @('description', $serviceName, 'MD-Gsm local API. Runs as LocalService with a dedicated service SID.')
Invoke-Sc @('failure', $serviceName, 'reset=', '86400', 'actions=', 'restart/5000/restart/15000/restart/60000')

$script:setupStage = 'provision or preserve DPAPI key'
Write-SetupLog 'Checking ProgramData key directory existence without reading key bytes.'
$directoryExisted = Test-Path -LiteralPath $keyDirectory -PathType Container
if ($directoryExisted -and -not (Test-Path -LiteralPath $keyFile -PathType Leaf)) {
    throw "The existing Persons key directory has no key file. Refusing to generate a replacement key."
}
if (-not $directoryExisted) {
    New-Item -ItemType Directory -Path $keyDirectory -Force | Out-Null
    Write-SetupLog 'Starting one-time explicit DPAPI key provisioning for MDGsmApi service SID.'
    $provisionOutput = & $apiExe '--provision-persons-workflow-key' $keyFile $serviceName 2>&1
    $provisionExitCode = $LASTEXITCODE
    foreach ($line in @($provisionOutput)) { Write-SetupLog ('Provisioning output: ' + [string]$line) }
    Write-SetupLog ('Provisioning exit code: ' + $provisionExitCode)
    if ($provisionExitCode -ne 0) { throw "One-time DPAPI key provisioning failed; the key was not replaced." }
}

$script:setupStage = 'validate service and key ACLs'
Write-SetupLog 'Resolving service and SYSTEM SIDs, then validating protected directory and key ACLs.'
$serviceSid = ([System.Security.Principal.NTAccount]::new("NT SERVICE\$serviceName")).Translate([System.Security.Principal.SecurityIdentifier])
$systemSid = (Get-AccountSid 'NT AUTHORITY\SYSTEM').Value
Assert-ProtectedAcl -Path $keyDirectory -ServiceSid $serviceSid.Value -SystemSid $systemSid -IsDirectory $true
Assert-ProtectedAcl -Path $keyFile -ServiceSid $serviceSid.Value -SystemSid $systemSid -IsDirectory $false

$script:setupStage = 'validate service account and start service'
Write-SetupLog 'Validating configured LocalService account before requesting service start.'
$service = Get-CimInstance Win32_Service -Filter "Name='$serviceName'"
if ($service.StartName -ne $serviceAccount) { throw "MDGsmApi is not configured to run as the approved LocalService account." }
Write-SetupLog 'Requesting MDGsmApi start through Service Control Manager.'
Invoke-Sc @('start', $serviceName)
Write-SetupLog 'SCM start command returned success.'

$deadline = (Get-Date).AddSeconds(40)
do {
    Start-Sleep -Milliseconds 500
    $service = Get-CimInstance Win32_Service -Filter "Name='$serviceName'"
} while ($service.State -ne 'Running' -and (Get-Date) -lt $deadline)
if ($service.State -ne 'Running') { Write-SetupLog ("Service state after wait: " + $service.State + "; Win32ExitCode=" + $service.ExitCode + "; ServiceSpecificExitCode=" + $service.ServiceSpecificExitCode); throw "MDGsmApi did not reach Running state. Key access or service startup validation may have failed." }

$script:setupStage = 'verify loopback API liveness'
Write-SetupLog 'Service reached Running; waiting for loopback liveness endpoint.'
$healthUri = 'http://127.0.0.1:5080/health/live'
$healthy = $false
$deadline = (Get-Date).AddSeconds(25)
while (-not $healthy -and (Get-Date) -lt $deadline) {
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $healthUri -TimeoutSec 2
        $healthy = ($response.StatusCode -eq 200)
    } catch {
        Start-Sleep -Milliseconds 500
    }
}
if (-not $healthy) { throw "MDGsmApi started but its loopback liveness endpoint did not respond." }

Write-SetupLog 'Installation completed successfully; service running and loopback liveness verified.'
Write-Host 'MD-Gsm API service is running as LocalService with the MDGsmApi service SID.'
Write-Host 'The DPAPI key is outside Program Files and remains unchanged during repair.'


} catch {
    Write-SetupLog ("FATAL: " + $_.Exception.ToString())
    Write-SetupLog ("FATAL category: " + [string]$_.CategoryInfo)
    Write-SetupLog ("FATAL script stack: " + [string]$_.ScriptStackTrace)
    throw
}
