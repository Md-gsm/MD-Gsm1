using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using MDGsm.Application.Persons;

namespace MDGsm.Infrastructure.Persons;

/// <summary>
/// Windows DPAPI LocalMachine key provider for the Persons temporary-workflow purpose.
/// It never provisions on read; setup must call ProvisionNewKey explicitly under the
/// final API identity before the API registers this provider.
/// </summary>
public sealed class WindowsDpapiTemporaryWorkflowEncryptionKeyProvider : ITemporaryWorkflowEncryptionKeyProvider
{
    public const string ConfigurationKey = "Persons:WorkflowEncryption:KeyFilePath";
    public const string KeyVersion = "dpapi-localmachine-v1";
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("MD-Gsm/Persons/TemporaryWorkflow/DPAPI/v1");
    private static readonly byte[] Header = Encoding.ASCII.GetBytes("MDGSPWK1");
    private readonly string keyFilePath;

    public WindowsDpapiTemporaryWorkflowEncryptionKeyProvider(string keyFilePath)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Persons workflow DPAPI key custody requires Windows.");
        this.keyFilePath = ValidatePath(keyFilePath);
    }

    public ValueTask<byte[]> GetKeyAsync(string keyVersion, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(keyVersion, KeyVersion, StringComparison.Ordinal))
            throw new TemporaryWorkflowPayloadException();
        try
        {
            var file = File.ReadAllBytes(keyFilePath);
            if (file.Length <= Header.Length || !file.AsSpan(0, Header.Length).SequenceEqual(Header))
                throw new TemporaryWorkflowPayloadException();
            var protectedKey = file.AsSpan(Header.Length).ToArray();
            try
            {
                var key = ProtectedData.Unprotect(protectedKey, Entropy, DataProtectionScope.LocalMachine);
                if (key.Length != 32)
                {
                    CryptographicOperations.ZeroMemory(key);
                    throw new TemporaryWorkflowPayloadException();
                }
                return ValueTask.FromResult(key);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(protectedKey);
                CryptographicOperations.ZeroMemory(file);
            }
        }
        catch (TemporaryWorkflowPayloadException) { throw; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            throw new TemporaryWorkflowPayloadException();
        }
    }

    /// <summary>
    /// Explicit one-time setup operation. Run as the final, dedicated API identity.
    /// It refuses to overwrite an existing key and returns only non-secret metadata.
    /// </summary>
    public static void ProvisionNewKey(string keyFilePath)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Persons workflow DPAPI key custody requires Windows.");
        var path = ValidatePath(keyFilePath);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

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
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            {
                createdByThisCall = true;
                stream.Write(file);
                stream.Flush(flushToDisk: true);
            }

            var fileSecurity = new FileSecurity();
            fileSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            fileSecurity.AddAccessRule(new FileSystemAccessRule(apiSid, FileSystemRights.Read,
                AccessControlType.Allow));
            fileSecurity.AddAccessRule(new FileSystemAccessRule(systemSid, FileSystemRights.FullControl,
                AccessControlType.Allow));
            new FileInfo(path).SetAccessControl(fileSecurity);

            var verified = ProtectedData.Unprotect(protectedKey, Entropy, DataProtectionScope.LocalMachine);
            try
            {
                if (verified.Length != 32 || !CryptographicOperations.FixedTimeEquals(verified, key))
                    throw new CryptographicException("Provisioned key verification failed.");
            }
            finally { CryptographicOperations.ZeroMemory(verified); }

            var directorySecurity = new DirectorySecurity();
            directorySecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            var apiSid = WindowsIdentity.GetCurrent().User
                ?? throw new InvalidOperationException("The setup process has no Windows SID.");
            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            directorySecurity.AddAccessRule(new FileSystemAccessRule(
                apiSid, FileSystemRights.ReadAndExecute, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
            directorySecurity.AddAccessRule(new FileSystemAccessRule(
                systemSid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(directory).SetAccessControl(directorySecurity);
        }
        catch
        {
            // Never delete/replace a pre-existing file. If this invocation created a file
            // but failed before verification, remove only that just-created artifact.
            if (createdByThisCall && File.Exists(path))
                File.Delete(path);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            if (protectedKey is not null) CryptographicOperations.ZeroMemory(protectedKey);
            if (file is not null) CryptographicOperations.ZeroMemory(file);
        }
    }

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
