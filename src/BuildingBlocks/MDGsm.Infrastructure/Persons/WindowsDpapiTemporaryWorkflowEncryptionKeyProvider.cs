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

        ProvisionNewKey(keyFilePath, serviceSid);
    }

    /// <summary>
    /// Explicit one-time provisioning operation for a caller-supplied runtime SID.
    /// Production Setup must call ProvisionNewKeyForService after registering the service SID.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public static void ProvisionNewKey(string keyFilePath, SecurityIdentifier runtimeServiceSid)
    {
        ArgumentNullException.ThrowIfNull(runtimeServiceSid);
        var path = ValidatePath(keyFilePath);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        var setupSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The setup process has no Windows SID.");
        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);

        if (File.Exists(path))
            throw new IOException("A Persons workflow key already exists; provisioning never overwrites it.");

        // Restrict the directory before the protected blob is written. This closes
        // the inherited-ProgramData-ACL window during first-time provisioning.
        SetDirectoryAcl(directory, setupSid, systemSid, FileSystemRights.Modify);

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

            SetFileAcl(path, runtimeServiceSid, systemSid);
            SetDirectoryAcl(directory, runtimeServiceSid, systemSid, FileSystemRights.ReadAndExecute);
        }
        catch
        {
            // A failed first provision is cleaned using a temporary setup-only ACL.
            // No cleanup path is allowed to replace an existing key.
            try
            {
                if (Directory.Exists(directory))
                    SetDirectoryAcl(directory, setupSid, systemSid, FileSystemRights.Modify);
                if (createdByThisCall && File.Exists(path))
                {
                    var cleanupSecurity = new FileSecurity();
                    cleanupSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
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

    [SupportedOSPlatform("windows")]
    private static void SetFileAcl(string path, SecurityIdentifier runtimeServiceSid, SecurityIdentifier systemSid)
    {
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(runtimeServiceSid, FileSystemRights.Read, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(systemSid, FileSystemRights.FullControl, AccessControlType.Allow));
        new FileInfo(path).SetAccessControl(security);
    }

    [SupportedOSPlatform("windows")]
    private static void SetDirectoryAcl(
        string directory, SecurityIdentifier apiSid, SecurityIdentifier systemSid, FileSystemRights apiRights)
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
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
