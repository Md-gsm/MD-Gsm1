[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$markerPath = Join-Path $env:ProgramData 'MD-Gsm\ClientTrust\trusted-root.json'
if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf)) {
    Write-Output 'No MD-Gsm client trust marker exists; no machine trust changes are required.'
    exit 0
}

$marker = Get-Content -LiteralPath $markerPath -Raw | ConvertFrom-Json
$thumbprint = ([string]$marker.Thumbprint -replace '[^0-9A-Fa-f]', '').ToUpperInvariant()
if ($thumbprint.Length -ne 40 -or $marker.Subject -ne 'CN=MD-Gsm Internal API Root CA') {
    throw 'The MD-Gsm client trust cleanup marker is invalid; refusing to remove any certificate.'
}

$apiService = Get-CimInstance Win32_Service -Filter "Name='MDGsmApi'" -ErrorAction SilentlyContinue
if ($null -eq $apiService -and [bool]$marker.AddedByMdGsm) {
    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        [System.Security.Cryptography.X509Certificates.StoreName]::Root,
        [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
    try {
        $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $matches = $store.Certificates.Find(
            [System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint,
            $thumbprint, $false)
        foreach ($certificate in $matches) {
            if ($certificate.Subject -eq [string]$marker.Subject -and -not $certificate.HasPrivateKey) {
                $store.Remove($certificate)
            }
        }
        Write-Output 'Removed only the MD-Gsm client-installed public CA trust anchor.'
    } finally {
        $store.Close()
        $store.Dispose()
    }
} else {
    Write-Output 'Preserved the root CA trust because the central API service still exists or the trust anchor predated this client.'
}

Remove-Item -LiteralPath $markerPath -Force
$markerDirectory = Split-Path -Parent $markerPath
if ((Test-Path -LiteralPath $markerDirectory -PathType Container) -and
    (@(Get-ChildItem -LiteralPath $markerDirectory -Force).Count -eq 0)) {
    Remove-Item -LiteralPath $markerDirectory -Force
}
