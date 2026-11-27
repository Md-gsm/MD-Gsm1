[CmdletBinding()]
param(
    [switch] $SkipTests,
    [string] $InnoSetupPath
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'MDGsm.sln'
$desktopProject = Join-Path $repoRoot 'src\Host\Desktop\MDGsm.Desktop.csproj'
$apiProject = Join-Path $repoRoot 'src\Host\Api\MDGsm.Api.csproj'
$publishRoot = Join-Path $repoRoot 'artifacts\windows-publish'
$desktopPublishDir = Join-Path $publishRoot 'desktop'
$apiPublishDir = Join-Path $publishRoot 'api'
$outputDir = Join-Path $repoRoot 'artifacts\installer'
$issFile = Join-Path $repoRoot 'installer\MDGsm-Setup.iss'
$clientIssFile = Join-Path $repoRoot 'installer\MDGsm-Client.iss'
$centralProvisioner = Join-Path $repoRoot 'installer\Provision-MDGsmCentralDatabase.ps1'
$networkProvisioner = Join-Path $repoRoot 'installer\Configure-MDGsmNetwork.ps1'
$clientConnectionImporter = Join-Path $repoRoot 'installer\Import-MDGsmClientConnection.ps1'

if (-not $IsWindows -and $env:OS -ne 'Windows_NT') {
    throw 'The Windows installer must be built on Windows. No application or database was changed.'
}
foreach ($requiredPath in @($solution, $desktopProject, $apiProject, $issFile, $clientIssFile, $centralProvisioner, $networkProvisioner, $clientConnectionImporter)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) { throw "Required build input was not found: $requiredPath" }
}

$sdks = & dotnet --list-sdks 2>&1
if ($LASTEXITCODE -ne 0 -or -not ($sdks | Where-Object { $_ -match '^10\.' })) {
    throw '.NET 10 SDK is required to build the Windows installer.'
}

if ([string]::IsNullOrWhiteSpace($InnoSetupPath)) {
    $candidates = @(
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source),
        (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $InnoSetupPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($InnoSetupPath) -or -not (Test-Path -LiteralPath $InnoSetupPath)) {
    throw 'Inno Setup 6 (ISCC.exe) is required. Install it once or pass -InnoSetupPath. Python is not required.'
}

Write-Host '== Restore ==' -ForegroundColor Cyan
& dotnet restore $solution
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }

Write-Host '== Release build ==' -ForegroundColor Cyan
& dotnet build $solution --configuration Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }

if (-not $SkipTests) {
    Write-Host '== .NET tests ==' -ForegroundColor Cyan
    & dotnet test $solution --configuration Release --no-build --filter 'Category!=Integration' --logger 'trx;LogFileName=mdgsm-installer-tests.trx'
    if ($LASTEXITCODE -ne 0) { throw 'The .NET test suite failed; installer packaging is blocked.' }
}

if (Test-Path -LiteralPath $publishRoot) { Remove-Item -LiteralPath $publishRoot -Recurse -Force }
New-Item -ItemType Directory -Path $desktopPublishDir -Force | Out-Null
New-Item -ItemType Directory -Path $apiPublishDir -Force | Out-Null
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

Write-Host '== Publish self-contained Windows x64 Desktop ==' -ForegroundColor Cyan
& dotnet publish $desktopProject --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=false -p:IncludeNativeLibrariesForSelfExtract=true -o $desktopPublishDir
if ($LASTEXITCODE -ne 0) { throw 'Self-contained Desktop publish failed.' }

Write-Host '== Publish self-contained Windows x64 API service ==' -ForegroundColor Cyan
& dotnet publish $apiProject --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=false -p:IncludeNativeLibrariesForSelfExtract=true -o $apiPublishDir
if ($LASTEXITCODE -ne 0) { throw 'Self-contained API publish failed.' }

$desktopExe = Join-Path $desktopPublishDir 'MDGsm.Desktop.exe'
$apiExe = Join-Path $apiPublishDir 'MDGsm.Api.exe'
if (-not (Test-Path -LiteralPath $desktopExe)) { throw 'Published Desktop executable is missing.' }
if (-not (Test-Path -LiteralPath $apiExe)) { throw 'Published API executable is missing.' }
foreach ($requiredFile in @(
    (Join-Path $desktopPublishDir 'Assets\menu-catalog-fa.json'),
    (Join-Path $desktopPublishDir 'appsettings.json'),
    (Join-Path $apiPublishDir 'appsettings.json'),
    (Join-Path $apiPublishDir 'appsettings.Production.json')
)) {
    if (-not (Test-Path -LiteralPath $requiredFile)) { throw "Published file is missing: $requiredFile" }
}

Write-Host '== Compile system-wide Windows installer =='
& $InnoSetupPath "/DPublishDir=$desktopPublishDir" "/DApiPublishDir=$apiPublishDir" "/DOutputDir=$outputDir" $issFile
if ($LASTEXITCODE -ne 0) { throw 'Central-server Inno Setup compilation failed.' }

$centralInstaller = Join-Path $outputDir 'MD-Gsm-Setup-v0.3.0.exe'
if (-not (Test-Path -LiteralPath $centralInstaller)) { throw 'Central-server installer EXE was not produced.' }

& $InnoSetupPath "/DPublishDir=$desktopPublishDir" "/DOutputDir=$outputDir" $clientIssFile
if ($LASTEXITCODE -ne 0) { throw 'Client-only Inno Setup compilation failed.' }

$clientInstaller = Join-Path $outputDir 'MD-Gsm-Client-Setup-v0.3.0.exe'
if (-not (Test-Path -LiteralPath $clientInstaller)) { throw 'Client-only installer EXE was not produced.' }

foreach ($installer in @($centralInstaller, $clientInstaller)) {
    $hash = Get-FileHash -LiteralPath $installer -Algorithm SHA256
    $hashLine = '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), (Split-Path -Leaf $installer)
    Set-Content -LiteralPath ($installer + '.sha256') -Value $hashLine -Encoding ASCII
    Write-Host "PASS: Windows installer created: $installer" -ForegroundColor Green
}
Write-Host 'Central setup provisions PostgreSQL 18, automatically detects an eligible private Windows network, configures scoped HTTPS and exports a public client-connection bundle. Client setup installs only Desktop and imports the bundle with normal TLS validation. Schema migrations and feature activation remain separate.' -ForegroundColor Yellow
