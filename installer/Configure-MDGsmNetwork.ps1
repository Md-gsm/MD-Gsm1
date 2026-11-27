[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $InstallDirectory,
    [switch] $RenewOnly
)

$ErrorActionPreference = 'Stop'
$serviceName = 'MDGsmApi'
$httpsPort = 5081
$firewallRuleName = 'MDGsmApiHttps5081'
$renewalTaskName = 'MD-Gsm API TLS Certificate Renewal'
$networkRoot = Join-Path $env:ProgramData 'MD-Gsm\Network'
$networkConfigPath = Join-Path $networkRoot 'api-network.json'
$bundlePath = Join-Path $networkRoot 'MD-Gsm-Client-Connection.mdgsm.json'
$publicBundlePath = Join-Path $env:PUBLIC 'Documents\MD-Gsm-Client-Connection.mdgsm.json'
$apiExe = Join-Path $InstallDirectory 'Api\MDGsm.Api.exe'
$powerShellExe = Join-Path $PSHOME 'powershell.exe'

function Write-NetworkLog([string] $Message) {
    $line = '{0:o} [MDGsmNetwork] {1}' -f (Get-Date), $Message
    Write-Output $line
    try {
        $logPath = Join-Path $networkRoot 'network-setup.log'
        if (Test-Path -LiteralPath $networkRoot -PathType Container) {
            Add-Content -LiteralPath $logPath -Value $line -Encoding UTF8
        }
    } catch { }
}

function Get-Ipv4Cidr([string] $Address, [int] $PrefixLength) {
    $ip = [System.Net.IPAddress]::Parse($Address)
    if ($ip.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork -or $PrefixLength -lt 1 -or $PrefixLength -gt 30) {
        throw 'Only usable IPv4 LAN prefixes from /1 through /30 are supported.'
    }
    $addressBytes = $ip.GetAddressBytes()
    $networkBytes = [byte[]]::new(4)
    $wholeBytes = [Math]::Floor($PrefixLength / 8)
    $remainingBits = $PrefixLength % 8
    for ($index = 0; $index -lt 4; $index++) {
        if ($index -lt $wholeBytes) {
            $networkBytes[$index] = $addressBytes[$index]
        } elseif ($index -eq $wholeBytes -and $remainingBits -gt 0) {
            $octetMask = (0xff -shl (8 - $remainingBits)) -band 0xff
            $networkBytes[$index] = [byte]($addressBytes[$index] -band $octetMask)
        } else {
            $networkBytes[$index] = 0
        }
    }
    $networkAddress = [System.Net.IPAddress]::new($networkBytes).ToString()
    return ('{0}/{1}' -f $networkAddress, $PrefixLength)
}

function Get-PrimaryPrivateNetwork {
    $routes = @(Get-NetRoute -AddressFamily IPv4 -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_.NextHop) -and $_.NextHop -ne '0.0.0.0' })
    $candidates = New-Object System.Collections.Generic.List[object]
    foreach ($route in $routes) {
        $profile = Get-NetConnectionProfile -InterfaceIndex $route.InterfaceIndex -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -eq $profile -or $profile.NetworkCategory -notin @('Private', 'DomainAuthenticated')) { continue }
        $interface = Get-NetIPInterface -InterfaceIndex $route.InterfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -eq $interface) { continue }
        $addresses = @(Get-NetIPAddress -InterfaceIndex $route.InterfaceIndex -AddressFamily IPv4 -AddressState Preferred -ErrorAction SilentlyContinue |
            Where-Object {
                $_.PrefixLength -ge 1 -and $_.PrefixLength -le 30 -and
                $_.IPAddress -notmatch '^(127\.|169\.254\.|0\.)' -and -not $_.SkipAsSource
            } | Sort-Object -Property SkipAsSource, IPAddress)
        if ($addresses.Count -eq 0) { continue }
        $address = $addresses[0]
        $candidates.Add([pscustomobject]@{
            InterfaceIndex = [int]$route.InterfaceIndex
            InterfaceAlias = [string]$interface.InterfaceAlias
            InterfaceMetric = [int]$interface.InterfaceMetric
            RouteMetric = [int]$route.RouteMetric
            Score = [int]$interface.InterfaceMetric + [int]$route.RouteMetric
            NetworkCategory = [string]$profile.NetworkCategory
            Address = [string]$address.IPAddress
            PrefixLength = [int]$address.PrefixLength
            Cidr = Get-Ipv4Cidr -Address ([string]$address.IPAddress) -PrefixLength ([int]$address.PrefixLength)
        })
    }

    if ($candidates.Count -eq 0) {
        return [pscustomobject]@{
            Mode = 'Loopback'; HostName = 'localhost'; BindAddress = '127.0.0.1'
            Cidr = $null; InterfaceAlias = $null; Profile = $null
            Reason = 'No active Domain or Private IPv4 network with a default route was detected. Remote access remains disabled.'
        }
    }

    $ordered = @($candidates | Sort-Object -Property Score, InterfaceIndex, Address)
    $bestScore = [int]$ordered[0].Score
    $best = @($ordered | Where-Object { $_.Score -eq $bestScore } | Sort-Object -Property InterfaceIndex, Address)
    $bestInterfaces = @($best | Select-Object -ExpandProperty InterfaceIndex -Unique)
    if ($bestInterfaces.Count -gt 1) {
        return [pscustomobject]@{
            Mode = 'Loopback'; HostName = 'localhost'; BindAddress = '127.0.0.1'
            Cidr = $null; InterfaceAlias = $null; Profile = $null
            Reason = 'Several private network interfaces have the same route priority. Remote access remains disabled rather than selecting an ambiguous network.'
        }
    }

    $selected = $best[0]
    $computer = Get-CimInstance Win32_ComputerSystem
    $computer = Get-CimInstance Win32_ComputerSystem
    $hostName = [string]$selected.Address
    $certificateNameType = 'IPAddress'
    if ($computer.PartOfDomain -and -not [string]::IsNullOrWhiteSpace($computer.Domain) -and
        -not [string]::IsNullOrWhiteSpace($computer.DNSHostName)) {
        $hostName = ('{0}.{1}' -f $computer.DNSHostName, $computer.Domain).TrimEnd('.').ToLowerInvariant()
        $certificateNameType = 'DNS'
    }

    if ($certificateNameType -eq 'DNS' -and
        ($hostName.Length -gt 253 -or $hostName -notmatch '^[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?
}

function Test-IsCertificateAuthority([System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate) {
    $extension = $Certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.19' } | Select-Object -First 1
    if ($null -eq $extension) { return $false }
    return ([System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]$extension).CertificateAuthority
}

function Get-OrCreateRootAuthority {
    $subject = 'CN=MD-Gsm Internal API Root CA'
    $root = Get-ChildItem -Path Cert:\LocalMachine\My |
        Where-Object {
            $_.Subject -eq $subject -and $_.HasPrivateKey -and
            $_.NotAfter -gt (Get-Date).AddDays(180) -and (Test-IsCertificateAuthority $_)
        } | Sort-Object -Property NotAfter -Descending | Select-Object -First 1

    if ($null -eq $root) {
        $root = New-SelfSignedCertificate -Type Custom -Subject $subject -CertStoreLocation Cert:\LocalMachine\My -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 -KeyUsage CertSign, CRLSign -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(10) -TextExtension @('2.5.29.19={critical}{text}ca=1&pathlength=0')
        Write-NetworkLog 'Created a new non-exportable machine-local API root CA. Its private key is never included in the client connection bundle.'
    }

    if (-not (Test-IsCertificateAuthority $root)) { throw 'The stored MD-Gsm root certificate is not a CA certificate.' }
    if ($root.NotAfter -le (Get-Date).AddDays(180)) {
        throw 'The internal root CA expires within 180 days. A planned client re-pairing is required; automatic root replacement is blocked.'
    }

    $rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store(
        [System.Security.Cryptography.X509Certificates.StoreName]::Root,
        [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
    try {
        $rootStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $trusted = $rootStore.Certificates.Find([System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint, $root.Thumbprint, $false)
        if ($trusted.Count -eq 0) {
            $rootStore.Add($root)
            Write-NetworkLog 'Installed the public root certificate into LocalMachine Root for local server-side chain validation.'
        }
    } finally { $rootStore.Close() }
    return $root
}

function Add-ServiceReadAccessToPrivateKey([System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate) {
    $rsa = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($Certificate)
    if ($null -eq $rsa) { throw 'The API TLS certificate has no RSA private key.' }
    try {
        if ($rsa -is [System.Security.Cryptography.RSACng]) {
            $keyFile = Join-Path $env:ProgramData ('Microsoft\Crypto\Keys\' + $rsa.Key.UniqueName)
        } elseif ($rsa -is [System.Security.Cryptography.RSACryptoServiceProvider]) {
            $keyFile = Join-Path $env:ProgramData ('Microsoft\Crypto\RSA\MachineKeys\' + $rsa.CspKeyContainerInfo.UniqueKeyContainerName)
        } else {
            throw ('Unsupported machine key provider: ' + $rsa.GetType().FullName)
        }
        if (-not (Test-Path -LiteralPath $keyFile -PathType Leaf)) { throw 'The API TLS private-key file could not be resolved in the machine key store.' }
        $serviceSid = (New-Object System.Security.Principal.NTAccount('NT SERVICE', $serviceName)).Translate([System.Security.Principal.SecurityIdentifier])
        $acl = Get-Acl -LiteralPath $keyFile
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new($serviceSid, [System.Security.AccessControl.FileSystemRights]::Read, [System.Security.AccessControl.AccessControlType]::Allow)
        $acl.SetAccessRule($rule)
        Set-Acl -LiteralPath $keyFile -AclObject $acl
    } finally { $rsa.Dispose() }
}

function Test-ServerAuthenticationUsage($Certificate) {
    $eku = $Certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.37' } | Select-Object -First 1
    if ($null -eq $eku) { return $false }
    $parsed = [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]$eku
    return (@($parsed.EnhancedKeyUsages | Where-Object { $_.Value -eq '1.3.6.1.5.5.7.3.1' }).Count -gt 0)
}

function Get-OrCreateServerCertificate(
    [System.Security.Cryptography.X509Certificates.X509Certificate2] $Root,
    [string] $HostName,
    [ValidateSet('DNS', 'IPAddress')][string] $NameType) {
    $expectedSubject = 'CN={0}' -f $HostName
    $certificate = Get-ChildItem -Path Cert:\LocalMachine\My |
        Where-Object {
            $_.Subject -eq $expectedSubject -and $_.Issuer -eq $Root.Subject -and $_.HasPrivateKey -and
            $_.NotAfter -gt (Get-Date).AddDays(45) -and (Test-ServerAuthenticationUsage $_) -and
            ($_.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.17' }).Count -gt 0
        } | Sort-Object -Property NotAfter -Descending | Select-Object -First 1

    if ($null -eq $certificate) {
        $notAfter = (Get-Date).AddDays(397)
        if ($notAfter -ge $Root.NotAfter) { $notAfter = $Root.NotAfter.AddDays(-1) }
        if ($NameType -eq 'DNS') {
            $certificate = New-SelfSignedCertificate -Type SSLServerAuthentication -Subject $expectedSubject -DnsName $HostName -Signer $Root -CertStoreLocation Cert:\LocalMachine\My -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 -KeyExportPolicy NonExportable -NotAfter $notAfter
        } else {
            $sanExtension = '2.5.29.17={text}IPAddress=' + $HostName
            $certificate = New-SelfSignedCertificate -Type SSLServerAuthentication -Subject $expectedSubject -Signer $Root -CertStoreLocation Cert:\LocalMachine\My -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 -KeyExportPolicy NonExportable -NotAfter $notAfter -TextExtension @($sanExtension)
        }
        Write-NetworkLog ('Created or renewed the API server certificate for ' + $HostName + ' (' + $NameType + ').')
    }

    Add-ServiceReadAccessToPrivateKey -Certificate $certificate
    return $certificate
}

function Set-NetworkDirectoryAcl {
    $serviceSid = (New-Object System.Security.Principal.NTAccount('NT SERVICE', $serviceName)).Translate([System.Security.Principal.SecurityIdentifier]).Value
    $icacls = Join-Path $env:windir 'System32\icacls.exe'
    & $icacls $networkRoot '/inheritance:r' '/grant:r' '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' ("*{0}:(OI)(CI)RX" -f $serviceSid) | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not restrict the API network configuration directory to SYSTEM, Administrators and the API service SID.' }
}

function Write-JsonAtomically([string] $Path, $Value) {
    $temporaryPath = $Path + '.tmp'
    $json = $Value | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText($temporaryPath, $json, [System.Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
}

function Set-ApiFirewallRule($Network) {
    $oldRule = Get-NetFirewallRule -Name $firewallRuleName -ErrorAction SilentlyContinue
    if ($null -ne $oldRule) { $oldRule | Remove-NetFirewallRule }
    if ($Network.Mode -ne 'Https') { return }
    $null = New-NetFirewallRule -Name $firewallRuleName -DisplayName 'MD-Gsm Central API HTTPS (private network only)' -Direction Inbound -Action Allow -Enabled True -Protocol TCP -LocalPort $httpsPort -LocalAddress $Network.BindAddress -RemoteAddress $Network.Cidr -InterfaceAlias $Network.InterfaceAlias -Profile $Network.Profile -Program $apiExe -EdgeTraversalPolicy Block
}

function Register-CertificateRenewalTask {
    $scriptPath = Join-Path $InstallDirectory 'Configure-MDGsmNetwork.ps1'
    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) { throw 'The network certificate renewal script is missing from Program Files.' }
    $arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + $scriptPath + '" -InstallDirectory "' + $InstallDirectory + '" -RenewOnly'
    $action = New-ScheduledTaskAction -Execute $powerShellExe -Argument $arguments
    $trigger = New-ScheduledTaskTrigger -Daily -At 3:20am
    $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Minutes 10)
    Register-ScheduledTask -TaskName $renewalTaskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description 'Renews the MD-Gsm API TLS leaf certificate before expiry; keeps the same trusted root CA.' -Force | Out-Null
}

if (-not (Test-Path -LiteralPath $apiExe -PathType Leaf)) { throw 'The central API executable is missing; network configuration was not changed.' }
New-Item -ItemType Directory -Path $networkRoot -Force | Out-Null
Set-NetworkDirectoryAcl

$oldConfig = $null
if (Test-Path -LiteralPath $networkConfigPath -PathType Leaf) {
    try { $oldConfig = Get-Content -LiteralPath $networkConfigPath -Raw | ConvertFrom-Json }
    catch { throw 'The existing API network configuration is invalid; refusing to overwrite it.' }
}

$network = Get-PrimaryPrivateNetwork
if ($network.Mode -eq 'Loopback') { Write-NetworkLog $network.Reason }
else { Write-NetworkLog ('Detected network ' + $network.Cidr + ' on ' + $network.InterfaceAlias + ' using Windows profile ' + $network.Profile + '.') }

$root = Get-OrCreateRootAuthority
$serverCertificate = Get-OrCreateServerCertificate -Root $root -HostName $network.HostName -NameType $(if ($network.Mode -eq 'Https') { $network.CertificateNameType } else { 'DNS' })
$serverName = $network.HostName
$apiBaseUrl = 'https://{0}:{1}/' -f $serverName, $httpsPort
$allowedHosts = if ($network.Mode -eq 'Https') { $serverName + ';localhost' } else { 'localhost;127.0.0.1' }

$config = [ordered]@{
    AllowedHosts = $allowedHosts
    Network = [ordered]@{
        Mode = 'Https'
        BindAddress = $network.BindAddress
        Port = $httpsPort
        ServerName = $serverName
        CertificateThumbprint = $serverCertificate.Thumbprint
        InterfaceAlias = $network.InterfaceAlias
        NetworkCidr = $network.Cidr
        WindowsProfile = $network.Profile
        ApiBaseUrl = $apiBaseUrl
        RemoteAccessEnabled = ($network.Mode -eq 'Https')
    }
}

try {
    Set-ApiFirewallRule -Network $network
} catch {
    $network = [pscustomobject]@{ Mode = 'Loopback'; HostName = 'localhost'; BindAddress = '127.0.0.1'; Cidr = $null; InterfaceAlias = $null; Profile = $null }
    $apiBaseUrl = 'https://localhost:5081/'
    $config = [ordered]@{
        AllowedHosts = 'localhost;127.0.0.1'
        Network = [ordered]@{
            Mode = 'Https'; BindAddress = '127.0.0.1'; Port = $httpsPort
            ServerName = 'localhost'; CertificateThumbprint = ''
            InterfaceAlias = $null; NetworkCidr = $null; WindowsProfile = $null
            ApiBaseUrl = $apiBaseUrl; RemoteAccessEnabled = $false
        }
    }
    Set-ApiFirewallRule -Network $network
    Write-NetworkLog ('Firewall configuration failed. Falling back to HTTPS loopback only. Reason: ' + $_.Exception.Message)
    $serverCertificate = Get-OrCreateServerCertificate -Root $root -HostName 'localhost' -NameType 'DNS'
    $config.Network.CertificateThumbprint = $serverCertificate.Thumbprint
}

$configJson = $config | ConvertTo-Json -Depth 8
$oldJson = if ($null -eq $oldConfig) { '' } else { $oldConfig | ConvertTo-Json -Depth 8 }
$changed = $oldJson -cne $configJson
Write-JsonAtomically -Path $networkConfigPath -Value $config
Set-NetworkDirectoryAcl

if ($config.Network.RemoteAccessEnabled) {
    $bundle = [ordered]@{
        schemaVersion = 1
        apiBaseUrl = $apiBaseUrl
        rootCaThumbprint = $root.Thumbprint
        rootCaSubject = $root.Subject
        rootCaNotAfterUtc = $root.NotAfter.ToUniversalTime().ToString('o')
        rootCaCertificateBase64 = [Convert]::ToBase64String($root.RawData)
        transferInstruction = 'Transfer this public connection file to each client using trusted physical media or an already authenticated channel. Do not trust a replacement file received from an unknown source.'
    }
    Write-JsonAtomically -Path $bundlePath -Value $bundle
    $publicDirectory = Split-Path -Parent $publicBundlePath
    New-Item -ItemType Directory -Path $publicDirectory -Force | Out-Null
    Copy-Item -LiteralPath $bundlePath -Destination $publicBundlePath -Force
    Write-NetworkLog ('Client connection bundle written to ' + $publicBundlePath + '. It contains only the public root certificate and API URL.')
} else {
    Remove-Item -LiteralPath $bundlePath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $publicBundlePath -Force -ErrorAction SilentlyContinue
    Write-NetworkLog 'No private/domain network is available. The API remains HTTPS loopback-only and no remote client bundle is exported.'
}

if (-not $RenewOnly) {
    Register-CertificateRenewalTask
} elseif ($changed) {
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($null -ne $service -and $service.Status -eq 'Running') {
        Restart-Service -Name $serviceName -Force
        Write-NetworkLog 'Network endpoint or TLS certificate changed; restarted MDGsmApi to activate the renewed listener.'
    }
}

if ($config.Network.RemoteAccessEnabled) {
    Write-NetworkLog ('Remote HTTPS is configured at ' + $apiBaseUrl + ' for network ' + $config.Network.NetworkCidr + '; PostgreSQL remains local and is not opened in Windows Firewall.')
    Write-Output ('MDGSM_REMOTE_API_URL=' + $apiBaseUrl)
    Write-Output ('MDGSM_CLIENT_CONNECTION_BUNDLE=' + $publicBundlePath)
} else {
    Write-NetworkLog 'Remote access is not enabled. HTTPS is bound only to 127.0.0.1:5081.'
    Write-Output 'MDGSM_REMOTE_API_URL=disabled'
}
)) {
        return [pscustomobject]@{
            Mode = 'Loopback'; HostName = 'localhost'; BindAddress = '127.0.0.1'
            Cidr = $null; InterfaceAlias = $null; Profile = $null; CertificateNameType = 'DNS'
            Reason = 'Windows did not provide a usable domain DNS host name. Remote access remains disabled.'
        }
    }

    return [pscustomobject]@{
        Mode = 'Https'; HostName = $hostName; BindAddress = $selected.Address
        Cidr = $selected.Cidr; InterfaceAlias = $selected.InterfaceAlias
        CertificateNameType = $certificateNameType
        Profile = if ($selected.NetworkCategory -eq 'DomainAuthenticated') { 'Domain' } else { 'Private' }
        Reason = 'Selected the active private network with the lowest combined route and interface metric.'
    }
}

function Test-IsCertificateAuthority([System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate) {
    $extension = $Certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.19' } | Select-Object -First 1
    if ($null -eq $extension) { return $false }
    return ([System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]$extension).CertificateAuthority
}

function Get-OrCreateRootAuthority {
    $subject = 'CN=MD-Gsm Internal API Root CA'
    $root = Get-ChildItem -Path Cert:\LocalMachine\My |
        Where-Object {
            $_.Subject -eq $subject -and $_.HasPrivateKey -and
            $_.NotAfter -gt (Get-Date).AddDays(180) -and (Test-IsCertificateAuthority $_)
        } | Sort-Object -Property NotAfter -Descending | Select-Object -First 1

    if ($null -eq $root) {
        $root = New-SelfSignedCertificate -Type Custom -Subject $subject -CertStoreLocation Cert:\LocalMachine\My -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 -KeyUsage CertSign, CRLSign -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(10) -TextExtension @('2.5.29.19={critical}{text}ca=1&pathlength=0')
        Write-NetworkLog 'Created a new non-exportable machine-local API root CA. Its private key is never included in the client connection bundle.'
    }

    if (-not (Test-IsCertificateAuthority $root)) { throw 'The stored MD-Gsm root certificate is not a CA certificate.' }
    if ($root.NotAfter -le (Get-Date).AddDays(180)) {
        throw 'The internal root CA expires within 180 days. A planned client re-pairing is required; automatic root replacement is blocked.'
    }

    $rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store(
        [System.Security.Cryptography.X509Certificates.StoreName]::Root,
        [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
    try {
        $rootStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $trusted = $rootStore.Certificates.Find([System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint, $root.Thumbprint, $false)
        if ($trusted.Count -eq 0) {
            $rootStore.Add($root)
            Write-NetworkLog 'Installed the public root certificate into LocalMachine Root for local server-side chain validation.'
        }
    } finally { $rootStore.Close() }
    return $root
}

function Add-ServiceReadAccessToPrivateKey([System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate) {
    $rsa = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($Certificate)
    if ($null -eq $rsa) { throw 'The API TLS certificate has no RSA private key.' }
    try {
        if ($rsa -is [System.Security.Cryptography.RSACng]) {
            $keyFile = Join-Path $env:ProgramData ('Microsoft\Crypto\Keys\' + $rsa.Key.UniqueName)
        } elseif ($rsa -is [System.Security.Cryptography.RSACryptoServiceProvider]) {
            $keyFile = Join-Path $env:ProgramData ('Microsoft\Crypto\RSA\MachineKeys\' + $rsa.CspKeyContainerInfo.UniqueKeyContainerName)
        } else {
            throw ('Unsupported machine key provider: ' + $rsa.GetType().FullName)
        }
        if (-not (Test-Path -LiteralPath $keyFile -PathType Leaf)) { throw 'The API TLS private-key file could not be resolved in the machine key store.' }
        $serviceSid = (New-Object System.Security.Principal.NTAccount('NT SERVICE', $serviceName)).Translate([System.Security.Principal.SecurityIdentifier])
        $acl = Get-Acl -LiteralPath $keyFile
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new($serviceSid, [System.Security.AccessControl.FileSystemRights]::Read, [System.Security.AccessControl.AccessControlType]::Allow)
        $acl.SetAccessRule($rule)
        Set-Acl -LiteralPath $keyFile -AclObject $acl
    } finally { $rsa.Dispose() }
}

function Test-ServerAuthenticationUsage($Certificate) {
    $eku = $Certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.37' } | Select-Object -First 1
    if ($null -eq $eku) { return $false }
    $parsed = [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]$eku
    return (@($parsed.EnhancedKeyUsages | Where-Object { $_.Value -eq '1.3.6.1.5.5.7.3.1' }).Count -gt 0)
}

function Get-OrCreateServerCertificate([System.Security.Cryptography.X509Certificates.X509Certificate2] $Root, [string] $HostName) {
    $expectedSubject = 'CN={0}' -f $HostName
    $certificate = Get-ChildItem -Path Cert:\LocalMachine\My |
        Where-Object {
            $_.Subject -eq $expectedSubject -and $_.Issuer -eq $Root.Subject -and $_.HasPrivateKey -and
            $_.NotAfter -gt (Get-Date).AddDays(45) -and (Test-ServerAuthenticationUsage $_)
        } | Sort-Object -Property NotAfter -Descending | Select-Object -First 1

    if ($null -eq $certificate) {
        $notAfter = (Get-Date).AddDays(397)
        if ($notAfter -ge $Root.NotAfter) { $notAfter = $Root.NotAfter.AddDays(-1) }
        $certificate = New-SelfSignedCertificate -Type SSLServerAuthentication -Subject $expectedSubject -DnsName $HostName -Signer $Root -CertStoreLocation Cert:\LocalMachine\My -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 -KeyExportPolicy NonExportable -NotAfter $notAfter
        Write-NetworkLog ('Created or renewed the API server certificate for ' + $HostName + '.')
    }

    Add-ServiceReadAccessToPrivateKey -Certificate $certificate
    return $certificate
}

function Set-NetworkDirectoryAcl {
    $serviceSid = (New-Object System.Security.Principal.NTAccount('NT SERVICE', $serviceName)).Translate([System.Security.Principal.SecurityIdentifier]).Value
    $icacls = Join-Path $env:windir 'System32\icacls.exe'
    & $icacls $networkRoot '/inheritance:r' '/grant:r' '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' ("*{0}:(OI)(CI)RX" -f $serviceSid) | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not restrict the API network configuration directory to SYSTEM, Administrators and the API service SID.' }
}

function Write-JsonAtomically([string] $Path, $Value) {
    $temporaryPath = $Path + '.tmp'
    $json = $Value | ConvertTo-Json -Depth 8
    [System.IO.File]::WriteAllText($temporaryPath, $json, [System.Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
}

function Set-ApiFirewallRule($Network) {
    $oldRule = Get-NetFirewallRule -Name $firewallRuleName -ErrorAction SilentlyContinue
    if ($null -ne $oldRule) { $oldRule | Remove-NetFirewallRule }
    if ($Network.Mode -ne 'Https') { return }
    $null = New-NetFirewallRule -Name $firewallRuleName -DisplayName 'MD-Gsm Central API HTTPS (private network only)' -Direction Inbound -Action Allow -Enabled True -Protocol TCP -LocalPort $httpsPort -LocalAddress $Network.BindAddress -RemoteAddress $Network.Cidr -InterfaceAlias $Network.InterfaceAlias -Profile $Network.Profile -Program $apiExe -EdgeTraversalPolicy Block
}

function Register-CertificateRenewalTask {
    $scriptPath = Join-Path $InstallDirectory 'Configure-MDGsmNetwork.ps1'
    if (-not (Test-Path -LiteralPath $scriptPath -PathType Leaf)) { throw 'The network certificate renewal script is missing from Program Files.' }
    $arguments = '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + $scriptPath + '" -InstallDirectory "' + $InstallDirectory + '" -RenewOnly'
    $action = New-ScheduledTaskAction -Execute $powerShellExe -Argument $arguments
    $trigger = New-ScheduledTaskTrigger -Daily -At 3:20am
    $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Minutes 10)
    Register-ScheduledTask -TaskName $renewalTaskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description 'Renews the MD-Gsm API TLS leaf certificate before expiry; keeps the same trusted root CA.' -Force | Out-Null
}

if (-not (Test-Path -LiteralPath $apiExe -PathType Leaf)) { throw 'The central API executable is missing; network configuration was not changed.' }
New-Item -ItemType Directory -Path $networkRoot -Force | Out-Null
Set-NetworkDirectoryAcl

$oldConfig = $null
if (Test-Path -LiteralPath $networkConfigPath -PathType Leaf) {
    try { $oldConfig = Get-Content -LiteralPath $networkConfigPath -Raw | ConvertFrom-Json }
    catch { throw 'The existing API network configuration is invalid; refusing to overwrite it.' }
}

$network = Get-PrimaryPrivateNetwork
if ($network.Mode -eq 'Loopback') { Write-NetworkLog $network.Reason }
else { Write-NetworkLog ('Detected network ' + $network.Cidr + ' on ' + $network.InterfaceAlias + ' using Windows profile ' + $network.Profile + '.') }

$root = Get-OrCreateRootAuthority
$serverCertificate = Get-OrCreateServerCertificate -Root $root -HostName $network.HostName
$serverName = $network.HostName
$apiBaseUrl = 'https://{0}:{1}/' -f $serverName, $httpsPort
$allowedHosts = if ($network.Mode -eq 'Https') { $serverName + ';localhost' } else { 'localhost;127.0.0.1' }

$config = [ordered]@{
    AllowedHosts = $allowedHosts
    Network = [ordered]@{
        Mode = 'Https'
        BindAddress = $network.BindAddress
        Port = $httpsPort
        ServerName = $serverName
        CertificateThumbprint = $serverCertificate.Thumbprint
        InterfaceAlias = $network.InterfaceAlias
        NetworkCidr = $network.Cidr
        WindowsProfile = $network.Profile
        ApiBaseUrl = $apiBaseUrl
        RemoteAccessEnabled = ($network.Mode -eq 'Https')
    }
}

try {
    Set-ApiFirewallRule -Network $network
} catch {
    $network = [pscustomobject]@{ Mode = 'Loopback'; HostName = 'localhost'; BindAddress = '127.0.0.1'; Cidr = $null; InterfaceAlias = $null; Profile = $null }
    $apiBaseUrl = 'https://localhost:5081/'
    $config = [ordered]@{
        AllowedHosts = 'localhost;127.0.0.1'
        Network = [ordered]@{
            Mode = 'Https'; BindAddress = '127.0.0.1'; Port = $httpsPort
            ServerName = 'localhost'; CertificateThumbprint = ''
            InterfaceAlias = $null; NetworkCidr = $null; WindowsProfile = $null
            ApiBaseUrl = $apiBaseUrl; RemoteAccessEnabled = $false
        }
    }
    Set-ApiFirewallRule -Network $network
    Write-NetworkLog ('Firewall configuration failed. Falling back to HTTPS loopback only. Reason: ' + $_.Exception.Message)
    $serverCertificate = Get-OrCreateServerCertificate -Root $root -HostName 'localhost'
    $config.Network.CertificateThumbprint = $serverCertificate.Thumbprint
}

$configJson = $config | ConvertTo-Json -Depth 8
$oldJson = if ($null -eq $oldConfig) { '' } else { $oldConfig | ConvertTo-Json -Depth 8 }
$changed = $oldJson -cne $configJson
Write-JsonAtomically -Path $networkConfigPath -Value $config
Set-NetworkDirectoryAcl

if ($config.Network.RemoteAccessEnabled) {
    $bundle = [ordered]@{
        schemaVersion = 1
        apiBaseUrl = $apiBaseUrl
        rootCaThumbprint = $root.Thumbprint
        rootCaSubject = $root.Subject
        rootCaNotAfterUtc = $root.NotAfter.ToUniversalTime().ToString('o')
        rootCaCertificateBase64 = [Convert]::ToBase64String($root.RawData)
        transferInstruction = 'Transfer this public connection file to each client using trusted physical media or an already authenticated channel. Do not trust a replacement file received from an unknown source.'
    }
    Write-JsonAtomically -Path $bundlePath -Value $bundle
    $publicDirectory = Split-Path -Parent $publicBundlePath
    New-Item -ItemType Directory -Path $publicDirectory -Force | Out-Null
    Copy-Item -LiteralPath $bundlePath -Destination $publicBundlePath -Force
    Write-NetworkLog ('Client connection bundle written to ' + $publicBundlePath + '. It contains only the public root certificate and API URL.')
} else {
    Remove-Item -LiteralPath $bundlePath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $publicBundlePath -Force -ErrorAction SilentlyContinue
    Write-NetworkLog 'No private/domain network is available. The API remains HTTPS loopback-only and no remote client bundle is exported.'
}

if (-not $RenewOnly) {
    Register-CertificateRenewalTask
} elseif ($changed) {
    $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
    if ($null -ne $service -and $service.Status -eq 'Running') {
        Restart-Service -Name $serviceName -Force
        Write-NetworkLog 'Network endpoint or TLS certificate changed; restarted MDGsmApi to activate the renewed listener.'
    }
}

if ($config.Network.RemoteAccessEnabled) {
    Write-NetworkLog ('Remote HTTPS is configured at ' + $apiBaseUrl + ' for network ' + $config.Network.NetworkCidr + '; PostgreSQL remains local and is not opened in Windows Firewall.')
    Write-Output ('MDGSM_REMOTE_API_URL=' + $apiBaseUrl)
    Write-Output ('MDGSM_CLIENT_CONNECTION_BUNDLE=' + $publicBundlePath)
} else {
    Write-NetworkLog 'Remote access is not enabled. HTTPS is bound only to 127.0.0.1:5081.'
    Write-Output 'MDGSM_REMOTE_API_URL=disabled'
}
