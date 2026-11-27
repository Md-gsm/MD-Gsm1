[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $InstallDirectory,
    [string] $ProtectedConnectionFile = (Join-Path $env:ProgramData 'MD-Gsm\Secrets\Api\database-connection.dpapi')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security -ErrorAction Stop
$pgServiceName = 'MDGsmPostgreSQL18'
$pgInstallRoot = Join-Path $env:ProgramFiles 'MD-Gsm-PostgreSQL\18'
$pgDataRoot = Join-Path $env:ProgramData 'MD-Gsm\PostgreSQL\18'
$pgDataDirectory = Join-Path $pgDataRoot 'data'
$provisioningRoot = Join-Path $env:ProgramData 'MD-Gsm\Provisioning'
$stateDirectory = Join-Path $provisioningRoot 'PostgreSQL'
$statePath = Join-Path $stateDirectory 'cluster-state.json'
$adminSecretPath = Join-Path $stateDirectory 'cluster-admin.dpapi'
$installerUrl = 'https://get.enterprisedb.com/postgresql/postgresql-18.6-5-windows-x64.exe'
$expectedInstallerSha256 = '227e2bc08091b4c2b8338c7d61b11d057f895711480b006f90bfcfcd2410a58a'
$databaseName = 'mdgsm'
$runtimeLogin = 'mdgsm_api_runtime'
$runtimeGroup = 'mdgsm_runtime'
$apiServiceSid = 'NT SERVICE\MDGsmApi'
$psqlPath = Join-Path $pgInstallRoot 'bin\psql.exe'
$pgIsReadyPath = Join-Path $pgInstallRoot 'bin\pg_isready.exe'

function Get-CurrentAdminSid {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Central database provisioning requires an elevated local administrator.'
    }
    return $identity.User
}

function Get-AccountSid([string] $Account) {
    if ($Account -match '^NT SERVICE\\([^\\]+)

function Set-RestrictedDirectoryAcl([string] $Path, [bool] $AllowApiService) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
    $adminSid = Get-CurrentAdminSid
    $systemSid = [Security.Principal.SecurityIdentifier]::new('S-1-5-18')
    $acl = [System.Security.AccessControl.DirectorySecurity]::new()
    $acl.SetAccessRuleProtection($true, $false)
    $acl.SetOwner($adminSid)
    foreach ($sid in @($adminSid, $systemSid)) {
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
            $sid, [System.Security.AccessControl.FileSystemRights]::FullControl,
            [System.Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [System.Security.AccessControl.InheritanceFlags]::ObjectInherit,
            [System.Security.AccessControl.PropagationFlags]::None,
            [System.Security.AccessControl.AccessControlType]::Allow)
        $acl.AddAccessRule($rule)
    }
    if ($AllowApiService) {
        $serviceSid = Get-AccountSid $apiServiceSid
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
            $serviceSid, [System.Security.AccessControl.FileSystemRights]::ReadAndExecute,
            [System.Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [System.Security.AccessControl.InheritanceFlags]::ObjectInherit,
            [System.Security.AccessControl.PropagationFlags]::None,
            [System.Security.AccessControl.AccessControlType]::Allow)
        $acl.AddAccessRule($rule)
    }
    [System.IO.Directory]::SetAccessControl($Path, $acl)
}

function Set-RestrictedFileAcl([string] $Path, [bool] $AllowApiService) {
    $adminSid = Get-CurrentAdminSid
    $systemSid = [Security.Principal.SecurityIdentifier]::new('S-1-5-18')
    $acl = [System.Security.AccessControl.FileSecurity]::new()
    $acl.SetAccessRuleProtection($true, $false)
    $acl.SetOwner($adminSid)
    foreach ($sid in @($adminSid, $systemSid)) {
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
            $sid, [System.Security.AccessControl.FileSystemRights]::FullControl,
            [System.Security.AccessControl.AccessControlType]::Allow)
        $acl.AddAccessRule($rule)
    }
    if ($AllowApiService) {
        $serviceSid = Get-AccountSid $apiServiceSid
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
            $serviceSid, [System.Security.AccessControl.FileSystemRights]::Read,
            [System.Security.AccessControl.AccessControlType]::Allow)
        $acl.AddAccessRule($rule)
    }
    [System.IO.File]::SetAccessControl($Path, $acl)
}

function Protect-Text([string] $Text, [string] $Path, [bool] $AllowApiService) {
    $parent = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($Path))
    Set-RestrictedDirectoryAcl $parent $AllowApiService
    if (Test-Path -LiteralPath $Path) {
        throw "Protected provisioning file already exists; refusing to overwrite it: $([System.IO.Path]::GetFileName($Path))"
    }
    $plain = [System.Text.Encoding]::UTF8.GetBytes($Text)
    $cipher = [System.Security.Cryptography.ProtectedData]::Protect(
        $plain, $null, [System.Security.Cryptography.DataProtectionScope]::LocalMachine)
    try {
        $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try { $stream.Write($cipher, 0, $cipher.Length) } finally { $stream.Dispose() }
        Set-RestrictedFileAcl $Path $AllowApiService
    }
    finally {
        [System.Array]::Clear($plain, 0, $plain.Length)
        [System.Array]::Clear($cipher, 0, $cipher.Length)
    }
}

function Unprotect-Text([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required protected provisioning file is missing: $([System.IO.Path]::GetFileName($Path))"
    }
    $cipher = [System.IO.File]::ReadAllBytes($Path)
    $plain = $null
    try {
        $plain = [System.Security.Cryptography.ProtectedData]::Unprotect(
            $cipher, $null, [System.Security.Cryptography.DataProtectionScope]::LocalMachine)
        return [System.Text.Encoding]::UTF8.GetString($plain)
    }
    finally {
        [System.Array]::Clear($cipher, 0, $cipher.Length)
        if ($null -ne $plain) { [System.Array]::Clear($plain, 0, $plain.Length) }
    }
}

function Set-PostgresDataRootAcl {
    $serviceRecord = Get-CimInstance -ClassName Win32_Service -Filter "Name='$pgServiceName'" -ErrorAction SilentlyContinue
    $accountName = if ($null -ne $serviceRecord) { [string]$serviceRecord.StartName } else { '.\MDGsmPgSvc' }
    if ([string]::IsNullOrWhiteSpace($accountName) -or
        $accountName -match '^(LocalSystem|NT AUTHORITY\\|NT SERVICE\\)') {
        throw 'The managed PostgreSQL service is using an unexpected built-in or service-SID account.'
    }
    $accountSid = Get-AccountSid $accountName

    if (-not (Test-Path -LiteralPath $pgDataRoot -PathType Container)) {
        New-Item -ItemType Directory -Path $pgDataRoot -Force | Out-Null
    }
    $adminSid = Get-CurrentAdminSid
    $systemSid = [Security.Principal.SecurityIdentifier]::new('S-1-5-18')
    $acl = [System.Security.AccessControl.DirectorySecurity]::new()
    $acl.SetAccessRuleProtection($true, $false)
    $acl.SetOwner($adminSid)
    foreach ($sid in @($adminSid, $systemSid)) {
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
            $sid, [System.Security.AccessControl.FileSystemRights]::FullControl,
            [System.Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [System.Security.AccessControl.InheritanceFlags]::ObjectInherit,
            [System.Security.AccessControl.PropagationFlags]::None,
            [System.Security.AccessControl.AccessControlType]::Allow)
        $acl.AddAccessRule($rule)
    }
    $serviceRule = [System.Security.AccessControl.FileSystemAccessRule]::new(
        $accountSid, [System.Security.AccessControl.FileSystemRights]::Modify,
        [System.Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [System.Security.AccessControl.InheritanceFlags]::ObjectInherit,
        [System.Security.AccessControl.PropagationFlags]::None,
        [System.Security.AccessControl.AccessControlType]::Allow)
    $acl.AddAccessRule($serviceRule)
    [System.IO.Directory]::SetAccessControl($pgDataRoot, $acl)
}

function Ensure-PostgresServiceAccount {
    $accountName = 'MDGsmPgSvc'
    $localUser = Get-LocalUser -Name $accountName -ErrorAction SilentlyContinue
    if ($null -ne $localUser -and $state.Status -ne 'provisioning') {
        throw 'The PostgreSQL service account exists outside an incomplete product provisioning operation; Setup will not take it over.'
    }
    $securePassword = [System.Security.SecureString]::new()
    foreach ($character in $script:clusterSecrets.ServicePassword.ToCharArray()) {
        $securePassword.AppendChar($character)
    }
    $securePassword.MakeReadOnly()
    if ($null -eq $localUser) {
        New-LocalUser -Name $accountName -Password $securePassword -AccountNeverExpires -PasswordNeverExpires -UserMayNotChangePassword -Description 'MD-Gsm managed PostgreSQL 18 service account' | Out-Null
    }
    else {
        Set-LocalUser -Name $accountName -Password $securePassword
    }
    $localUser = Get-LocalUser -Name $accountName -ErrorAction Stop
    if ([string]$localUser.Description -ne 'MD-Gsm managed PostgreSQL 18 service account') {
        throw 'The local PostgreSQL service account description does not match the managed product account.'
    }
}

function Write-State($State) {
    Set-RestrictedDirectoryAcl $stateDirectory $false
    $tempPath = $statePath + '.tmp'
    $json = $State | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText($tempPath, $json, [System.Text.UTF8Encoding]::new($false))
    Set-RestrictedFileAcl $tempPath $false
    if (Test-Path -LiteralPath $statePath) {
        [System.IO.File]::Replace($tempPath, $statePath, $null, $true)
    } else {
        [System.IO.File]::Move($tempPath, $statePath)
    }
}

function New-RandomPassword {
    $bytes = [byte[]]::new(48)
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) }
    finally { $rng.Dispose() }
    try {
        return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
    }
    finally { [System.Array]::Clear($bytes, 0, $bytes.Length) }
}

function Get-FreeLocalPort {
    for ($candidate = 5432; $candidate -le 5500; $candidate++) {
        $listener = Get-NetTCPConnection -LocalPort $candidate -State Listen -ErrorAction SilentlyContinue
        if (-not $listener) { return $candidate }
    }
    throw 'No free local PostgreSQL port was found in the approved 5432-5500 range.'
}

function Write-RestrictedTextFile([string] $Path, [string] $Text) {
    $parent = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($Path))
    Set-RestrictedDirectoryAcl $parent $false
    [System.IO.File]::WriteAllText($Path, $Text, [System.Text.Encoding]::ASCII)
    Set-RestrictedFileAcl $Path $false
}

function Invoke-Psql([string] $Database, [string] $User, [string] $Password, [string] $Sql) {
    $sqlPath = Join-Path $provisioningRoot ('sql-' + [Guid]::NewGuid().ToString('N') + '.txt')
    $oldPassword = $env:PGPASSWORD
    try {
        Set-RestrictedDirectoryAcl $provisioningRoot $false
        Write-RestrictedTextFile $sqlPath $Sql
        $env:PGPASSWORD = $Password
        $output = & $psqlPath -X -w -h 127.0.0.1 -p ([string]$state.Port) -U $User -d $Database -v ON_ERROR_STOP=1 -t -A -f $sqlPath 2>&1
        $exitCode = $LASTEXITCODE
        if ($exitCode -ne 0) {
            throw "A PostgreSQL provisioning operation failed (psql exit $exitCode). Sensitive SQL output was suppressed."
        }
        return (($output | ForEach-Object { [string]$_ }) -join [Environment]::NewLine).Trim()
    }
    finally {
        if ($null -eq $oldPassword) { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }
        else { $env:PGPASSWORD = $oldPassword }
        Remove-Item -LiteralPath $sqlPath -Force -ErrorAction SilentlyContinue
    }
}

function Wait-PostgresReady {
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        & $pgIsReadyPath -h 127.0.0.1 -p ([string]$state.Port) -U postgres -d postgres | Out-Null
        if ($LASTEXITCODE -eq 0) { return }
        Start-Sleep -Seconds 2
    }
    throw 'The managed PostgreSQL 18 Windows service did not become ready within 120 seconds.'
}

function Get-RoleExists([string] $Role, [string] $AdminPassword) {
    $result = Invoke-Psql 'postgres' 'postgres' $AdminPassword "SELECT count(*) FROM pg_roles WHERE rolname = '$Role';"
    return ($result -eq '1')
}

function Get-DatabaseExists([string] $AdminPassword) {
    $result = Invoke-Psql 'postgres' 'postgres' $AdminPassword "SELECT count(*) FROM pg_database WHERE datname = '$databaseName';"
    return ($result -eq '1')
}

function Test-ManagedCluster([string] $AdminPassword) {
    $version = Invoke-Psql 'postgres' 'postgres' $AdminPassword 'SHOW server_version;'
    if ($version -notmatch '^18\.') { throw 'The managed PostgreSQL service is not major version 18.' }
    $listen = Invoke-Psql 'postgres' 'postgres' $AdminPassword 'SHOW listen_addresses;'
    if ($listen -ne '127.0.0.1') {
        Invoke-Psql 'postgres' 'postgres' $AdminPassword "ALTER SYSTEM SET listen_addresses = '127.0.0.1';"
        Restart-Service -Name $pgServiceName -Force
        Wait-PostgresReady
    }
}

function Test-ApiConnectionString([string] $ConnectionString) {
    $pattern = '^Host=127\.0\.0\.1;Port=(\d{4,5});Database=mdgsm;Username=mdgsm_api_runtime;Password=([A-Za-z0-9_-]{40,80});Timeout=15;Command Timeout=30;Pooling=true$'
    if ($ConnectionString -notmatch $pattern) {
        throw 'The protected API connection string does not match the fixed local runtime configuration.'
    }
    if ([int]$Matches[1] -ne [int]$state.Port) {
        throw 'The protected API connection string port does not match the managed cluster.'
    }
    return $Matches[2]
}

function Invoke-InstallPostgreSql {
    $downloadRoot = Join-Path $provisioningRoot 'download'
    Set-RestrictedDirectoryAcl $downloadRoot $false
    $installerPath = Join-Path $downloadRoot 'postgresql-18.6-5-windows-x64.exe'
    Write-Host 'Downloading the pinned PostgreSQL 18.6-5 installer and verifying its SHA-256...'
    Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
    $actualHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $expectedInstallerSha256) {
        Remove-Item -LiteralPath $installerPath -Force -ErrorAction SilentlyContinue
        throw 'The PostgreSQL installer checksum did not match the approved SHA-256.'
    }

    $optionFile = Join-Path $provisioningRoot 'postgresql-installer-options.txt'
    $options = @(
        'mode=unattended',
        'unattendedmodeui=none',
        'superaccount=postgres',
        "superpassword=$($script:clusterSecrets.SuperuserPassword)",
        'serviceaccount=MDGsmPgSvc',
        "servicepassword=$($script:clusterSecrets.ServicePassword)",
        "serverport=$($state.Port)",
        "prefix=$pgInstallRoot",
        "datadir=$pgDataDirectory",
        "servicename=$pgServiceName",
        'enable-components=server,commandlinetools',
        'enable_acledit=1'
    )
    Write-RestrictedTextFile $optionFile ($options -join [Environment]::NewLine)
    try {
        $process = Start-Process -FilePath $installerPath -ArgumentList @('--mode', 'unattended', '--unattendedmodeui', 'none', '--optionfile', $optionFile) -Wait -PassThru -NoNewWindow
        if ($process.ExitCode -ne 0) {
            throw "The verified PostgreSQL installer failed with exit code $($process.ExitCode)."
        }
    }
    finally {
        Remove-Item -LiteralPath $optionFile -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $installerPath -Force -ErrorAction SilentlyContinue
    }
}

try {
    $null = Get-CurrentAdminSid
    Set-RestrictedDirectoryAcl $provisioningRoot $false
    Set-RestrictedDirectoryAcl $stateDirectory $false
    if (-not (Test-Path -LiteralPath $pgDataRoot -PathType Container)) {
        New-Item -ItemType Directory -Path $pgDataRoot -Force | Out-Null
    }
    $service = Get-Service -Name $pgServiceName -ErrorAction SilentlyContinue

    if (Test-Path -LiteralPath $statePath) {
        $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
        if ($state.SchemaVersion -ne 1 -or $state.ServiceName -ne $pgServiceName -or $state.DatabaseName -ne $databaseName -or $state.RuntimeLogin -ne $runtimeLogin) {
            throw 'The existing PostgreSQL installation state is invalid or does not match this product.'
        }
        if ($null -eq $service) {
            if (Test-Path -LiteralPath $pgDataDirectory -PathType Container) {
                throw 'The managed PostgreSQL service is missing while its data directory exists. Setup will preserve the cluster and will not reinitialize it.'
            }
            if ($state.Status -eq 'ready') {
                throw 'The PostgreSQL service is missing from a completed installation; Repair will not recreate it automatically.'
            }
        }
        elseif (-not (Test-Path -LiteralPath $pgDataDirectory -PathType Container)) {
            throw 'The managed PostgreSQL data directory is missing; refusing to create a new empty cluster during Repair.'
        }
        $script:clusterSecrets = (Unprotect-Text $adminSecretPath) | ConvertFrom-Json
        if ([string]::IsNullOrWhiteSpace($script:clusterSecrets.SuperuserPassword) -or [string]::IsNullOrWhiteSpace($script:clusterSecrets.ServicePassword)) {
            throw 'The protected PostgreSQL maintenance secret bundle is invalid.'
        }
    }
    else {
        if ($null -ne $service -or (Test-Path -LiteralPath $pgDataDirectory)) {
            throw 'An unregistered PostgreSQL 18 service or data directory already exists. Setup will not adopt, overwrite, or reinitialize it.'
        }
        $state = [ordered]@{
            SchemaVersion = 1
            Status = 'provisioning'
            ServiceName = $pgServiceName
            InstallRoot = $pgInstallRoot
            DataDirectory = $pgDataDirectory
            Port = Get-FreeLocalPort
            DatabaseName = $databaseName
            RuntimeLogin = $runtimeLogin
            PostgreSqlMajor = 18
            CreatedAtUtc = [DateTime]::UtcNow.ToString('o')
        }
        $script:clusterSecrets = [ordered]@{
            SuperuserPassword = New-RandomPassword
            ServicePassword = New-RandomPassword
        }
        Protect-Text ($script:clusterSecrets | ConvertTo-Json -Compress) $adminSecretPath $false
        Write-State $state
    }

    if ($null -eq $service) {
        Ensure-PostgresServiceAccount
        Set-PostgresDataRootAcl
        Invoke-InstallPostgreSql
        $service = Get-Service -Name $pgServiceName -ErrorAction Stop
    }
    Set-PostgresDataRootAcl
    if ($service.Status -ne 'Running') { Start-Service -Name $pgServiceName }
    Wait-PostgresReady

    if (-not (Test-Path -LiteralPath $psqlPath -PathType Leaf)) {
        throw 'The PostgreSQL 18 psql client is missing from the managed installation.'
    }
    Test-ManagedCluster $script:clusterSecrets.SuperuserPassword

    $dbExists = Get-DatabaseExists $script:clusterSecrets.SuperuserPassword
    if (-not $dbExists) {
        if ($state.Status -eq 'ready') {
            throw 'The product database is missing from a completed installation; Repair will not recreate it silently.'
        }
        Invoke-Psql 'postgres' 'postgres' $script:clusterSecrets.SuperuserPassword 'CREATE DATABASE mdgsm OWNER postgres;'
    }

    $connectionString = $null
    $apiPassword = $null
    if (Test-Path -LiteralPath $ProtectedConnectionFile -PathType Leaf) {
        $connectionString = Unprotect-Text $ProtectedConnectionFile
        $apiPassword = Test-ApiConnectionString $connectionString
    }
    else {
        if (Get-RoleExists $runtimeLogin $script:clusterSecrets.SuperuserPassword) {
            throw 'The API LOGIN exists but its protected connection credential is missing; Setup will not rotate or reset it.'
        }
        $apiPassword = New-RandomPassword
        $connectionString = "Host=127.0.0.1;Port=$($state.Port);Database=mdgsm;Username=mdgsm_api_runtime;Password=$apiPassword;Timeout=15;Command Timeout=30;Pooling=true"
        Protect-Text $connectionString $ProtectedConnectionFile $true
    }

    $runtimeExists = Get-RoleExists $runtimeLogin $script:clusterSecrets.SuperuserPassword
    if (-not $runtimeExists) {
        if ($state.Status -eq 'ready') {
            throw 'The API LOGIN is missing from a completed installation; Repair will not silently recreate it.'
        }
        if (-not (Get-RoleExists $runtimeGroup $script:clusterSecrets.SuperuserPassword)) {
            Invoke-Psql 'postgres' 'postgres' $script:clusterSecrets.SuperuserPassword "CREATE ROLE mdgsm_runtime NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;" | Out-Null
        }
        $sql = "CREATE ROLE mdgsm_api_runtime LOGIN PASSWORD '$apiPassword' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS INHERIT;"
        Invoke-Psql 'postgres' 'postgres' $script:clusterSecrets.SuperuserPassword $sql | Out-Null
    }
    else {
        $roleAttributes = Invoke-Psql 'postgres' 'postgres' $script:clusterSecrets.SuperuserPassword "SELECT (rolcanlogin AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolreplication AND NOT rolbypassrls AND rolinherit)::text FROM pg_roles WHERE rolname = 'mdgsm_api_runtime';"
        if ($roleAttributes -ne 'true') { throw 'The existing API LOGIN has unexpected PostgreSQL role attributes.' }
        if (-not (Get-RoleExists $runtimeGroup $script:clusterSecrets.SuperuserPassword)) {
            throw 'The existing API LOGIN has no approved mdgsm_runtime group role; Repair will not broaden or recreate role policy.'
        }
    }

    $groupAttributes = Invoke-Psql 'postgres' 'postgres' $script:clusterSecrets.SuperuserPassword "SELECT (NOT rolcanlogin AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolreplication AND NOT rolbypassrls AND NOT rolinherit)::text FROM pg_roles WHERE rolname = 'mdgsm_runtime';"
    if ($groupAttributes -ne 'true') { throw 'The mdgsm_runtime group role has unexpected PostgreSQL role attributes.' }

    Invoke-Psql 'postgres' 'postgres' $script:clusterSecrets.SuperuserPassword "GRANT mdgsm_runtime TO mdgsm_api_runtime WITH INHERIT TRUE, SET FALSE, ADMIN FALSE;" | Out-Null
    Invoke-Psql 'postgres' 'postgres' $script:clusterSecrets.SuperuserPassword "GRANT CONNECT ON DATABASE mdgsm TO mdgsm_api_runtime;" | Out-Null
    $membership = Invoke-Psql 'postgres' 'postgres' $script:clusterSecrets.SuperuserPassword "SELECT (m.inherit_option AND NOT m.set_option AND NOT m.admin_option)::text FROM pg_auth_members m JOIN pg_roles parent ON parent.oid=m.roleid JOIN pg_roles member ON member.oid=m.member WHERE parent.rolname='mdgsm_runtime' AND member.rolname='mdgsm_api_runtime';"
    if ($membership -ne 'true') { throw 'The API LOGIN membership options do not match the least-privilege runtime policy.' }

    $apiIdentity = Invoke-Psql 'mdgsm' 'mdgsm_api_runtime' $apiPassword "SELECT session_user || '|' || current_user || '|' || current_database() || '|' || (current_setting('server_version_num')::integer / 10000)::text || '|' || inet_server_addr()::text;"
    if ($apiIdentity -ne 'mdgsm_api_runtime|mdgsm_api_runtime|mdgsm|18|127.0.0.1') {
        throw 'The generated API LOGIN failed the exact PostgreSQL identity/version/loopback proof.'
    }

    $state.Status = 'ready'
    $state.RuntimeIdentityVerifiedAtUtc = [DateTime]::UtcNow.ToString('o')
    Write-State $state
    Write-Host 'PASS: managed PostgreSQL 18 verified; mdgsm database and least-privilege API LOGIN provisioned; connection secret DPAPI-protected and ACL-restricted.'
    Write-Host 'No application schema migrations were executed.'
}
catch {
    $failure = $_
    $safeMessage = [string]$failure.Exception.Message
    $secretValues = @()
    if ($null -ne $script:clusterSecrets) {
        $secretValues += [string]$script:clusterSecrets.SuperuserPassword
        $secretValues += [string]$script:clusterSecrets.ServicePassword
    }
    if (-not [string]::IsNullOrWhiteSpace([string]$apiPassword)) {
        $secretValues += [string]$apiPassword
    }
    foreach ($secretValue in $secretValues) {
        if (-not [string]::IsNullOrWhiteSpace($secretValue)) {
            $safeMessage = $safeMessage.Replace($secretValue, '[REDACTED]')
        }
    }
    $safeMessage = $safeMessage -replace '(?i)(password|pwd)\s*=\s*[^;,\s]+', '$1=[REDACTED]'
    $safeMessage = $safeMessage -replace '(?i)PGPASSWORD\s*=\s*[^;,\s]+', 'PGPASSWORD=[REDACTED]'
    Write-Output ('PROVISIONING FAILURE: {0}: {1}' -f $failure.Exception.GetType().Name, $safeMessage)
    throw [System.InvalidOperationException]::new("Central PostgreSQL provisioning failed: $safeMessage")
}
finally {
    $script:clusterSecrets = $null
    $apiPassword = $null
    $connectionString = $null
    if ($null -ne $env:PGPASSWORD) { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }
}
) {
        $serviceName = $Matches[1]
        $scOutput = & sc.exe showsid $serviceName 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Service Control Manager could not provide the SID for service '$serviceName'." }
        $sidMatch = [regex]::Match(($scOutput -join [Environment]::NewLine), 'S-1-5-80-(?:[0-9]+-){4}[0-9]+')
        if (-not $sidMatch.Success) { throw "Service Control Manager returned no valid service SID for '$serviceName'." }
        return [Security.Principal.SecurityIdentifier]::new($sidMatch.Value)
    }
    return ([Security.Principal.NTAccount]::new($Account)).Translate([Security.Principal.SecurityIdentifier])
}

function Set-RestrictedDirectoryAcl([string] $Path, [bool] $AllowApiService) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
    $adminSid = Get-CurrentAdminSid
    $systemSid = [Security.Principal.SecurityIdentifier]::new('S-1-5-18')
    $acl = [System.Security.AccessControl.DirectorySecurity]::new()
    $acl.SetAccessRuleProtection($true, $false)
    $acl.SetOwner($adminSid)
    foreach ($sid in @($adminSid, $systemSid)) {
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
            $sid, [System.Security.AccessControl.FileSystemRights]::FullControl,
            [System.Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [System.Security.AccessControl.InheritanceFlags]::ObjectInherit,
            [System.Security.AccessControl.PropagationFlags]::None,
            [System.Security.AccessControl.AccessControlType]::Allow)
        $acl.AddAccessRule($rule)
    }
    if ($AllowApiService) {
        $serviceSid = Get-AccountSid $apiServiceSid
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
            $serviceSid, [System.Security.AccessControl.FileSystemRights]::ReadAndExecute,
            [System.Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [System.Security.AccessControl.InheritanceFlags]::ObjectInherit,
            [System.Security.AccessControl.PropagationFlags]::None,
            [System.Security.AccessControl.AccessControlType]::Allow)
        $acl.AddAccessRule($rule)
    }
    [System.IO.Directory]::SetAccessControl($Path, $acl)
}

function Set-RestrictedFileAcl([string] $Path, [bool] $AllowApiService) {
    $adminSid = Get-CurrentAdminSid
    $systemSid = [Security.Principal.SecurityIdentifier]::new('S-1-5-18')
    $acl = [System.Security.AccessControl.FileSecurity]::new()
    $acl.SetAccessRuleProtection($true, $false)
    $acl.SetOwner($adminSid)
    foreach ($sid in @($adminSid, $systemSid)) {
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
            $sid, [System.Security.AccessControl.FileSystemRights]::FullControl,
            [System.Security.AccessControl.AccessControlType]::Allow)
        $acl.AddAccessRule($rule)
    }
    if ($AllowApiService) {
        $serviceSid = Get-AccountSid $apiServiceSid
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
            $serviceSid, [System.Security.AccessControl.FileSystemRights]::Read,
            [System.Security.AccessControl.AccessControlType]::Allow)
        $acl.AddAccessRule($rule)
    }
    [System.IO.File]::SetAccessControl($Path, $acl)
}

function Protect-Text([string] $Text, [string] $Path, [bool] $AllowApiService) {
    $parent = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($Path))
    Set-RestrictedDirectoryAcl $parent $AllowApiService
    if (Test-Path -LiteralPath $Path) {
        throw "Protected provisioning file already exists; refusing to overwrite it: $([System.IO.Path]::GetFileName($Path))"
    }
    $plain = [System.Text.Encoding]::UTF8.GetBytes($Text)
    $cipher = [System.Security.Cryptography.ProtectedData]::Protect(
        $plain, $null, [System.Security.Cryptography.DataProtectionScope]::LocalMachine)
    try {
        $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
        try { $stream.Write($cipher, 0, $cipher.Length) } finally { $stream.Dispose() }
        Set-RestrictedFileAcl $Path $AllowApiService
    }
    finally {
        [System.Array]::Clear($plain, 0, $plain.Length)
        [System.Array]::Clear($cipher, 0, $cipher.Length)
    }
}

function Unprotect-Text([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required protected provisioning file is missing: $([System.IO.Path]::GetFileName($Path))"
    }
    $cipher = [System.IO.File]::ReadAllBytes($Path)
    $plain = $null
    try {
        $plain = [System.Security.Cryptography.ProtectedData]::Unprotect(
            $cipher, $null, [System.Security.Cryptography.DataProtectionScope]::LocalMachine)
        return [System.Text.Encoding]::UTF8.GetString($plain)
    }
    finally {
        [System.Array]::Clear($cipher, 0, $cipher.Length)
        if ($null -ne $plain) { [System.Array]::Clear($plain, 0, $plain.Length) }
    }
}

function Set-PostgresDataRootAcl {
    $serviceRecord = Get-CimInstance -ClassName Win32_Service -Filter "Name='$pgServiceName'" -ErrorAction SilentlyContinue
    $accountName = if ($null -ne $serviceRecord) { [string]$serviceRecord.StartName } else { '.\MDGsmPgSvc' }
    if ([string]::IsNullOrWhiteSpace($accountName) -or
        $accountName -match '^(LocalSystem|NT AUTHORITY\\|NT SERVICE\\)') {
        throw 'The managed PostgreSQL service is using an unexpected built-in or service-SID account.'
    }
    $accountSid = Get-AccountSid $accountName

    if (-not (Test-Path -LiteralPath $pgDataRoot -PathType Container)) {
        New-Item -ItemType Directory -Path $pgDataRoot -Force | Out-Null
    }
    $adminSid = Get-CurrentAdminSid
    $systemSid = [Security.Principal.SecurityIdentifier]::new('S-1-5-18')
    $acl = [System.Security.AccessControl.DirectorySecurity]::new()
    $acl.SetAccessRuleProtection($true, $false)
    $acl.SetOwner($adminSid)
    foreach ($sid in @($adminSid, $systemSid)) {
        $rule = [System.Security.AccessControl.FileSystemAccessRule]::new(
            $sid, [System.Security.AccessControl.FileSystemRights]::FullControl,
            [System.Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [System.Security.AccessControl.InheritanceFlags]::ObjectInherit,
            [System.Security.AccessControl.PropagationFlags]::None,
            [System.Security.AccessControl.AccessControlType]::Allow)
        $acl.AddAccessRule($rule)
    }
    $serviceRule = [System.Security.AccessControl.FileSystemAccessRule]::new(
        $accountSid, [System.Security.AccessControl.FileSystemRights]::Modify,
        [System.Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [System.Security.AccessControl.InheritanceFlags]::ObjectInherit,
        [System.Security.AccessControl.PropagationFlags]::None,
        [System.Security.AccessControl.AccessControlType]::Allow)
    $acl.AddAccessRule($serviceRule)
    [System.IO.Directory]::SetAccessControl($pgDataRoot, $acl)
}

function Ensure-PostgresServiceAccount {
    $accountName = 'MDGsmPgSvc'
    $localUser = Get-LocalUser -Name $accountName -ErrorAction SilentlyContinue
    if ($null -ne $localUser -and $state.Status -ne 'provisioning') {
        throw 'The PostgreSQL service account exists outside an incomplete product provisioning operation; Setup will not take it over.'
    }
    $securePassword = [System.Security.SecureString]::new()
    foreach ($character in $script:clusterSecrets.ServicePassword.ToCharArray()) {
        $securePassword.AppendChar($character)
    }
    $securePassword.MakeReadOnly()
    if ($null -eq $localUser) {
        New-LocalUser -Name $accountName -Password $securePassword -AccountNeverExpires -PasswordNeverExpires -UserMayNotChangePassword -Description 'MD-Gsm managed PostgreSQL 18 service account' | Out-Null
    }
    else {
        Set-LocalUser -Name $accountName -Password $securePassword
    }
    $localUser = Get-LocalUser -Name $accountName -ErrorAction Stop
    if ([string]$localUser.Description -ne 'MD-Gsm managed PostgreSQL 18 service account') {
        throw 'The local PostgreSQL service account description does not match the managed product account.'
    }
}

function Write-State($State) {
    Set-RestrictedDirectoryAcl $stateDirectory $false
    $tempPath = $statePath + '.tmp'
    $json = $State | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText($tempPath, $json, [System.Text.UTF8Encoding]::new($false))
    Set-RestrictedFileAcl $tempPath $false
    if (Test-Path -LiteralPath $statePath) {
        [System.IO.File]::Replace($tempPath, $statePath, $null, $true)
    } else {
        [System.IO.File]::Move($tempPath, $statePath)
    }
}

function New-RandomPassword {
    $bytes = [byte[]]::new(48)
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) }
    finally { $rng.Dispose() }
    try {
        return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
    }
    finally { [System.Array]::Clear($bytes, 0, $bytes.Length) }
}

function Get-FreeLocalPort {
    for ($candidate = 5432; $candidate -le 5500; $candidate++) {
        $listener = Get-NetTCPConnection -LocalPort $candidate -State Listen -ErrorAction SilentlyContinue
        if (-not $listener) { return $candidate }
    }
    throw 'No free local PostgreSQL port was found in the approved 5432-5500 range.'
}

function Write-RestrictedTextFile([string] $Path, [string] $Text) {
    $parent = [System.IO.Path]::GetDirectoryName([System.IO.Path]::GetFullPath($Path))
    Set-RestrictedDirectoryAcl $parent $false
    [System.IO.File]::WriteAllText($Path, $Text, [System.Text.Encoding]::ASCII)
    Set-RestrictedFileAcl $Path $false
}

function Invoke-Psql([string] $Database, [string] $User, [string] $Password, [string] $Sql) {
    $sqlPath = Join-Path $provisioningRoot ('sql-' + [Guid]::NewGuid().ToString('N') + '.txt')
    $oldPassword = $env:PGPASSWORD
    try {
        Set-RestrictedDirectoryAcl $provisioningRoot $false
        Write-RestrictedTextFile $sqlPath $Sql
        $env:PGPASSWORD = $Password
        $output = & $psqlPath -X -w -h 127.0.0.1 -p ([string]$state.Port) -U $User -d $Database -v ON_ERROR_STOP=1 -t -A -f $sqlPath 2>&1
        $exitCode = $LASTEXITCODE
        if ($exitCode -ne 0) {
            throw "A PostgreSQL provisioning operation failed (psql exit $exitCode). Sensitive SQL output was suppressed."
        }
        return (($output | ForEach-Object { [string]$_ }) -join [Environment]::NewLine).Trim()
    }
    finally {
        if ($null -eq $oldPassword) { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }
        else { $env:PGPASSWORD = $oldPassword }
        Remove-Item -LiteralPath $sqlPath -Force -ErrorAction SilentlyContinue
    }
}

function Wait-PostgresReady {
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        & $pgIsReadyPath -h 127.0.0.1 -p ([string]$state.Port) -U postgres -d postgres | Out-Null
        if ($LASTEXITCODE -eq 0) { return }
        Start-Sleep -Seconds 2
    }
    throw 'The managed PostgreSQL 18 Windows service did not become ready within 120 seconds.'
}

function Get-RoleExists([string] $Role, [string] $AdminPassword) {
    $result = Invoke-Psql 'postgres' 'postgres' $AdminPassword "SELECT count(*) FROM pg_roles WHERE rolname = '$Role';"
    return ($result -eq '1')
}

function Get-DatabaseExists([string] $AdminPassword) {
    $result = Invoke-Psql 'postgres' 'postgres' $AdminPassword "SELECT count(*) FROM pg_database WHERE datname = '$databaseName';"
    return ($result -eq '1')
}

function Test-ManagedCluster([string] $AdminPassword) {
    $version = Invoke-Psql 'postgres' 'postgres' $AdminPassword 'SHOW server_version;'
    if ($version -notmatch '^18\.') { throw 'The managed PostgreSQL service is not major version 18.' }
    $listen = Invoke-Psql 'postgres' 'postgres' $AdminPassword 'SHOW listen_addresses;'
    if ($listen -ne '127.0.0.1') {
        Invoke-Psql 'postgres' 'postgres' $AdminPassword "ALTER SYSTEM SET listen_addresses = '127.0.0.1';"
        Restart-Service -Name $pgServiceName -Force
        Wait-PostgresReady
    }
}

function Test-ApiConnectionString([string] $ConnectionString) {
    $pattern = '^Host=127\.0\.0\.1;Port=(\d{4,5});Database=mdgsm;Username=mdgsm_api_runtime;Password=([A-Za-z0-9_-]{40,80});Timeout=15;Command Timeout=30;Pooling=true$'
    if ($ConnectionString -notmatch $pattern) {
        throw 'The protected API connection string does not match the fixed local runtime configuration.'
    }
    if ([int]$Matches[1] -ne [int]$state.Port) {
        throw 'The protected API connection string port does not match the managed cluster.'
    }
    return $Matches[2]
}

function Invoke-InstallPostgreSql {
    $downloadRoot = Join-Path $provisioningRoot 'download'
    Set-RestrictedDirectoryAcl $downloadRoot $false
    $installerPath = Join-Path $downloadRoot 'postgresql-18.6-5-windows-x64.exe'
    Write-Host 'Downloading the pinned PostgreSQL 18.6-5 installer and verifying its SHA-256...'
    Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
    $actualHash = (Get-FileHash -LiteralPath $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $expectedInstallerSha256) {
        Remove-Item -LiteralPath $installerPath -Force -ErrorAction SilentlyContinue
        throw 'The PostgreSQL installer checksum did not match the approved SHA-256.'
    }

    $optionFile = Join-Path $provisioningRoot 'postgresql-installer-options.txt'
    $options = @(
        'mode=unattended',
        'unattendedmodeui=none',
        'superaccount=postgres',
        "superpassword=$($script:clusterSecrets.SuperuserPassword)",
        'serviceaccount=MDGsmPgSvc',
        "servicepassword=$($script:clusterSecrets.ServicePassword)",
        "serverport=$($state.Port)",
        "prefix=$pgInstallRoot",
        "datadir=$pgDataDirectory",
        "servicename=$pgServiceName",
        'enable-components=server,commandlinetools',
        'enable_acledit=1'
    )
    Write-RestrictedTextFile $optionFile ($options -join [Environment]::NewLine)
    try {
        $process = Start-Process -FilePath $installerPath -ArgumentList @('--mode', 'unattended', '--unattendedmodeui', 'none', '--optionfile', $optionFile) -Wait -PassThru -NoNewWindow
        if ($process.ExitCode -ne 0) {
            throw "The verified PostgreSQL installer failed with exit code $($process.ExitCode)."
        }
    }
    finally {
        Remove-Item -LiteralPath $optionFile -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $installerPath -Force -ErrorAction SilentlyContinue
    }
}

try {
    $null = Get-CurrentAdminSid
    Set-RestrictedDirectoryAcl $provisioningRoot $false
    Set-RestrictedDirectoryAcl $stateDirectory $false
    if (-not (Test-Path -LiteralPath $pgDataRoot -PathType Container)) {
        New-Item -ItemType Directory -Path $pgDataRoot -Force | Out-Null
    }
    $service = Get-Service -Name $pgServiceName -ErrorAction SilentlyContinue

    if (Test-Path -LiteralPath $statePath) {
        $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
        if ($state.SchemaVersion -ne 1 -or $state.ServiceName -ne $pgServiceName -or $state.DatabaseName -ne $databaseName -or $state.RuntimeLogin -ne $runtimeLogin) {
            throw 'The existing PostgreSQL installation state is invalid or does not match this product.'
        }
        if ($null -eq $service) {
            if (Test-Path -LiteralPath $pgDataDirectory -PathType Container) {
                throw 'The managed PostgreSQL service is missing while its data directory exists. Setup will preserve the cluster and will not reinitialize it.'
            }
            if ($state.Status -eq 'ready') {
                throw 'The PostgreSQL service is missing from a completed installation; Repair will not recreate it automatically.'
            }
        }
        elseif (-not (Test-Path -LiteralPath $pgDataDirectory -PathType Container)) {
            throw 'The managed PostgreSQL data directory is missing; refusing to create a new empty cluster during Repair.'
        }
        $script:clusterSecrets = (Unprotect-Text $adminSecretPath) | ConvertFrom-Json
        if ([string]::IsNullOrWhiteSpace($script:clusterSecrets.SuperuserPassword) -or [string]::IsNullOrWhiteSpace($script:clusterSecrets.ServicePassword)) {
            throw 'The protected PostgreSQL maintenance secret bundle is invalid.'
        }
    }
    else {
        if ($null -ne $service -or (Test-Path -LiteralPath $pgDataDirectory)) {
            throw 'An unregistered PostgreSQL 18 service or data directory already exists. Setup will not adopt, overwrite, or reinitialize it.'
        }
        $state = [ordered]@{
            SchemaVersion = 1
            Status = 'provisioning'
            ServiceName = $pgServiceName
            InstallRoot = $pgInstallRoot
            DataDirectory = $pgDataDirectory
            Port = Get-FreeLocalPort
            DatabaseName = $databaseName
            RuntimeLogin = $runtimeLogin
            PostgreSqlMajor = 18
            CreatedAtUtc = [DateTime]::UtcNow.ToString('o')
        }
        $script:clusterSecrets = [ordered]@{
            SuperuserPassword = New-RandomPassword
            ServicePassword = New-RandomPassword
        }
        Protect-Text ($script:clusterSecrets | ConvertTo-Json -Compress) $adminSecretPath $false
        Write-State $state
    }

    if ($null -eq $service) {
        Ensure-PostgresServiceAccount
        Set-PostgresDataRootAcl
        Invoke-InstallPostgreSql
        $service = Get-Service -Name $pgServiceName -ErrorAction Stop
    }
    Set-PostgresDataRootAcl
    if ($service.Status -ne 'Running') { Start-Service -Name $pgServiceName }
    Wait-PostgresReady

    if (-not (Test-Path -LiteralPath $psqlPath -PathType Leaf)) {
        throw 'The PostgreSQL 18 psql client is missing from the managed installation.'
    }
    Test-ManagedCluster $script:clusterSecrets.SuperuserPassword

    $dbExists = Get-DatabaseExists $script:clusterSecrets.SuperuserPassword
    if (-not $dbExists) {
        if ($state.Status -eq 'ready') {
            throw 'The product database is missing from a completed installation; Repair will not recreate it silently.'
        }
        Invoke-Psql 'postgres' 'postgres' $script:clusterSecrets.SuperuserPassword 'CREATE DATABASE mdgsm OWNER postgres;'
    }

    $connectionString = $null
    $apiPassword = $null
    if (Test-Path -LiteralPath $ProtectedConnectionFile -PathType Leaf) {
        $connectionString = Unprotect-Text $ProtectedConnectionFile
        $apiPassword = Test-ApiConnectionString $connectionString
    }
    else {
        if (Get-RoleExists $runtimeLogin $script:clusterSecrets.SuperuserPassword) {
            throw 'The API LOGIN exists but its protected connection credential is missing; Setup will not rotate or reset it.'
        }
        $apiPassword = New-RandomPassword
        $connectionString = "Host=127.0.0.1;Port=$($state.Port);Database=mdgsm;Username=mdgsm_api_runtime;Password=$apiPassword;Timeout=15;Command Timeout=30;Pooling=true"
        Protect-Text $connectionString $ProtectedConnectionFile $true
    }

    $runtimeExists = Get-RoleExists $runtimeLogin $script:clusterSecrets.SuperuserPassword
    if (-not $runtimeExists) {
        if ($state.Status -eq 'ready') {
            throw 'The API LOGIN is missing from a completed installation; Repair will not silently recreate it.'
        }
        if (-not (Get-RoleExists $runtimeGroup $script:clusterSecrets.SuperuserPassword)) {
            Invoke-Psql 'postgres' 'postgres' $script:clusterSecrets.SuperuserPassword "CREATE ROLE mdgsm_runtime NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;" | Out-Null
        }
        $sql = "CREATE ROLE mdgsm_api_runtime LOGIN PASSWORD '$apiPassword' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS INHERIT;"
        Invoke-Psql 'postgres' 'postgres' $script:clusterSecrets.SuperuserPassword $sql | Out-Null
    }
    else {
        $roleAttributes = Invoke-Psql 'postgres' 'postgres' $script:clusterSecrets.SuperuserPassword "SELECT (rolcanlogin AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolreplication AND NOT rolbypassrls AND rolinherit)::text FROM pg_roles WHERE rolname = 'mdgsm_api_runtime';"
        if ($roleAttributes -ne 'true') { throw 'The existing API LOGIN has unexpected PostgreSQL role attributes.' }
        if (-not (Get-RoleExists $runtimeGroup $script:clusterSecrets.SuperuserPassword)) {
            throw 'The existing API LOGIN has no approved mdgsm_runtime group role; Repair will not broaden or recreate role policy.'
        }
    }

    $groupAttributes = Invoke-Psql 'postgres' 'postgres' $script:clusterSecrets.SuperuserPassword "SELECT (NOT rolcanlogin AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolreplication AND NOT rolbypassrls AND NOT rolinherit)::text FROM pg_roles WHERE rolname = 'mdgsm_runtime';"
    if ($groupAttributes -ne 'true') { throw 'The mdgsm_runtime group role has unexpected PostgreSQL role attributes.' }

    Invoke-Psql 'postgres' 'postgres' $script:clusterSecrets.SuperuserPassword "GRANT mdgsm_runtime TO mdgsm_api_runtime WITH INHERIT TRUE, SET FALSE, ADMIN FALSE;" | Out-Null
    Invoke-Psql 'postgres' 'postgres' $script:clusterSecrets.SuperuserPassword "GRANT CONNECT ON DATABASE mdgsm TO mdgsm_api_runtime;" | Out-Null
    $membership = Invoke-Psql 'postgres' 'postgres' $script:clusterSecrets.SuperuserPassword "SELECT (m.inherit_option AND NOT m.set_option AND NOT m.admin_option)::text FROM pg_auth_members m JOIN pg_roles parent ON parent.oid=m.roleid JOIN pg_roles member ON member.oid=m.member WHERE parent.rolname='mdgsm_runtime' AND member.rolname='mdgsm_api_runtime';"
    if ($membership -ne 'true') { throw 'The API LOGIN membership options do not match the least-privilege runtime policy.' }

    $apiIdentity = Invoke-Psql 'mdgsm' 'mdgsm_api_runtime' $apiPassword "SELECT session_user || '|' || current_user || '|' || current_database() || '|' || (current_setting('server_version_num')::integer / 10000)::text || '|' || inet_server_addr()::text;"
    if ($apiIdentity -ne 'mdgsm_api_runtime|mdgsm_api_runtime|mdgsm|18|127.0.0.1') {
        throw 'The generated API LOGIN failed the exact PostgreSQL identity/version/loopback proof.'
    }

    $state.Status = 'ready'
    $state.RuntimeIdentityVerifiedAtUtc = [DateTime]::UtcNow.ToString('o')
    Write-State $state
    Write-Host 'PASS: managed PostgreSQL 18 verified; mdgsm database and least-privilege API LOGIN provisioned; connection secret DPAPI-protected and ACL-restricted.'
    Write-Host 'No application schema migrations were executed.'
}
catch {
    $failure = $_
    $safeMessage = [string]$failure.Exception.Message
    $secretValues = @()
    if ($null -ne $script:clusterSecrets) {
        $secretValues += [string]$script:clusterSecrets.SuperuserPassword
        $secretValues += [string]$script:clusterSecrets.ServicePassword
    }
    if (-not [string]::IsNullOrWhiteSpace([string]$apiPassword)) {
        $secretValues += [string]$apiPassword
    }
    foreach ($secretValue in $secretValues) {
        if (-not [string]::IsNullOrWhiteSpace($secretValue)) {
            $safeMessage = $safeMessage.Replace($secretValue, '[REDACTED]')
        }
    }
    $safeMessage = $safeMessage -replace '(?i)(password|pwd)\s*=\s*[^;,\s]+', '$1=[REDACTED]'
    $safeMessage = $safeMessage -replace '(?i)PGPASSWORD\s*=\s*[^;,\s]+', 'PGPASSWORD=[REDACTED]'
    Write-Output ('PROVISIONING FAILURE: {0}: {1}' -f $failure.Exception.GetType().Name, $safeMessage)
    throw [System.InvalidOperationException]::new("Central PostgreSQL provisioning failed: $safeMessage")
}
finally {
    $script:clusterSecrets = $null
    $apiPassword = $null
    $connectionString = $null
    if ($null -ne $env:PGPASSWORD) { Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue }
}
