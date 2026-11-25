using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using MDGsm.Application.Persons;
using MDGsm.Infrastructure.Persons;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

[SupportedOSPlatform("windows")]
public sealed class WindowsDpapiTemporaryWorkflowKeyProviderTests
{
    [Fact]
    public async Task Explicit_provision_round_trips_key_and_refuses_overwrite()
    {
        if (!OperatingSystem.IsWindows()) return;

        var directory = CreateIsolatedDirectory();
        var path = Path.Combine(directory, "workflow-key.dpapi");
        try
        {
            WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ProvisionNewKey(path, CurrentTestSid());
            var before = await File.ReadAllBytesAsync(path);
            var provider = new WindowsDpapiTemporaryWorkflowEncryptionKeyProvider(path);
            var key = await provider.GetKeyAsync(
                WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.KeyVersion, CancellationToken.None);
            try { Assert.Equal(32, key.Length); }
            finally { CryptographicOperations.ZeroMemory(key); }

            Assert.ThrowsAny<Exception>(() =>
                WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ProvisionNewKey(path, CurrentTestSid()));
            Assert.Equal(before, await File.ReadAllBytesAsync(path));
            CryptographicOperations.ZeroMemory(before);
        }
        finally { Cleanup(directory); }
    }

    [Fact]
    public async Task Missing_key_fails_closed_without_generating_replacement()
    {
        if (!OperatingSystem.IsWindows()) return;

        var directory = CreateIsolatedDirectory();
        var path = Path.Combine(directory, "missing-key.dpapi");
        try
        {
            var provider = new WindowsDpapiTemporaryWorkflowEncryptionKeyProvider(path);
            await Assert.ThrowsAsync<TemporaryWorkflowPayloadException>(async () =>
                await provider.GetKeyAsync(
                    WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.KeyVersion, CancellationToken.None));
            Assert.False(File.Exists(path));
        }
        finally { Cleanup(directory); }
    }

    [Fact]
    public void Unknown_key_version_fails_closed()
    {
        if (!OperatingSystem.IsWindows()) return;

        var directory = CreateIsolatedDirectory();
        var path = Path.Combine(directory, "missing-key.dpapi");
        try
        {
            var provider = new WindowsDpapiTemporaryWorkflowEncryptionKeyProvider(path);
            Assert.Throws<TemporaryWorkflowPayloadException>(() =>
                provider.GetKeyAsync("unknown-version", CancellationToken.None));
        }
        finally { Cleanup(directory); }
    }

    [Fact]
    public async Task Two_installations_receive_independent_keys()
    {
        if (!OperatingSystem.IsWindows()) return;

        var firstDirectory = CreateIsolatedDirectory();
        var secondDirectory = CreateIsolatedDirectory();
        try
        {
            var firstPath = Path.Combine(firstDirectory, "workflow-key.dpapi");
            var secondPath = Path.Combine(secondDirectory, "workflow-key.dpapi");
            WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ProvisionNewKey(firstPath, CurrentTestSid());
            WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ProvisionNewKey(secondPath, CurrentTestSid());

            var first = await new WindowsDpapiTemporaryWorkflowEncryptionKeyProvider(firstPath)
                .GetKeyAsync(WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.KeyVersion, CancellationToken.None);
            var second = await new WindowsDpapiTemporaryWorkflowEncryptionKeyProvider(secondPath)
                .GetKeyAsync(WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.KeyVersion, CancellationToken.None);
            try { Assert.False(CryptographicOperations.FixedTimeEquals(first, second)); }
            finally
            {
                CryptographicOperations.ZeroMemory(first);
                CryptographicOperations.ZeroMemory(second);
            }
        }
        finally
        {
            Cleanup(firstDirectory);
            Cleanup(secondDirectory);
        }
    }

    [Fact]
    public async Task Corrupted_protected_blob_fails_closed()
    {
        if (!OperatingSystem.IsWindows()) return;

        var directory = CreateIsolatedDirectory();
        var path = Path.Combine(directory, "workflow-key.dpapi");
        try
        {
            WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ProvisionNewKey(path, CurrentTestSid());
            var bytes = await File.ReadAllBytesAsync(path);
            bytes[^1] ^= 0x01;

            // ProvisionNewKey deliberately grants the API identity read-only access.
            // This test-only file is disposable, so grant the current test identity
            // Modify solely to corrupt the blob; production ACLs remain unchanged.
            var testIdentity = WindowsIdentity.GetCurrent().User
                ?? throw new InvalidOperationException("The Windows test identity has no SID.");
            var testFileSecurity = new FileSecurity();
            testFileSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            testFileSecurity.AddAccessRule(new FileSystemAccessRule(
                testIdentity, FileSystemRights.Modify, AccessControlType.Allow));
            testFileSecurity.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl, AccessControlType.Allow));
            new FileInfo(path).SetAccessControl(testFileSecurity);
            await File.WriteAllBytesAsync(path, bytes);
            CryptographicOperations.ZeroMemory(bytes);

            var provider = new WindowsDpapiTemporaryWorkflowEncryptionKeyProvider(path);
            await Assert.ThrowsAsync<TemporaryWorkflowPayloadException>(async () =>
                await provider.GetKeyAsync(
                    WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.KeyVersion, CancellationToken.None));
        }
        finally { Cleanup(directory); }
    }

    [Fact]
    public void Path_outside_programdata_persons_key_root_is_rejected()
    {
        if (!OperatingSystem.IsWindows()) return;

        Assert.Throws<InvalidOperationException>(() =>
            new WindowsDpapiTemporaryWorkflowEncryptionKeyProvider(
                Path.Combine(Path.GetTempPath(), "outside-approved-root.dpapi")));
    }

    [Fact]
    public void Provisioned_file_and_directory_acl_are_protected_and_include_only_reader_and_system()
    {
        if (!OperatingSystem.IsWindows()) return;

        var directory = CreateIsolatedDirectory();
        var path = Path.Combine(directory, "workflow-key.dpapi");
        var readerSid = CurrentTestSid();
        var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        try
        {
            WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ProvisionNewKey(path, readerSid);

            var fileAcl = new FileInfo(path).GetAccessControl();
            var directoryAcl = new DirectoryInfo(directory).GetAccessControl();
            Assert.True(fileAcl.AreAccessRulesProtected);
            Assert.True(directoryAcl.AreAccessRulesProtected);

            var expected = new[] { readerSid.Value, systemSid.Value }.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var fileSids = fileAcl.GetAccessRules(true, false, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>()
                .Select(rule => ((SecurityIdentifier)rule.IdentityReference).Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var directorySids = directoryAcl.GetAccessRules(true, false, typeof(SecurityIdentifier))
                .Cast<FileSystemAccessRule>()
                .Select(rule => ((SecurityIdentifier)rule.IdentityReference).Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected, fileSids);
            Assert.Equal(expected, directorySids);
            WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ValidateProtectedKeyStorage(
                path, readerSid, readerSid);
            Assert.Equal(readerSid.Value,
                ((SecurityIdentifier)fileAcl.GetOwner(typeof(SecurityIdentifier))!).Value);
            Assert.Equal(readerSid.Value,
                ((SecurityIdentifier)directoryAcl.GetOwner(typeof(SecurityIdentifier))!).Value);
        }
        finally { Cleanup(directory); }
    }

    [Fact]
    public void Protected_storage_validator_rejects_an_additional_principal()
    {
        if (!OperatingSystem.IsWindows()) return;

        var directory = CreateIsolatedDirectory();
        var path = Path.Combine(directory, "workflow-key.dpapi");
        var readerSid = CurrentTestSid();
        var everyoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        try
        {
            WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ProvisionNewKey(path, readerSid);
            WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ValidateProtectedKeyStorage(
                path, readerSid, readerSid);

            var fileSecurity = new FileInfo(path).GetAccessControl();
            fileSecurity.AddAccessRule(new FileSystemAccessRule(
                everyoneSid, FileSystemRights.Read, AccessControlType.Allow));
            new FileInfo(path).SetAccessControl(fileSecurity);

            Assert.Throws<InvalidOperationException>(() =>
                WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ValidateProtectedKeyStorage(
                    path, readerSid, readerSid));
        }
        finally { Cleanup(directory); }
    }

    private static SecurityIdentifier CurrentTestSid() =>
        WindowsIdentity.GetCurrent().User
        ?? throw new InvalidOperationException("The Windows test identity has no SID.");

    private static string CreateIsolatedDirectory()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "MD-Gsm", "Keys", "Persons");
        var directory = Path.Combine(root, "test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void Cleanup(string directory)
    {
        if (!Directory.Exists(directory)) return;
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var fileSecurity = new FileInfo(file).GetAccessControl();
            fileSecurity.SetAccessRuleProtection(isProtected: false, preserveInheritance: false);
            new FileInfo(file).SetAccessControl(fileSecurity);
        }
        var security = new DirectoryInfo(directory).GetAccessControl();
        security.SetAccessRuleProtection(isProtected: false, preserveInheritance: false);
        new DirectoryInfo(directory).SetAccessControl(security);
        Directory.Delete(directory, recursive: true);
    }
}
