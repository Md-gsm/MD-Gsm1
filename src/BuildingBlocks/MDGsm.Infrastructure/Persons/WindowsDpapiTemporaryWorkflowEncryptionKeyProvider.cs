using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using MDGsm.Application.Persons;

namespace MDGsm.Infrastructure.Persons;

/// <summary>
/// Windows DPAPI LocalMachine provider dedicated to Persons temporary-workflow payloads.
/// Reading never creates or replaces key material; provisioning is an explicit setup action.
/// </summary>
public sealed class WindowsDpapiTemporaryWorkflowEncryptionKeyProvider : ITemporaryWorkflowEncryptionKeyProvider
{
    public const string ConfigurationKey = "Persons:WorkflowEncryption:KeyFilePath";
    public const string KeyVersion = "dpapi-localmachine-v1";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MD-Gsm/Persons/TemporaryWorkflow/DPAPI/v1");
    private static readonly byte[] Header = Encoding.ASCII.GetBytes("MDGSPWK1");
    private readonly string keyFilePath;

    [SupportedOSPlatform("windows")]
    public WindowsDpapiTemporaryWorkflowEncryptionKeyProvider(string keyFilePath)
    {
        this.keyFilePath = ValidatePath(keyFilePath);
    }

    [SupportedOSPlatform("windows")]
    public ValueTask<byte[]> GetKeyAsync(string keyVersion, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(keyVersion, KeyVersion, StringComparison.Ordinal))
            throw new TemporaryWorkflowPayloadException();

        byte[]? file = null;
        byte[]? protectedKey = null;
        try
        {
            file = File.ReadAllBytes(keyFilePath);
            if (file.Length <= Header.Length || !file.AsSpan(0, Header.Length).SequenceEqual(Header))
                throw new TemporaryWorkflowPayloadException();

            protectedKey = file.AsSpan(Header.Length).ToArray();
            var key = ProtectedData.Unprotect(protectedKey, Entropy, DataProtectionScope.LocalMachine);
            if (key.Length != 32)
            {
                CryptographicOperations.ZeroMemory(key);
                throw new TemporaryWorkflowPayloadException();
            }
            return ValueTask.FromResult(key);
        }
        catch (TemporaryWorkflowPayloadException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            throw new TemporaryWorkflowPayloadException();
        }
        finally
        {
            if (protectedKey is not null) CryptographicOperations.ZeroMemory(protectedKey);
            if (file is not null) CryptographicOperations.ZeroMemory(file);
        }
    }

    public const string ApiServiceName = "MDGsmApi";

    /// <summary>
    /// Setup resolves the service SID after creating the Windows service and enabling its SID type.
    /// The elevated setup identity is never used as the runtime key reader.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static void ProvisionNewKeyForService(string keyFilePath, string serviceName)
    {
        if (!string.Equals(serviceName, ApiServiceName, StringComparison.Ordinal))
            throw new InvalidOperationException("Only the approved MDGsmApi service identity can receive this key.");

        var serviceSid = new NTAccount("NT SERVICE", serviceName)
            .Translate(typeof(SecurityIdentifier)) as SecurityIdentifier
            ?? throw new InvalidOperationException("The MDGsmApi service SID could not be resolved.");
        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

        // Production key and directory ownership is transferred to LocalSystem.
        // The Setup identity is deliberately not retained as owner after provisioning.
        ProvisionNewKeyCore(keyFilePath, serviceSid, systemSid);
    }

    /// <summary>
    /// Test seam for provisioning with an explicitly supplied reader SID. Production
    /// Setup must use ProvisionNewKeyForService so LocalSystem owns the protected objects.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static void ProvisionNewKey(string keyFilePath, SecurityIdentifier runtimeServiceSid)
    {
        ArgumentNullException.ThrowIfNull(runtimeServiceSid);
        ProvisionNewKeyCore(keyFilePath, runtimeServiceSid, storageOwnerSid: null);
    }

    [SupportedOSPlatform("windows")]
    private static void ProvisionNewKeyCore(
        string keyFilePath,
        SecurityIdentifier runtimeServiceSid,
        SecurityIdentifier? storageOwnerSid)
    {
        var path = ValidatePath(keyFilePath);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        var setupSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The setup process has no Windows SID.");
        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var finalOwnerSid = storageOwnerSid ?? setupSid;

        if (File.Exists(path))
            throw new IOException("A Persons workflow key already exists; provisioning never overwrites it.");

        // Restrict the directory before the protected blob is written. This closes
        // the inherited-ProgramData-ACL window during first-time provisioning.
        SetDirectoryAcl(directory, setupSid, systemSid, FileSystemRights.Modify, setupSid);

        var key = RandomNumberGenerator.GetBytes(32);
        byte[]? protectedKey = null;
        byte[]? file = null;
        var createdByThisCall = false;
        try
        {
            protectedKey = ProtectedData.Protect(key, Entropy, DataProtectionScope.LocalMachine);
            file = new byte[Header.Length + protectedKey.Length];
            Header.CopyTo(file, 0);
            protectedKey.CopyTo(file, Header.Length);

            // Keep the inherited setup ACL only while creating the one-time file.
            // The final protected DACL excludes the interactive/elevated setup identity.
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            {
                createdByThisCall = true;
                stream.Write(file);
                stream.Flush(flushToDisk: true);
            }

            var verified = ProtectedData.Unprotect(protectedKey, Entropy, DataProtectionScope.LocalMachine);
            try
            {
                if (verified.Length != 32 || !CryptographicOperations.FixedTimeEquals(verified, key))
                    throw new CryptographicException("Provisioned key verification failed.");
            }
            finally { CryptographicOperations.ZeroMemory(verified); }

            SetFileAcl(path, runtimeServiceSid, systemSid, finalOwnerSid);
            SetDirectoryAcl(directory, runtimeServiceSid, systemSid, FileSystemRights.ReadAndExecute, finalOwnerSid);
        }
        catch
        {
            // A failed first provision is cleaned using a temporary setup-only ACL.
            // No cleanup path is allowed to replace an existing key.
            try
            {
                if (Directory.Exists(directory))
                    SetDirectoryAcl(directory, setupSid, systemSid, FileSystemRights.Modify, setupSid);
                if (createdByThisCall && File.Exists(path))
                {
                    var cleanupSecurity = new FileSecurity();
                    cleanupSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                    cleanupSecurity.SetOwner(setupSid);
                    cleanupSecurity.AddAccessRule(new FileSystemAccessRule(setupSid, FileSystemRights.FullControl, AccessControlType.Allow));
                    cleanupSecurity.AddAccessRule(new FileSystemAccessRule(systemSid, FileSystemRights.FullControl, AccessControlType.Allow));
                    new FileInfo(path).SetAccessControl(cleanupSecurity);
                    File.Delete(path);
                }
            }
            catch { /* Preserve the original failure; any partial file remains fail-closed. */ }
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            if (protectedKey is not null) CryptographicOperations.ZeroMemory(protectedKey);
            if (file is not null) CryptographicOperations.ZeroMemory(file);
        }
    }

    /// <summary>
    /// Called by the API hosted service while running under the actual MDGsmApi token.
    /// It verifies the service SID is present in that token and that both protected
    /// objects are owned by LocalSystem with the exact two-principal DACL.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public void ValidateKeyStorageForCurrentService()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var localServiceSid = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
        if (identity.User is null || !identity.User.Equals(localServiceSid))
            throw new InvalidOperationException("The Persons workflow key may only be validated by the approved LocalService API process.");

        var serviceSid = new NTAccount("NT SERVICE", ApiServiceName)
            .Translate(typeof(SecurityIdentifier)) as SecurityIdentifier
            ?? throw new InvalidOperationException("The MDGsmApi service SID could not be resolved.");
        if (identity.Groups is null || !identity.Groups.Contains(serviceSid))
            throw new InvalidOperationException("The MDGsmApi service SID is not enabled in the current Windows service token.");

        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        ValidateProtectedKeyStorage(keyFilePath, serviceSid, systemSid);
    }

    /// <summary>
    /// Validates owner and exact DACL semantics. Exposed as a deterministic test seam;
    /// production calls it only through ValidateKeyStorageForCurrentService.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static void ValidateProtectedKeyStorage(
        string keyFilePath,
        SecurityIdentifier expectedReaderSid,
        SecurityIdentifier expectedOwnerSid)
    {
        ArgumentNullException.ThrowIfNull(expectedReaderSid);
        ArgumentNullException.ThrowIfNull(expectedOwnerSid);
        var path = ValidatePath(keyFilePath);
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("The Persons workflow key directory is invalid.");
        var sections = AccessControlSections.Access | AccessControlSections.Owner;

        var directorySecurity = new DirectoryInfo(directory).GetAccessControl(sections);
        var fileSecurity = new FileInfo(path).GetAccessControl(sections);
        ValidateProtectedAccessRules(directorySecurity, expectedReaderSid, expectedOwnerSid, isDirectory: true);
        ValidateProtectedAccessRules(fileSecurity, expectedReaderSid, expectedOwnerSid, isDirectory: false);
    }

    [SupportedOSPlatform("windows")]
    private static void ValidateProtectedAccessRules(
        FileSystemSecurity security,
        SecurityIdentifier expectedReaderSid,
        SecurityIdentifier expectedOwnerSid,
        bool isDirectory)
    {
        if (!security.AreAccessRulesProtected)
            throw new InvalidOperationException("The Persons workflow key DACL must be protected from inheritance.");

        var owner = security.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
        if (owner is null || !owner.Equals(expectedOwnerSid))
            throw new InvalidOperationException("The Persons workflow key object has an unexpected owner.");

        var rules = security.GetAccessRules(
                includeExplicit: true,
                includeInherited: true,
                targetType: typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .ToArray();
        if (rules.Length != 2 || rules.Any(rule =>
                rule.IsInherited || rule.AccessControlType != AccessControlType.Allow))
            throw new InvalidOperationException("The Persons workflow key DACL must contain exactly two explicit allow ACEs.");

        var readerRule = rules.SingleOrDefault(rule =>
            rule.IdentityReference.Equals(expectedReaderSid));
        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var systemRule = rules.SingleOrDefault(rule =>
            rule.IdentityReference.Equals(systemSid));
        if (readerRule is null || systemRule is null)
            throw new InvalidOperationException("The Persons workflow key DACL contains an unexpected principal.");

        if ((systemRule.FileSystemRights & FileSystemRights.FullControl) != FileSystemRights.FullControl)
            throw new InvalidOperationException("LocalSystem must retain FullControl over the Persons workflow key.");

        var expectedInheritance = isDirectory
            ? InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit
            : InheritanceFlags.None;
        if (readerRule.InheritanceFlags != expectedInheritance ||
            systemRule.InheritanceFlags != expectedInheritance ||
            readerRule.PropagationFlags != PropagationFlags.None ||
            systemRule.PropagationFlags != PropagationFlags.None)
            throw new InvalidOperationException("The Persons workflow key ACE inheritance flags are invalid.");

        var rights = readerRule.FileSystemRights;
        var required = isDirectory ? FileSystemRights.ReadAndExecute : FileSystemRights.Read;
        var prohibited = FileSystemRights.WriteData |
                         FileSystemRights.AppendData |
                         FileSystemRights.WriteAttributes |
                         FileSystemRights.WriteExtendedAttributes |
                         FileSystemRights.Delete |
                         FileSystemRights.DeleteSubdirectoriesAndFiles |
                         FileSystemRights.ChangePermissions |
                         FileSystemRights.TakeOwnership;
        if ((rights & required) != required || (rights & prohibited) != 0)
            throw new InvalidOperationException("The MDGsmApi service SID must have read-only access to the Persons workflow key storage.");
    }

    [SupportedOSPlatform("windows")]
    private static void SetFileAcl(
        string path,
        SecurityIdentifier runtimeServiceSid,
        SecurityIdentifier systemSid,
        SecurityIdentifier ownerSid)
    {
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(ownerSid);
        security.AddAccessRule(new FileSystemAccessRule(runtimeServiceSid, FileSystemRights.Read, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(systemSid, FileSystemRights.FullControl, AccessControlType.Allow));
        new FileInfo(path).SetAccessControl(security);
    }

    [SupportedOSPlatform("windows")]
    private static void SetDirectoryAcl(
        string directory,
        SecurityIdentifier apiSid,
        SecurityIdentifier systemSid,
        FileSystemRights apiRights,
        SecurityIdentifier ownerSid)
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.SetOwner(ownerSid);
        security.AddAccessRule(new FileSystemAccessRule(
            apiSid, apiRights, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            systemSid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(directory).SetAccessControl(security);
    }

    [SupportedOSPlatform("windows")]
    private static string ValidatePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
            throw new InvalidOperationException("A fully qualified DPAPI key path is required.");

        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var approvedRoot = Path.GetFullPath(Path.Combine(programData, "MD-Gsm", "Keys", "Persons")) +
                           Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(value);
        if (!fullPath.StartsWith(approvedRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The DPAPI key path must be under the approved ProgramData Persons key directory.");
        return fullPath;
    }
}
