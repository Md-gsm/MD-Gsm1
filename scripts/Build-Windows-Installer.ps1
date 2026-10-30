[CmdletBinding()]
param(
    [switch] $SkipTests,
    [string] $InnoSetupPath,
    [string] $SigningCertificatePath,
    [string] $SigningCertificatePassword,
    [string] $TimestampServer = 'https://timestamp.digicert.com',
    [switch] $RequireAuthenticodeSignature
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'MDGsm.sln'
$desktopProject = Join-Path $repoRoot 'src\MDGsm.Desktop\MDGsm.Desktop.csproj'
$publishDir = Join-Path $repoRoot 'artifacts\windows-publish'
$outputDir = Join-Path $repoRoot 'artifacts\installer'
$issFile = Join-Path $repoRoot 'installer\MDGsm-Persons-Preview.iss'
$signScript = Join-Path $PSScriptRoot 'Sign-Windows-Authenticode.ps1'
$installerName = 'MD-Gsm-Persons-Preview-Setup-v0.2.11.exe'
$installerPath = Join-Path $outputDir $installerName
$signatureStatusPath = $installerPath + '.signature-status.txt'
$shaPath = $installerPath + '.sha256'
$generatedPfxPath = $null

try {
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
            (Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
        ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        $InnoSetupPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    }
    if ([string]::IsNullOrWhiteSpace($InnoSetupPath) -or -not (Test-Path -LiteralPath $InnoSetupPath)) {
        throw 'Inno Setup 6 (ISCC.exe) is required. Install it once or pass -InnoSetupPath. Python is not required for this installer build.'
    }

    if ([string]::IsNullOrWhiteSpace($SigningCertificatePassword)) {
        $SigningCertificatePassword = $env:WINDOWS_CODESIGN_CERTIFICATE_PASSWORD
    }

    $certificateBase64 = $env:WINDOWS_CODESIGN_CERTIFICATE_BASE64
    if (-not [string]::IsNullOrWhiteSpace($certificateBase64)) {
        if (-not [string]::IsNullOrWhiteSpace($SigningCertificatePath)) {
            throw 'Specify either -SigningCertificatePath or WINDOWS_CODESIGN_CERTIFICATE_BASE64, not both.'
        }
        if ([string]::IsNullOrWhiteSpace($SigningCertificatePassword)) {
            throw 'WINDOWS_CODESIGN_CERTIFICATE_PASSWORD is required when a PFX certificate secret is supplied.'
        }
        try {
            $certificateBytes = [Convert]::FromBase64String($certificateBase64)
        }
        catch {
            throw 'WINDOWS_CODESIGN_CERTIFICATE_BASE64 is not valid Base64.'
        }
        $tempDirectory = if (-not [string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) { $env:RUNNER_TEMP } else { [IO.Path]::GetTempPath() }
        $generatedPfxPath = Join-Path $tempDirectory ("mdgsm-code-signing-{0}.pfx" -f [guid]::NewGuid().ToString('N'))
        [IO.File]::WriteAllBytes($generatedPfxPath, $certificateBytes)
        $SigningCertificatePath = $generatedPfxPath
    }

    $hasSigningCertificate = -not [string]::IsNullOrWhiteSpace($SigningCertificatePath)
    if ($hasSigningCertificate -and [string]::IsNullOrWhiteSpace($SigningCertificatePassword)) {
        throw 'A password is required for the Authenticode PFX certificate.'
    }
    if ($hasSigningCertificate -and -not (Test-Path -LiteralPath $SigningCertificatePath -PathType Leaf)) {
        throw 'The Authenticode PFX certificate file does not exist.'
    }
    if ($RequireAuthenticodeSignature -and -not $hasSigningCertificate) {
        throw 'Code signing was required, but no trusted Authenticode certificate was supplied. Add the Windows code-signing secrets before producing a signed release.'
    }
    if ($hasSigningCertificate -and -not (Test-Path -LiteralPath $signScript -PathType Leaf)) {
        throw 'The Authenticode signing helper script is missing.'
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
    & dotnet publish $desktopProject --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=false -p:IncludeNativeLibrariesForSelfExtract=true -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw 'Self-contained Desktop publish failed.' }

    $exePath = Join-Path $publishDir 'MDGsm.Desktop.exe'
    if (-not (Test-Path -LiteralPath $exePath)) { throw 'Published Desktop executable is missing.' }
    if (-not (Test-Path -LiteralPath (Join-Path $publishDir 'Assets\menu-catalog-fa.json'))) {
        throw 'Published menu catalog is missing.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $publishDir 'appsettings.json'))) {
        throw 'Published appsettings.json is missing.'
    }

    if ($hasSigningCertificate) {
        Write-Host '== Sign published Desktop executable ==' -ForegroundColor Cyan
        & $signScript -Path $exePath -CertificatePath $SigningCertificatePath -CertificatePassword $SigningCertificatePassword -TimestampServer $TimestampServer
    }
    else {
        Write-Warning 'No trusted Authenticode certificate was supplied. The preview installer will be unsigned and Windows may display "Unknown Publisher".'
    }

    Write-Host '== Compile Windows installer ==' -ForegroundColor Cyan
    & $InnoSetupPath "/DPublishDir=$publishDir" "/DOutputDir=$outputDir" $issFile
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
    if (-not (Test-Path -LiteralPath $installerPath)) { throw 'Installer EXE was not produced.' }

    if ($hasSigningCertificate) {
        Write-Host '== Sign Windows installer ==' -ForegroundColor Cyan
        & $signScript -Path $installerPath -CertificatePath $SigningCertificatePath -CertificatePassword $SigningCertificatePassword -TimestampServer $TimestampServer
        $signatureStatus = @(
            'SIGNED',
            'Both the published application executable and installer were Authenticode-signed and verified.',
            "Timestamp: $TimestampServer",
            'Digest: SHA256'
        )
    }
    else {
        $signatureStatus = @(
            'UNSIGNED',
            'No trusted code-signing certificate was supplied to this build.',
            'Windows may display "Unknown Publisher" when installing.',
            'Configure WINDOWS_CODESIGN_CERTIFICATE_BASE64 and WINDOWS_CODESIGN_CERTIFICATE_PASSWORD with a trusted code-signing PFX before a signed release.'
        )
    }

    Set-Content -LiteralPath $signatureStatusPath -Value $signatureStatus -Encoding ASCII

    $hash = Get-FileHash -LiteralPath $installerPath -Algorithm SHA256
    $hashLine = '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), (Split-Path -Leaf $installerPath)
    Set-Content -LiteralPath $shaPath -Value $hashLine -Encoding ASCII

    Write-Host "PASS: Windows preview installer created: $installerPath" -ForegroundColor Green
    if ($hasSigningCertificate) {
        Write-Host 'PASS: installer and application executable have valid Authenticode signatures and trusted timestamps.' -ForegroundColor Green
    }
    else {
        Write-Warning 'Installer build succeeded but is UNSIGNED. The "Unknown Publisher" warning is not fixed until a trusted code-signing certificate is configured.'
    }
    Write-Host 'This is a UI preview only: no Persons business database, no database migration, no real-data access, and no Python runtime dependency.' -ForegroundColor Yellow
}
finally {
    if ($generatedPfxPath -and (Test-Path -LiteralPath $generatedPfxPath)) {
        Remove-Item -LiteralPath $generatedPfxPath -Force
    }
}
