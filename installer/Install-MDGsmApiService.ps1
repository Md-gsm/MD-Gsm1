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

function Invoke-Sc([string[]] $Arguments) {
    $output = & $scExe @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Service Control Manager operation '$($Arguments[0])' failed: $($output -join ' ')"
    }
}

function Get-AccountSid([string] $AccountName) {
    return ([System.Security.Principal.NTAccount]::new($AccountName)).Translate(
        [System.Security.Principal.SecurityIdentifier])
}

function Assert-ProtectedAcl([string] $Path, [string] $ServiceSid, [string] $SystemSid, [bool] $IsDirectory) {
    $acl = Get-Acl -LiteralPath $Path
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

if (-not (Test-Path -LiteralPath $apiExe -PathType Leaf)) { throw "The API executable was not found under the selected Program Files installation." }
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw "The MD-Gsm service installer must run elevated." }

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

Invoke-Sc @('sidtype', $serviceName, 'unrestricted')
Invoke-Sc @('description', $serviceName, 'MD-Gsm local API. Runs as LocalService with a dedicated service SID.')
Invoke-Sc @('failure', $serviceName, 'reset=', '86400', 'actions=', 'restart/5000/restart/15000/restart/60000')

$directoryExisted = Test-Path -LiteralPath $keyDirectory -PathType Container
if ($directoryExisted -and -not (Test-Path -LiteralPath $keyFile -PathType Leaf)) {
    throw "The existing Persons key directory has no key file. Refusing to generate a replacement key."
}
if (-not $directoryExisted) {
    New-Item -ItemType Directory -Path $keyDirectory -Force | Out-Null
    & $apiExe '--provision-persons-workflow-key' $keyFile $serviceName
    if ($LASTEXITCODE -ne 0) { throw "One-time DPAPI key provisioning failed; the key was not replaced." }
}

$serviceSid = ([System.Security.Principal.NTAccount]::new("NT SERVICE\$serviceName")).Translate([System.Security.Principal.SecurityIdentifier])
$systemSid = (Get-AccountSid 'NT AUTHORITY\SYSTEM').Value
Assert-ProtectedAcl -Path $keyDirectory -ServiceSid $serviceSid.Value -SystemSid $systemSid -IsDirectory $true
Assert-ProtectedAcl -Path $keyFile -ServiceSid $serviceSid.Value -SystemSid $systemSid -IsDirectory $false

$service = Get-CimInstance Win32_Service -Filter "Name='$serviceName'"
if ($service.StartName -ne $serviceAccount) { throw "MDGsmApi is not configured to run as the approved LocalService account." }
Invoke-Sc @('start', $serviceName)

$deadline = (Get-Date).AddSeconds(40)
do {
    Start-Sleep -Milliseconds 500
    $service = Get-CimInstance Win32_Service -Filter "Name='$serviceName'"
} while ($service.State -ne 'Running' -and (Get-Date) -lt $deadline)
if ($service.State -ne 'Running') { throw "MDGsmApi did not reach Running state. Key access or service startup validation may have failed." }

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

Write-Host 'MD-Gsm API service is running as LocalService with the MDGsmApi service SID.'
Write-Host 'The DPAPI key is outside Program Files and remains unchanged during repair.'
