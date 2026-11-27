# Explicitly initialize the Windows Certificate provider for installer and renewal
# processes started with -NoProfile. Loading PKI avoids implicit module autoloading.
function Initialize-MDGsmCertificateProvider {
    Import-Module -Name Microsoft.PowerShell.Security -ErrorAction Stop
    Import-Module -Name PKI -ErrorAction Stop

    $certificateProvider = @(Get-PSProvider | Where-Object { $_.Name -eq 'Certificate' })
    if ($certificateProvider.Count -ne 1) {
        throw 'The Windows Certificate PowerShell provider is unavailable or ambiguous.'
    }

    $certificateDrive = Get-PSDrive -Name Cert -ErrorAction SilentlyContinue
    if ($null -eq $certificateDrive) {
        New-PSDrive -Name Cert -PSProvider Certificate -Root '\' -Scope Global -ErrorAction Stop | Out-Null
        $certificateDrive = Get-PSDrive -Name Cert -ErrorAction Stop
    }
    if ($certificateDrive.Provider.Name -ne 'Certificate') {
        throw 'The Cert: PowerShell drive is present but is not backed by the Windows Certificate provider.'
    }

    foreach ($storePath in @('Cert:\LocalMachine\My', 'Cert:\LocalMachine\Root')) {
        if (-not (Test-Path -LiteralPath $storePath -ErrorAction Stop)) {
            throw ("The required Windows machine certificate store is unavailable: " + $storePath)
        }
    }
}
