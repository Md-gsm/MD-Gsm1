# Establish the Windows Certificate provider without re-importing its module
# when the host already registered it. Windows PowerShell 5.1 can reject a second
# import because its extended type data contains members already registered.
function Initialize-MDGsmCertificateProvider {
    $certificateProvider = @(Get-PSProvider | Where-Object { $_.Name -eq 'Certificate' })
    if ($certificateProvider.Count -eq 0) {
        $securityModule = Get-Module -Name Microsoft.PowerShell.Security
        if ($null -eq $securityModule) {
            Import-Module -Name Microsoft.PowerShell.Security -ErrorAction Stop
        }
        $certificateProvider = @(Get-PSProvider | Where-Object { $_.Name -eq 'Certificate' })
    }
    if ($certificateProvider.Count -ne 1) {
        throw 'The Windows Certificate PowerShell provider is unavailable or ambiguous after checking the loaded security module.'
    }

    $certificateDrive = Get-PSDrive -Name Cert -ErrorAction SilentlyContinue
    if ($null -eq $certificateDrive) {
        New-PSDrive -Name Cert -PSProvider Certificate -Root '\' -Scope Global -ErrorAction Stop | Out-Null
        $certificateDrive = Get-PSDrive -Name Cert -ErrorAction Stop
    }
    if ($certificateDrive.Provider.Name -ne 'Certificate') {
        throw 'The Cert: PowerShell drive is present but is not backed by the Windows Certificate provider.'
    }

    $selfSignedCommand = Get-Command -Name New-SelfSignedCertificate -ErrorAction SilentlyContinue
    if ($null -eq $selfSignedCommand) {
        $pkiModule = Get-Module -Name PKI
        if ($null -eq $pkiModule) {
            Import-Module -Name PKI -ErrorAction Stop
        }
        $selfSignedCommand = Get-Command -Name New-SelfSignedCertificate -ErrorAction SilentlyContinue
    }
    if ($null -eq $selfSignedCommand) {
        throw 'The PKI module did not expose New-SelfSignedCertificate.'
    }

    foreach ($storePath in @('Cert:\LocalMachine\My', 'Cert:\LocalMachine\Root')) {
        if (-not (Test-Path -LiteralPath $storePath -ErrorAction Stop)) {
            throw ("The required Windows machine certificate store is unavailable: " + $storePath)
        }
    }
}
