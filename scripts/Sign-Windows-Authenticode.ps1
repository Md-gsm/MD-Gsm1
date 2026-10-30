[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Path,

    [Parameter(Mandatory = $true)]
    [string] $CertificatePath,

    [Parameter(Mandatory = $true)]
    [string] $CertificatePassword,

    [string] $TimestampServer = 'https://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
    throw "The file to sign does not exist: $Path"
}
if (-not (Test-Path -LiteralPath $CertificatePath -PathType Leaf)) {
    throw 'The Authenticode PFX certificate file was not found.'
}
if ([string]::IsNullOrWhiteSpace($CertificatePassword)) {
    throw 'The Authenticode certificate password is required.'
}
if ([string]::IsNullOrWhiteSpace($TimestampServer)) {
    throw 'An RFC 3161 timestamp server is required.'
}

$storePath = 'Cert:\CurrentUser\My'
$existingThumbprints = @(
    Get-ChildItem -Path $storePath | ForEach-Object { $_.Thumbprint.ToUpperInvariant() }
)
$importedThumbprints = @()
$securePassword = ConvertTo-SecureString -String $CertificatePassword -AsPlainText -Force

try {
    $importedCertificates = @(
        Import-PfxCertificate -FilePath $CertificatePath -CertStoreLocation $storePath -Password $securePassword
    )

    $importedThumbprints = @(
        $importedCertificates |
            Where-Object { $_ -and $_.Thumbprint } |
            ForEach-Object { $_.Thumbprint.ToUpperInvariant() } |
            Select-Object -Unique
    )

    $codeSigningCertificates = @(
        Get-ChildItem -Path $storePath |
            Where-Object {
                $ekuOids = @(
                    $_.EnhancedKeyUsageList |
                        ForEach-Object {
                            if ($_.ObjectId -and $_.ObjectId.Value) { $_.ObjectId.Value }
                            elseif ($_.ObjectId) { [string]$_.ObjectId }
                        }
                )
                $_.HasPrivateKey -and ($ekuOids -contains '1.3.6.1.5.5.7.3.3')
            }
    )

    if ($codeSigningCertificates.Count -ne 1) {
        throw "Expected exactly one code-signing certificate in the imported PFX; found $($codeSigningCertificates.Count)."
    }

    $certificate = $codeSigningCertificates[0]
    $now = Get-Date
    if ($now -lt $certificate.NotBefore -or $now -gt $certificate.NotAfter) {
        throw 'The Authenticode code-signing certificate is not currently valid.'
    }

    $signTool = (Get-Command 'signtool.exe' -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source)
    if ([string]::IsNullOrWhiteSpace($signTool)) {
        $programFilesX86 = [Environment]::GetFolderPath('ProgramFilesX86')
        $sdkBin = Join-Path $programFilesX86 'Windows Kits\10\bin'
        if (Test-Path -LiteralPath $sdkBin) {
            $signTool = Get-ChildItem -Path $sdkBin -Filter 'signtool.exe' -Recurse -File -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
                Sort-Object -Property @{ Expression = { [version]$_.Directory.Parent.Name }; Descending = $true } |
                Select-Object -First 1 -ExpandProperty FullName
        }
    }
    if ([string]::IsNullOrWhiteSpace($signTool) -or -not (Test-Path -LiteralPath $signTool)) {
        throw 'Windows SDK SignTool was not found. Install the Windows 10/11 SDK before signing.'
    }

    $thumbprint = $certificate.Thumbprint
    Write-Host "Authenticode signing: $(Split-Path -Leaf $Path)"
    $signArguments = @(
        'sign', '/s', 'My', '/sha1', $thumbprint,
        '/fd', 'SHA256',
        '/tr', $TimestampServer,
        '/td', 'SHA256',
        '/d', 'MD-Gsm',
        '/v', $Path
    )
    & $signTool @signArguments
    if ($LASTEXITCODE -ne 0) {
        throw "SignTool signing failed with exit code $LASTEXITCODE."
    }

    $verifyArguments = @('verify', '/pa', '/all', '/tw', '/v', $Path)
    & $signTool @verifyArguments
    if ($LASTEXITCODE -ne 0) {
        throw "SignTool signature verification failed with exit code $LASTEXITCODE."
    }

    Write-Host "PASS: Authenticode signature and RFC 3161 timestamp verified for $(Split-Path -Leaf $Path)." -ForegroundColor Green
}
finally {
    foreach ($thumbprint in $importedThumbprints) {
        if ($thumbprint -notin $existingThumbprints) {
            $certificateInStore = Join-Path $storePath $thumbprint
            if (Test-Path -LiteralPath $certificateInStore) {
                Remove-Item -LiteralPath $certificateInStore -Force
            }
        }
    }
    $securePassword.Dispose()
}
