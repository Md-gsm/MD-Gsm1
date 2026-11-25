using System.Security.AccessControl;
using System.Security.Cryptography;
using MDGsm.Application.Persons;
using MDGsm.Infrastructure.Persons;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class WindowsDpapiTemporaryWorkflowKeyProviderTests
{
    [Fact]
    public async Task Explicit_provision_round_trips_localmachine_key_and_never_overwrites_existing_file()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var directory = CreateIsolatedDirectory();
        var path = Path.Combine(directory, "workflow-key.dpapi");
        try
        {
            WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ProvisionNewKey(path);
            var originalFile = File.ReadAllBytes(path);
            var provider = new WindowsDpapiTemporaryWorkflowEncryptionKeyProvider(path);
            var key = await provider.GetKeyAsync(
                WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.KeyVersion, CancellationToken.None);
            try { Assert.Equal(32, key.Length); }
            finally { CryptographicOperations.ZeroMemory(key); }

            Assert.ThrowsAny<Exception>(() =>
                WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ProvisionNewKey(path));
            Assert.Equal(originalFile, File.ReadAllBytes(path));
            CryptographicOperations.ZeroMemory(originalFile);
        }
        finally
        {
            RestoreInheritedDirectoryAclAndDelete(directory);
        }
    }

    [Fact]
    public async Task Missing_key_fails_closed_without_creating_a_replacement()
    {
        if (!OperatingSystem.IsWindows())
            return;

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
        finally
        {
            RestoreInheritedDirectoryAclAndDelete(directory);
        }
    }

    [Fact]
    public void Key_path_outside_approved_programdata_root_is_rejected()
    {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.Throws<InvalidOperationException>(() =>
            new WindowsDpapiTemporaryWorkflowEncryptionKeyProvider(Path.GetTempFileName()));
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

    private static void RestoreInheritedDirectoryAclAndDelete(string directory)
    {
        if (!Directory.Exists(directory))
            return;
        var security = new DirectoryInfo(directory).GetAccessControl();
        security.SetAccessRuleProtection(isProtected: false, preserveInheritance: false);
        new DirectoryInfo(directory).SetAccessControl(security);
        Directory.Delete(directory, recursive: true);
    }
}
