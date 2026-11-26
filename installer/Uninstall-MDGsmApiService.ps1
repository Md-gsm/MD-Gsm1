[CmdletBinding()]
param([string] $ServiceName = 'MDGsmApi')

$ErrorActionPreference = 'Stop'
$scExe = Join-Path $env:windir 'System32\sc.exe'
$service = Get-CimInstance Win32_Service -Filter "Name='$ServiceName'" -ErrorAction SilentlyContinue
if ($null -ne $service) {
    if ($service.State -ne 'Stopped') {
        & $scExe stop $ServiceName | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Could not stop the MD-Gsm API service during uninstall." }
        $deadline = (Get-Date).AddSeconds(30)
        do {
            Start-Sleep -Milliseconds 500
            $service = Get-CimInstance Win32_Service -Filter "Name='$ServiceName'" -ErrorAction SilentlyContinue
        } while ($null -ne $service -and $service.State -ne 'Stopped' -and (Get-Date) -lt $deadline)
    }

    if ($null -ne $service -and $service.State -ne 'Stopped') {
        throw "The MD-Gsm API service did not reach Stopped state; refusing to remove files while it may still be running."
    }

    & $scExe delete $ServiceName | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Could not remove the MD-Gsm API service registration." }
}

# Intentionally preserve ProgramData key material and all user/database data.
Write-Host 'MD-Gsm API service removed. The protected Persons workflow key was preserved.'
