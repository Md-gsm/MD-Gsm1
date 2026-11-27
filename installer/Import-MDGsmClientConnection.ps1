[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $ConnectionFile,
    [Parameter(Mandatory = $true)][string] $InstallDirectory
)

$ErrorActionPreference = 'Stop'
$rootStore = [System.Security.Cryptography.X509Certificates.X509Store]::new(
    [System.Security.Cryptography.X509Certificates.StoreName]::Root,
    [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
$addedThumbprint = $null
$connection = $null
$settingsPath = $null
$settingsOriginalContent = $null
$settingsUpdated = $false

try {
    if (-not (Test-Path -LiteralPath $ConnectionFile -PathType Leaf)) {
        throw 'The central server connection file was not found.'
    }

    $connection = Get-Content -LiteralPath $ConnectionFile -Raw | ConvertFrom-Json
    if ($connection.schemaVersion -ne 1) { throw 'The central server connection file has an unsupported version.' }

    $apiUri = $null
    if (-not [Uri]::TryCreate([string]$connection.apiBaseUrl, [UriKind]::Absolute, [ref]$apiUri) -or
        $apiUri.Scheme -ne 'https' -or $apiUri.Port -ne 5081 -or
        $apiUri.AbsolutePath -ne '/' -or
        -not [string]::IsNullOrEmpty($apiUri.UserInfo) -or
        -not [string]::IsNullOrEmpty($apiUri.Query) -or
        -not [string]::IsNullOrEmpty($apiUri.Fragment) -or
        $apiUri.Host -in @('localhost', '127.0.0.1', '::1')) {
        throw 'The connection file does not contain an approved central HTTPS API address.'
    }

    $thumbprint = ([string]$connection.rootCaThumbprint -replace '[^0-9A-Fa-f]', '').ToUpperInvariant()
    if ($thumbprint.Length -ne 40 -or
        $connection.rootCaSubject -ne 'CN=MD-Gsm Internal API Root CA' -or
        [string]::IsNullOrWhiteSpace([string]$connection.rootCaCertificateBase64)) {
        throw 'The connection file does not identify the approved MD-Gsm root certificate.'
    }

    try {
        $rawCertificate = [Convert]::FromBase64String([string]$connection.rootCaCertificateBase64)
        $rootCertificate = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new([byte[]]$rawCertificate)
    } catch {
        throw 'The root certificate in the connection file is not valid base64-encoded X.509 data.'
    }

    try {
        if ($rootCertificate.HasPrivateKey -or
            $rootCertificate.Subject -ne [string]$connection.rootCaSubject -or
            $rootCertificate.Thumbprint -ne $thumbprint -or
            (Get-Date) -lt $rootCertificate.NotBefore -or
            (Get-Date) -ge $rootCertificate.NotAfter) {
            throw 'The public root certificate identity, fingerprint or validity period is invalid.'
        }

        $basic = $rootCertificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.19' } | Select-Object -First 1
        if ($null -eq $basic -or
            -not ([System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]$basic).CertificateAuthority) {
            throw 'The supplied certificate is not a CA trust anchor.'
        }
        $keyUsage = $rootCertificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.15' } | Select-Object -First 1
        if ($null -eq $keyUsage -or
            (([System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]$keyUsage).KeyUsages -band
                [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyCertSign) -eq 0) {
            throw 'The supplied CA certificate is not permitted to sign certificates.'
        }

        $rootStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $existing = $rootStore.Certificates.Find(
            [System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint,
            $thumbprint, $false)
        if ($existing.Count -gt 1) { throw 'More than one root certificate has the supplied fingerprint.' }
        if ($existing.Count -eq 1) {
            if ([BitConverter]::ToString([byte[]]$existing[0].RawData) -cne [BitConverter]::ToString([byte[]]$rootCertificate.RawData)) {
                throw 'The existing trusted root certificate does not match the connection bundle.'
            }
        } else {
            $rootStore.Add($rootCertificate)
            $addedThumbprint = $thumbprint
        }

        # This request uses the operating-system trust store and normal hostname
        # validation. No callback suppresses certificate-chain or name errors.
        $healthUri = [Uri]::new($apiUri, 'health/live')
        $response = Invoke-WebRequest -UseBasicParsing -Uri $healthUri.AbsoluteUri -TimeoutSec 8
        if ($response.StatusCode -ne 200) { throw 'The central API did not return a successful HTTPS liveness response.' }
        $health = $response.Content | ConvertFrom-Json
        if ($health.product -ne 'MD-Gsm' -or $health.status -ne 'alive' -or
            $health.scope -ne 'process-liveness-only') {
            throw 'The HTTPS endpoint did not return the expected MD-Gsm API identity.'
        }

        $settingsPath = Join-Path $InstallDirectory 'appsettings.json'
        if (Test-Path -LiteralPath $settingsPath -PathType Leaf) {
            $settingsOriginalContent = [System.IO.File]::ReadAllText($settingsPath, [System.Text.Encoding]::UTF8)
            $settings = $settingsOriginalContent | ConvertFrom-Json
        } else {
            $settings = [pscustomobject]@{}
        }
        $apiSettings = [pscustomobject]@{ BaseUrl = $apiUri.AbsoluteUri; TimeoutSeconds = 8 }
        if ($null -ne $settings.PSObject.Properties['Api']) {
            $settings.Api = $apiSettings
        } else {
            $settings | Add-Member -MemberType NoteProperty -Name Api -Value $apiSettings
        }

        $temporarySettings = $settingsPath + '.tmp'
        $settingsJson = $settings | ConvertTo-Json -Depth 12
        [System.IO.File]::WriteAllText($temporarySettings, $settingsJson, [System.Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $temporarySettings -Destination $settingsPath -Force
        $settingsUpdated = $true

        $trustDirectory = Join-Path $env:ProgramData 'MD-Gsm\ClientTrust'
        New-Item -ItemType Directory -Path $trustDirectory -Force | Out-Null
        $trustMarkerPath = Join-Path $trustDirectory 'trusted-root.json'
        $priorMarker = if (Test-Path -LiteralPath $trustMarkerPath -PathType Leaf) {
            try { Get-Content -LiteralPath $trustMarkerPath -Raw | ConvertFrom-Json } catch { $null }
        } else { $null }
        $wasAddedByProduct = ($existing.Count -eq 0) -or
            ($null -ne $priorMarker -and $priorMarker.Thumbprint -eq $thumbprint -and [bool]$priorMarker.AddedByMdGsm)
        $trustMarker = [ordered]@{
            Thumbprint = $thumbprint
            Subject = [string]$connection.rootCaSubject
            AddedByMdGsm = $wasAddedByProduct
            InstalledAtUtc = [DateTime]::UtcNow.ToString('o')
        }
        $trustMarkerTemp = $trustMarkerPath + '.tmp'
        [System.IO.File]::WriteAllText(
            $trustMarkerTemp, ($trustMarker | ConvertTo-Json -Depth 4),
            [System.Text.UTF8Encoding]::new($false))
        Move-Item -LiteralPath $trustMarkerTemp -Destination $trustMarkerPath -Force

        Write-Output ('PASS: central HTTPS endpoint and hostname validated: ' + $apiUri.Authority)
        Write-Output 'PASS: public root CA trusted for the whole Windows machine; no private key or database credential was imported.'
    } finally {
        $rootCertificate.Dispose()
        [Array]::Clear($rawCertificate, 0, $rawCertificate.Length)
    }
} catch {
    if ($settingsUpdated -and -not [string]::IsNullOrWhiteSpace($settingsPath)) {
        try {
            if ($null -eq $settingsOriginalContent) {
                Remove-Item -LiteralPath $settingsPath -Force -ErrorAction SilentlyContinue
            } else {
                $restorePath = $settingsPath + '.rollback.tmp'
                [System.IO.File]::WriteAllText($restorePath, $settingsOriginalContent, [System.Text.UTF8Encoding]::new($false))
                Move-Item -LiteralPath $restorePath -Destination $settingsPath -Force
            }
        } catch { }
    }
    if ($null -ne $addedThumbprint) {
        # The store is still open from the Add operation. Re-open is deliberately
        # avoided here so a failed endpoint probe cannot strand a newly added CA.
        try {
            $added = $rootStore.Certificates.Find(
                [System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint,
                $addedThumbprint, $false)
            foreach ($certificate in $added) { $rootStore.Remove($certificate) }
        } catch { }
    }
    throw
} finally {
    $rootStore.Close()
}
