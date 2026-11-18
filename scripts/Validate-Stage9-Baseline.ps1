[CmdletBinding()]
param(
    [switch] $SkipBuild,
    [switch] $SkipTests,
    [switch] $SkipPythonGates
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if (-not (Test-Path -LiteralPath (Join-Path $root 'MDGsm.sln'))) { throw 'MDGsm.sln was not found; run this script from the extracted Stage9 package.' }
    $sdkList = & dotnet --list-sdks
    if ($LASTEXITCODE -ne 0 -or -not ($sdkList | Where-Object { $_ -match '^10\.' })) {
        throw 'A .NET 10 SDK is required. No database operation has been performed.'
    }
    if (-not $SkipBuild) {
        & dotnet restore .\MDGsm.sln
        if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
        & dotnet build .\MDGsm.sln --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }
    }
    if (-not $SkipTests) {
        if ($SkipBuild) { Write-Host 'Build was skipped by explicit request; tests may use stale outputs.' -ForegroundColor Yellow }
        & dotnet test .\MDGsm.sln --configuration Release --no-build --logger 'trx;LogFileName=mdgsm-stage9.trx'
        if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }
    }
    $pythonGatesSkipped = $false
    if (-not $SkipPythonGates) {
        # Windows may expose a Microsoft Store execution alias named python.exe even
        # when Python is not installed. Do not mistake that alias for a working runtime.
        $pythonRuntime = $null
        foreach ($commandName in @('python', 'python3')) {
            $candidate = Get-Command $commandName -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $candidate) { continue }
            try {
                $versionOutput = @(& $candidate.Source --version 2>&1)
                $versionExitCode = $LASTEXITCODE
                $versionText = ($versionOutput -join ' ').Trim()
                $versionMatch = [regex]::Match($versionText, 'Python\s+(?<major>\d+)\.(?<minor>\d+)')
                if ($versionExitCode -eq 0 -and $versionMatch.Success -and
                    [int]$versionMatch.Groups['major'].Value -eq 3 -and
                    [int]$versionMatch.Groups['minor'].Value -ge 12) {
                    $pythonRuntime = [pscustomobject]@{
                        Executable = $candidate.Source
                        PrefixArgs = @()
                        Version = $versionText
                    }
                    break
                }
            }
            catch { }
        }
        # Also support the Python Launcher when python.exe is not on PATH.
        if (-not $pythonRuntime) {
            $launcher = Get-Command py -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($launcher) {
                try {
                    $versionOutput = @(& $launcher.Source -3 --version 2>&1)
                    $versionExitCode = $LASTEXITCODE
                    $versionText = ($versionOutput -join ' ').Trim()
                    $versionMatch = [regex]::Match($versionText, 'Python\s+(?<major>\d+)\.(?<minor>\d+)')
                    if ($versionExitCode -eq 0 -and $versionMatch.Success -and
                        [int]$versionMatch.Groups['major'].Value -eq 3 -and
                        [int]$versionMatch.Groups['minor'].Value -ge 12) {
                        $pythonRuntime = [pscustomobject]@{
                            Executable = $launcher.Source
                            PrefixArgs = @('-3')
                            Version = $versionText
                        }
                    }
                }
                catch { }
            }
        }
        if (-not $pythonRuntime) {
            throw @'
Python 3.12 or newer is required for the Persons source-preparation gates, but no working interpreter was found.
A WindowsApps/Microsoft Store alias named python.exe is not a Python runtime. Install Python 3.12+ (or the Python Launcher), open a new PowerShell window, and rerun this script.
The .NET restore/build/test steps above have completed; the Python source gates and their policy self-tests have NOT run. This is an environment prerequisite failure, not evidence that those gates passed or that the source gates themselves failed.
'@
        }
        Write-Host ("Using Python runtime: {0} ({1})" -f $pythonRuntime.Executable, $pythonRuntime.Version) -ForegroundColor Cyan
        $pythonArgs = @($pythonRuntime.PrefixArgs) + @('.\scripts\ci\validate_persons_gates.py', '--gate', 'all')
        & $pythonRuntime.Executable @pythonArgs
        if ($LASTEXITCODE -ne 0) { throw "Persons source preparation gates failed with exit code $LASTEXITCODE. See gate output above." }
        $pythonArgs = @($pythonRuntime.PrefixArgs) + @('-m', 'unittest', 'discover', '-s', '.\scripts\ci', '-p', 'test_*.py', '-v')
        & $pythonRuntime.Executable @pythonArgs
        if ($LASTEXITCODE -ne 0) { throw "Persons gate-policy self-tests failed with exit code $LASTEXITCODE." }
    }
    else {
        $pythonGatesSkipped = $true
        Write-Host 'PARTIAL VALIDATION: Python source gates and fail-closed policy self-tests were explicitly skipped; this is NOT a full pass and does NOT authorize G1.' -ForegroundColor Yellow
    }
    if ($pythonGatesSkipped) {
        Write-Host 'PARTIAL: .NET baseline checks completed, but Python gate checks were skipped.' -ForegroundColor Yellow
    }
    else {
        Write-Host 'PASS: local baseline validation completed. This does not prove remote SCM branch protection is active.' -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
