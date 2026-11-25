using System.Security.Cryptography;
using MDGsm.Application.Persons;
using MDGsm.Infrastructure.Persons;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

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
            WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ProvisionNewKey(path);
            var before = await File.ReadAllBytesAsync(path);
            var provider = new WindowsDpapiTemporaryWorkflowEncryptionKeyProvider(path);
            var key = await provider.GetKeyAsync(
                WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.KeyVersion, CancellationToken.None);
            try { Assert.Equal(32, key.Length); }
            finally { CryptographicOperations.ZeroMemory(key); }

            Assert.ThrowsAny<Exception>(() =>
                WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ProvisionNewKey(path));
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
            WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ProvisionNewKey(firstPath);
            WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ProvisionNewKey(secondPath);

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
            WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ProvisionNewKey(path);
            var bytes = await File.ReadAllBytesAsync(path);
            bytes[^1] ^= 0x01;
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
