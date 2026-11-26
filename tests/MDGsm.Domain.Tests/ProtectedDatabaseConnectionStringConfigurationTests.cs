using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using MDGsm.Infrastructure.Persistence;
using Xunit;

namespace MDGsm.Domain.Tests;

public sealed class ProtectedDatabaseConnectionStringConfigurationTests
{
    [Fact]
    public void Protected_connection_string_is_loaded_from_machine_dpapi_file()
    {
        if (!OperatingSystem.IsWindows()) return;

        var path = Path.Combine(Path.GetTempPath(), "mdgsm-db-" + Guid.NewGuid().ToString("N") + ".dpapi");
        const string connectionString =
            "Host=127.0.0.1;Port=5432;Database=mdgsm;Username=mdgsm_api_runtime;Password=unit-test-password-with-sufficient-length;Timeout=15;Command Timeout=30;Pooling=true";
        var plaintext = Encoding.UTF8.GetBytes(connectionString);
        var protectedBytes = ProtectedData.Protect(
            plaintext, null, DataProtectionScope.LocalMachine);
        try
        {
            File.WriteAllBytes(path, protectedBytes);
            var configuration = new ConfigurationBuilder()
                .AddProtectedDatabaseConnectionString(path)
                .Build();

            Assert.Equal(connectionString,
                configuration.GetConnectionString(PersistenceRegistration.ConnectionStringName));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(protectedBytes);
            File.Delete(path);
        }
    }

    [Fact]
    public void Protected_provider_rejects_a_non_product_database_identity()
    {
        if (!OperatingSystem.IsWindows()) return;

        var path = Path.Combine(Path.GetTempPath(), "mdgsm-db-" + Guid.NewGuid().ToString("N") + ".dpapi");
        var plaintext = Encoding.UTF8.GetBytes(
            "Host=remote.example;Port=5432;Database=other;Username=postgres;Password=not-a-product-runtime;Timeout=15;Command Timeout=30;Pooling=true");
        var protectedBytes = ProtectedData.Protect(
            plaintext, null, DataProtectionScope.LocalMachine);
        try
        {
            File.WriteAllBytes(path, protectedBytes);
            var configuration = new ConfigurationBuilder()
                .AddProtectedDatabaseConnectionString(path)
                .Build();

            Assert.Throws<InvalidOperationException>(() =>
                configuration.GetConnectionString(PersistenceRegistration.ConnectionStringName));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(protectedBytes);
            File.Delete(path);
        }
    }

    [Fact]
    public void Protected_provider_fails_when_secret_file_is_missing()
    {
        if (!OperatingSystem.IsWindows()) return;

        var path = Path.Combine(Path.GetTempPath(), "mdgsm-missing-" + Guid.NewGuid().ToString("N") + ".dpapi");
        var configuration = new ConfigurationBuilder()
            .AddProtectedDatabaseConnectionString(path)
            .Build();

        Assert.Throws<InvalidOperationException>(() =>
            configuration.GetConnectionString(PersistenceRegistration.ConnectionStringName));
    }
}
