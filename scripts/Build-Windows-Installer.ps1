[CmdletBinding()]
param(
    [switch] $SkipTests,
    [string] $InnoSetupPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'MDGsm.sln'
$desktopProject = Join-Path $repoRoot 'src\Host\Desktop\MDGsm.Desktop.csproj'
$publishDir = Join-Path $repoRoot 'artifacts\windows-publish'
$outputDir = Join-Path $repoRoot 'artifacts\installer'
$issFile = Join-Path $repoRoot 'installer\MDGsm-Persons-Preview.iss'

if (-not $IsWindows -and $env:OS -ne 'Windows_NT') {
    throw 'The Windows installer must be built on Windows. No application or database was changed.'
}
if (-not (Test-Path -LiteralPath $solution)) { throw 'MDGsm.sln was not found.' }
if (-not (Test-Path -LiteralPath $desktopProject)) { throw 'Desktop project was not found.' }
if (-not (Test-Path -LiteralPath $issFile)) { throw 'Inno Setup definition was not found.' }

$sdks = & dotnet --list-sdks 2>&1
if ($LASTEXITCODE -ne 0 -or -not ($sdks | Where-Object { $_ -match '^10\.' })) {
    throw '.NET 10 SDK is required to build the Windows preview installer.'
}

if ([string]::IsNullOrWhiteSpace($InnoSetupPath)) {
    $candidates = @(
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source),
        (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $InnoSetupPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($InnoSetupPath) -or -not (Test-Path -LiteralPath $InnoSetupPath)) {
    throw 'Inno Setup 6 (ISCC.exe) is required. Install it once or pass -InnoSetupPath. Python is not required for this installer build.'
}

Write-Host '== Restore ==' -ForegroundColor Cyan
& dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

Write-Host '== Release build ==' -ForegroundColor Cyan
& dotnet build $solution --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }

if (-not $SkipTests) {
    Write-Host '== .NET tests ==' -ForegroundColor Cyan
    & dotnet test $solution --configuration Release --no-build --logger 'trx;LogFileName=mdgsm-installer-tests.trx'
    if ($LASTEXITCODE -ne 0) { throw 'The .NET test suite failed; installer packaging is blocked.' }
}

if (Test-Path -LiteralPath $publishDir) { Remove-Item -LiteralPath $publishDir -Recurse -Force }
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

Write-Host '== Publish self-contained Windows x64 Desktop preview ==' -ForegroundColor Cyan
& dotnet publish $desktopProject --configuration Release --runtime win-x64 --self-contained true `
    -p:PublishSingleFile=false -p:PublishReadyToRun=false -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'Self-contained Desktop publish failed.' }

$exePath = Join-Path $publishDir 'MDGsm.Desktop.exe'
if (-not (Test-Path -LiteralPath $exePath)) { throw 'Published Desktop executable is missing.' }
if (-not (Test-Path -LiteralPath (Join-Path $publishDir 'Assets\menu-catalog-fa.json'))) {
    throw 'Published menu catalog is missing.'
}
if (-not (Test-Path -LiteralPath (Join-Path $publishDir 'appsettings.json'))) {
    throw 'Published appsettings.json is missing.'
}

Write-Host '== Compile Windows installer ==' -ForegroundColor Cyan
& $InnoSetupPath "/DPublishDir=$publishDir" "/DOutputDir=$outputDir" $issFile
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }

$installer = Join-Path $outputDir 'MD-Gsm-Persons-Preview-Setup-v0.2.10.exe'
if (-not (Test-Path -LiteralPath $installer)) { throw 'Installer EXE was not produced.' }

$hash = Get-FileHash -LiteralPath $installer -Algorithm SHA256
$hashLine = '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), (Split-Path -Leaf $installer)
Set-Content -LiteralPath ($installer + '.sha256') -Value $hashLine -Encoding ASCII
Write-Host "PASS: Windows preview installer created: $installer" -ForegroundColor Green
Write-Host 'This is a UI preview only: no Persons business database, no database migration, no real-data access, and no Python runtime dependency.' -ForegroundColor Yellow
