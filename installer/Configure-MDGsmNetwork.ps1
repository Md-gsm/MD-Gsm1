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
        if (Test-Path -LiteralPath $networkRoot -PathType Container) {
            Add-Content -LiteralPath (Join-Path $networkRoot 'network-setup.log') -Value $line -Encoding UTF8
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
            $mask = (0xff -shl (8 - $remainingBits)) -band 0xff
            $networkBytes[$index] = [byte]($addressBytes[$index] -band $mask)
        } else {
            $networkBytes[$index] = 0
        }
    }
    return ('{0}/{1}' -f ([System.Net.IPAddress]::new($networkBytes).ToString()), $PrefixLength)
}

function Test-IsPrivateIpv4([string] $Address) {
    $parsed = [System.Net.IPAddress]::Parse($Address)
    if ($parsed.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork) { return $false }
    $bytes = $parsed.GetAddressBytes()
    return (
        $bytes[0] -eq 10 -or
        ($bytes[0] -eq 172 -and $bytes[1] -ge 16 -and $bytes[1] -le 31) -or
        ($bytes[0] -eq 192 -and $bytes[1] -eq 168)
    )
}

function Get-PrimaryPrivateNetwork {
    $routes = @(Get-NetRoute -AddressFamily IPv4 -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_.NextHop) -and $_.NextHop -ne '0.0.0.0' })
    $candidates = [System.Collections.Generic.List[object]]::new()
    foreach ($route in $routes) {
        $profile = Get-NetConnectionProfile -InterfaceIndex $route.InterfaceIndex -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -eq $profile -or $profile.NetworkCategory -notin @('Private', 'DomainAuthenticated')) { continue }
        $interface = Get-NetIPInterface -InterfaceIndex $route.InterfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -eq $interface) { continue }
        $addresses = @(Get-NetIPAddress -InterfaceIndex $route.InterfaceIndex -AddressFamily IPv4 -AddressState Preferred -ErrorAction SilentlyContinue |
            Where-Object {
                $_.PrefixLength -ge 1 -and $_.PrefixLength -le 30 -and
                (Test-IsPrivateIpv4 -Address ([string]$_.IPAddress)) -and -not $_.SkipAsSource
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
            Mode = 'Loopback'; HostName = '127.0.0.1'; BindAddress = '127.0.0.1'
            Cidr = $null; InterfaceAlias = $null; Profile = $null; CertificateNameType = 'IPAddress'
            Reason = 'No active Domain or Private IPv4 network with a default route was detected. Remote access remains disabled.'
        }
    }

    $ordered = @($candidates | Sort-Object -Property Score, InterfaceIndex, Address)
    $bestScore = [int]$ordered[0].Score
    $best = @($ordered | Where-Object { $_.Score -eq $bestScore } | Sort-Object -Property InterfaceIndex, Address)
    $bestInterfaces = @($best | Select-Object -ExpandProperty InterfaceIndex -Unique)
    if ($bestInterfaces.Count -ne 1) {
        return [pscustomobject]@{
            Mode = 'Loopback'; HostName = '127.0.0.1'; BindAddress = '127.0.0.1'
            Cidr = $null; InterfaceAlias = $null; Profile = $null; CertificateNameType = 'IPAddress'
            Reason = 'Several private network interfaces have the same route priority. Remote access remains disabled rather than selecting an ambiguous network.'
        }
    }

    $selected = $best[0]
    $hostName = [string]$selected.Address
    $certificateNameType = 'IPAddress'
    $computer = Get-CimInstance Win32_ComputerSystem
    if ($computer.PartOfDomain -and -not [string]::IsNullOrWhiteSpace([string]$computer.Domain) -and
        -not [string]::IsNullOrWhiteSpace([string]$computer.DNSHostName)) {
        $candidateDnsName = ('{0}.{1}' -f $computer.DNSHostName, $computer.Domain).TrimEnd('.').ToLowerInvariant()
        if ($candidateDnsName.Length -le 253 -and $candidateDnsName -match '^[a-z0-9](?:[a-z0-9.-]*[a-z0-9])?$') {
            try {
                $resolvedAddresses = @([System.Net.Dns]::GetHostAddresses($candidateDnsName) | ForEach-Object { $_.ToString() })
                if ($resolvedAddresses.Count -eq 1 -and $resolvedAddresses[0] -eq [string]$selected.Address) {
                    $hostName = $candidateDnsName
                    $certificateNameType = 'DNS'
                } else {
                    Write-NetworkLog ('Domain DNS name did not resolve to the selected interface address. Using IP endpoint ' + $selected.Address + '.')
                }
            } catch {
                Write-NetworkLog ('Domain DNS resolution failed. Using IP endpoint ' + $selected.Address + '.')
            }
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

function Get-OrCreateRootAuthority([bool] $HadPriorNetworkConfiguration) {
    $subject = 'CN=MD-Gsm Internal API Root CA'
    $namedRootCertificates = @(Get-ChildItem -Path Cert:\LocalMachine\My |
        Where-Object { $_.Subject -eq $subject -and (Test-IsCertificateAuthority $_) })
    $existingRoots = @($namedRootCertificates | Where-Object { $_.HasPrivateKey })
    $now = Get-Date
    $validRoots = @($existingRoots | Where-Object { $_.NotBefore -le $now -and $_.NotAfter -gt $now } | Sort-Object -Property NotAfter -Descending)
    if ($namedRootCertificates.Count -gt 0 -and $validRoots.Count -eq 0) {
        throw 'The previously provisioned MD-Gsm root CA is missing its private key or is no longer valid. Automatic root replacement is blocked.'
    }
    if ($validRoots.Count -eq 0 -and $HadPriorNetworkConfiguration) {
        throw 'An existing API network configuration references a root CA that is no longer present. Restore the original root certificate or perform an approved explicit client re-pair/rotation; refusing silent trust replacement.'
    }
    if ($validRoots.Count -gt 1) {
        throw 'More than one valid MD-Gsm root CA exists in LocalMachine\My. Explicit certificate rotation/reconciliation is required.'
    }
    if ($validRoots.Count -eq 0 -and $existingRoots.Count -gt 0) {
        throw 'The previously provisioned MD-Gsm root CA is expired or not yet valid. Automatic root replacement is blocked; use the approved certificate rotation and client re-pair procedure.'
    }

    if ($validRoots.Count -eq 0) {
        $root = New-SelfSignedCertificate -Type Custom -Subject $subject -CertStoreLocation Cert:\LocalMachine\My -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 -KeyUsage CertSign, CRLSign -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(10) -TextExtension @('2.5.29.19={critical}{text}ca=1&pathlength=0')
        Write-NetworkLog 'Created the initial non-exportable machine-local API root CA. Its private key is never included in the client connection bundle.'
    } else {
        $root = $validRoots[0]
    }
    if (-not (Test-IsCertificateAuthority $root)) { throw 'The stored MD-Gsm root certificate is not a CA certificate.' }
    if ($root.NotAfter -le (Get-Date).AddDays(180)) {
        throw 'The MD-Gsm root CA expires within 180 days. Automatic replacement is blocked because every client must explicitly trust the replacement root.'
    }

    $rootStore = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        [System.Security.Cryptography.X509Certificates.StoreName]::Root,
        [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
    try {
        $rootStore.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $trusted = $rootStore.Certificates.Find([System.Security.Cryptography.X509Certificates.X509FindType]::FindByThumbprint, $root.Thumbprint, $false)
        if ($trusted.Count -gt 1) { throw 'The MD-Gsm root CA thumbprint is duplicated in LocalMachine Root.' }
        if ($trusted.Count -eq 0) {
            $publicRoot = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new([byte[]]$root.RawData)
            try { $rootStore.Add($publicRoot) } finally { $publicRoot.Dispose() }
            Write-NetworkLog 'Installed only the public root certificate into LocalMachine Root; the non-exportable signing key remains in LocalMachine My.'
        }
    } finally {
        $rootStore.Close()
        $rootStore.Dispose()
    }
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
        $serviceSid = [System.Security.Principal.NTAccount]::new('NT SERVICE', $serviceName).Translate([System.Security.Principal.SecurityIdentifier])
        $acl = Get-Acl -LiteralPath $keyFile
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new($serviceSid, [System.Security.AccessControl.FileSystemRights]::Read, [System.Security.AccessControl.AccessControlType]::Allow)
        $acl.SetAccessRule($rule)
        Set-Acl -LiteralPath $keyFile -AclObject $acl
    } finally {
        $rsa.Dispose()
    }
}

function Test-ServerAuthenticationUsage($Certificate) {
    $extension = $Certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.37' } | Select-Object -First 1
    if ($null -eq $extension) { return $false }
    $parsed = [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]$extension
    return (@($parsed.EnhancedKeyUsages | Where-Object { $_.Value -eq '1.3.6.1.5.5.7.3.1' }).Count -gt 0)
}

function Get-ExpectedSubjectAlternativeNameBytes([string] $HostName, [string] $NameType) {
    if ($NameType -eq 'IPAddress') {
        $address = [System.Net.IPAddress]::Parse($HostName)
        if ($address.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork) {
            throw 'Only IPv4 subject alternative names are supported by this installer.'
        }
        $bytes = $address.GetAddressBytes()
        $header = [byte[]]@(0x30, 0x06, 0x87, 0x04)
        return ($header + $bytes)
    }

    $dnsBytes = [System.Text.Encoding]::ASCII.GetBytes($HostName)
    if ($dnsBytes.Length -lt 1 -or $dnsBytes.Length -gt 253) {
        throw 'The TLS server DNS name is outside the supported length.'
    }
    $contentLength = $dnsBytes.Length + 2
    if ($contentLength -lt 128) {
        $header = [byte[]]@(0x30, [byte]$contentLength, 0x82, [byte]$dnsBytes.Length)
    } else {
        $header = [byte[]]@(0x30, 0x81, [byte]$contentLength, 0x82, [byte]$dnsBytes.Length)
    }
    return ($header + $dnsBytes)
}

function Test-ServerCertificateName([System.Security.Cryptography.X509Certificates.X509Certificate2] $Certificate, [string] $HostName, [string] $NameType) {
    $sanExtensions = @($Certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.17' })
    if ($sanExtensions.Count -ne 1) { return $false }
    $expected = Get-ExpectedSubjectAlternativeNameBytes -HostName $HostName -NameType $NameType
    return ([BitConverter]::ToString([byte[]]$sanExtensions[0].RawData) -ceq
        [BitConverter]::ToString([byte[]]$expected))
}

function Get-OrCreateServerCertificate([System.Security.Cryptography.X509Certificates.X509Certificate2] $Root, [string] $HostName, [ValidateSet('DNS', 'IPAddress')][string] $NameType) {
    $expectedSubject = 'CN={0}' -f $HostName
    $certificate = Get-ChildItem -Path Cert:\LocalMachine\My |
        Where-Object {
            $_.Subject -eq $expectedSubject -and $_.Issuer -eq $Root.Subject -and $_.HasPrivateKey -and
            $_.NotAfter -gt (Get-Date).AddDays(45) -and (Test-ServerAuthenticationUsage $_) -and
            (Test-ServerCertificateName -Certificate $_ -HostName $HostName -NameType $NameType)
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
    $serviceSid = [System.Security.Principal.NTAccount]::new('NT SERVICE', $serviceName).Translate([System.Security.Principal.SecurityIdentifier]).Value
    $icacls = Join-Path $env:windir 'System32\icacls.exe'
    & $icacls $networkRoot '/inheritance:r' '/grant:r' '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' ("*{0}:(OI)(CI)RX" -f $serviceSid) | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not restrict the API network configuration directory to SYSTEM, Administrators and the API service SID.'
    }
}

function Write-JsonAtomically([string] $Path, $Value) {
    $temporaryPath = $Path + '.tmp'
    [System.IO.File]::WriteAllText($temporaryPath, ($Value | ConvertTo-Json -Depth 12), [System.Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
}

function Set-ApiFirewallRule($Network) {
    Get-NetFirewallRule -Name $firewallRuleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule -ErrorAction SilentlyContinue
    if ($Network.Mode -ne 'Https') { return }
    $null = New-NetFirewallRule -Name $firewallRuleName -DisplayName 'MD-Gsm Central API HTTPS (private network only)' -Direction Inbound -Action Allow -Enabled True -Protocol TCP -LocalPort $httpsPort -LocalAddress $Network.BindAddress -RemoteAddress $Network.Cidr -InterfaceAlias $Network.InterfaceAlias -Profile $Network.Profile -Program $apiExe -EdgeTraversalPolicy Block
}

function Set-DesktopApiBaseUrl([string] $BaseUrl) {
    $desktopSettingsPath = Join-Path $InstallDirectory 'appsettings.json'
    if (-not (Test-Path -LiteralPath $desktopSettingsPath -PathType Leaf)) {
        throw 'The central Desktop appsettings.json file is missing; the central UI cannot be safely bound to the API endpoint.'
    }
    $settings = Get-Content -LiteralPath $desktopSettingsPath -Raw | ConvertFrom-Json
    $apiSettings = [pscustomobject]@{ BaseUrl = $BaseUrl; TimeoutSeconds = 8 }
    if ($null -ne $settings.PSObject.Properties['Api']) {
        $settings.Api = $apiSettings
    } else {
        $settings | Add-Member -MemberType NoteProperty -Name Api -Value $apiSettings
    }
    Write-JsonAtomically -Path $desktopSettingsPath -Value $settings
    Write-NetworkLog ('Central Desktop client configured to use HTTPS endpoint ' + $BaseUrl + '.')
}

function Set-PublicBundleAcl([string] $Path) {
    $icacls = Join-Path $env:windir 'System32\icacls.exe'
    & $icacls $Path '/inheritance:r' '/grant:r' '*S-1-5-18:F' '*S-1-5-32-544:F' '*S-1-5-32-545:R' | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not restrict the public client bundle to SYSTEM/Administrators write access and Users read access.'
    }
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

if (-not (Test-Path -LiteralPath $apiExe -PathType Leaf)) {
    throw 'The central API executable is missing; network configuration was not changed.'
}

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

$root = Get-OrCreateRootAuthority -HadPriorNetworkConfiguration ($null -ne $oldConfig -and -not [string]::IsNullOrWhiteSpace([string]$oldConfig.Network.CertificateThumbprint))
$serverCertificate = Get-OrCreateServerCertificate -Root $root -HostName $network.HostName -NameType $network.CertificateNameType
$serverName = $network.HostName
$apiBaseUrl = 'https://{0}:{1}/' -f $serverName, $httpsPort
$allowedHosts = if ($network.Mode -eq 'Https') { $serverName + ';localhost;127.0.0.1' } else { 'localhost;127.0.0.1' }

$config = [ordered]@{
    AllowedHosts = $allowedHosts
    Network = [ordered]@{
        Mode = 'Https'
        BindAddress = $network.BindAddress
        Port = $httpsPort
        ServerName = $serverName
        CertificateThumbprint = $serverCertificate.Thumbprint
        RootCaThumbprint = $root.Thumbprint
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
    $network = [pscustomobject]@{
        Mode = 'Loopback'; HostName = '127.0.0.1'; BindAddress = '127.0.0.1'
        Cidr = $null; InterfaceAlias = $null; Profile = $null; CertificateNameType = 'IPAddress'
    }
    $apiBaseUrl = 'https://127.0.0.1:5081/'
    $config = [ordered]@{
        AllowedHosts = 'localhost;127.0.0.1'
        Network = [ordered]@{
            Mode = 'Https'; BindAddress = '127.0.0.1'; Port = $httpsPort
            ServerName = '127.0.0.1'; CertificateThumbprint = $null
            RootCaThumbprint = $root.Thumbprint
            InterfaceAlias = $null; NetworkCidr = $null; WindowsProfile = $null
            ApiBaseUrl = $apiBaseUrl; RemoteAccessEnabled = $false
        }
    }
    Set-ApiFirewallRule -Network $network
    Write-NetworkLog ('Firewall configuration failed. Falling back to HTTPS loopback only. Reason: ' + $_.Exception.Message)
    $serverCertificate = Get-OrCreateServerCertificate -Root $root -HostName '127.0.0.1' -NameType 'IPAddress'
    $config.Network.CertificateThumbprint = $serverCertificate.Thumbprint
}

$configJson = $config | ConvertTo-Json -Depth 12
$oldJson = if ($null -eq $oldConfig) { '' } else { $oldConfig | ConvertTo-Json -Depth 12 }
$changed = $oldJson -cne $configJson
Write-JsonAtomically -Path $networkConfigPath -Value $config
Set-NetworkDirectoryAcl
Set-DesktopApiBaseUrl -BaseUrl $apiBaseUrl

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
    Set-PublicBundleAcl -Path $publicBundlePath
    Write-NetworkLog ('Client connection bundle written to ' + $publicBundlePath + '. It contains only the public root certificate and API URL; ordinary users have read-only access.')
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
        $deadline = (Get-Date).AddSeconds(40)
        do {
            Start-Sleep -Milliseconds 500
            $service.Refresh()
        } while ($service.Status -ne 'Running' -and (Get-Date) -lt $deadline)
        if ($service.Status -ne 'Running') {
            throw 'MDGsmApi did not return to Running after the TLS/network configuration changed.'
        }
        $healthUri = [Uri]::new([Uri]$apiBaseUrl, 'health/live').AbsoluteUri
        $healthy = $false
        $healthDeadline = (Get-Date).AddSeconds(30)
        while (-not $healthy -and (Get-Date) -lt $healthDeadline) {
            try {
                $response = Invoke-WebRequest -UseBasicParsing -Uri $healthUri -TimeoutSec 4
                $healthy = ($response.StatusCode -eq 200)
            } catch {
                Start-Sleep -Milliseconds 500
            }
        }
        if (-not $healthy) {
            throw 'MDGsmApi failed the HTTPS liveness check after its network/certificate update.'
        }
        Write-NetworkLog 'The updated HTTPS listener returned to Running and passed its liveness probe.'
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
