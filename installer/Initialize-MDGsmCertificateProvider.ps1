# Initialize the Certificate provider using modules from the current PowerShell host.
# A Windows PowerShell 5.1 process can inherit PowerShell 7 paths in PSModulePath.
# Resolving built-in modules by name then may load incompatible type data and fail.
function Get-MDGsmModuleManifestPath {
    param([Parameter(Mandatory = $true)][ValidateSet('Microsoft.PowerShell.Security', 'PKI')][string] $ModuleName)

    $moduleDirectory = Join-Path (Join-Path $PSHOME 'Modules') $ModuleName
    $manifestPath = Join-Path $moduleDirectory ($ModuleName + '.psd1')
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf -ErrorAction Stop)) {
        throw ("The current PowerShell host module manifest is missing: " + $manifestPath)
    }
    return [System.IO.Path]::GetFullPath($manifestPath)
}

function Assert-MDGsmModuleFromCurrentHost {
    param([Parameter(Mandatory = $true)][ValidateSet('Microsoft.PowerShell.Security', 'PKI')][string] $ModuleName)

    if ($PSVersionTable.PSEdition -ne 'Desktop') { return }

    $modules = @(Get-Module -Name $ModuleName)
    if ($modules.Count -ne 1) {
        throw ("Expected exactly one loaded Windows PowerShell module " + $ModuleName + "; found " + $modules.Count + ".")
    }

    $expectedManifestPath = Get-MDGsmModuleManifestPath -ModuleName $ModuleName
    $actualModulePath = [System.IO.Path]::GetFullPath([string]$modules[0].Path)
    if (-not [string]::Equals($actualModulePath, $expectedManifestPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw ("Refusing to use " + $ModuleName + " loaded from a path other than the current Windows PowerShell host manifest. Actual: " + $actualModulePath + "; expected: " + $expectedManifestPath)
    }
}

function Import-MDGsmCurrentHostModule {
    param([Parameter(Mandatory = $true)][ValidateSet('Microsoft.PowerShell.Security', 'PKI')][string] $ModuleName)

    if ($PSVersionTable.PSEdition -eq 'Desktop') {
        $manifestPath = Get-MDGsmModuleManifestPath -ModuleName $ModuleName
        Import-Module -Name $manifestPath -ErrorAction Stop
    } else {
        Import-Module -Name $ModuleName -ErrorAction Stop
    }
    Assert-MDGsmModuleFromCurrentHost -ModuleName $ModuleName
}

function Initialize-MDGsmCertificateProvider {
    $certificateProvider = @(Get-PSProvider | Where-Object { $_.Name -eq 'Certificate' })
    $securityModules = @(Get-Module -Name Microsoft.PowerShell.Security)
    if ($securityModules.Count -gt 1) {
        throw 'More than one Microsoft.PowerShell.Security module is loaded; refusing ambiguous certificate-provider state.'
    }

    if ($securityModules.Count -eq 1) {
        Assert-MDGsmModuleFromCurrentHost -ModuleName 'Microsoft.PowerShell.Security'
    } elseif ($certificateProvider.Count -eq 0) {
        Import-MDGsmCurrentHostModule -ModuleName 'Microsoft.PowerShell.Security'
    }

    $certificateProvider = @(Get-PSProvider | Where-Object { $_.Name -eq 'Certificate' })
    if ($certificateProvider.Count -ne 1) {
        throw 'The Windows Certificate PowerShell provider is unavailable or ambiguous after loading the current host security module.'
    }

    $certificateDrive = Get-PSDrive -Name Cert -ErrorAction SilentlyContinue
    if ($null -eq $certificateDrive) {
        New-PSDrive -Name Cert -PSProvider Certificate -Root '\' -Scope Global -ErrorAction Stop | Out-Null
        $certificateDrive = Get-PSDrive -Name Cert -ErrorAction Stop
    }
    if ($certificateDrive.Provider.Name -ne 'Certificate') {
        throw 'The Cert: PowerShell drive is present but is not backed by the Windows Certificate provider.'
    }

    $pkiModules = @(Get-Module -Name PKI)
    if ($pkiModules.Count -gt 1) {
        throw 'More than one PKI module is loaded; refusing ambiguous certificate-command state.'
    }
    if ($pkiModules.Count -eq 0) {
        Import-MDGsmCurrentHostModule -ModuleName 'PKI'
    } else {
        Assert-MDGsmModuleFromCurrentHost -ModuleName 'PKI'
    }

    $selfSignedCommand = Get-Command -Name New-SelfSignedCertificate -Module PKI -ErrorAction SilentlyContinue
    if ($null -eq $selfSignedCommand) {
        throw 'The current host PKI module did not expose New-SelfSignedCertificate.'
    }

    foreach ($storePath in @('Cert:\LocalMachine\My', 'Cert:\LocalMachine\Root')) {
        if (-not (Test-Path -LiteralPath $storePath -ErrorAction Stop)) {
            throw ("The required Windows machine certificate store is unavailable: " + $storePath)
        }
    }
}
