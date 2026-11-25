using MDGsm.Persons.Application;
using MDGsm.Persons.Infrastructure;
using MDGsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace MDGsm.Domain.Tests;

public sealed class PersistenceRegistrationTests
{
    [Fact]
    public void Does_not_register_database_context_when_connection_is_not_configured()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        services.AddMdGsmPersistence(configuration);

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(MdGsmDbContext));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(DbContextOptions<MdGsmDbContext>));
    }

    [Fact]
    public void Windows_key_configuration_registers_provider_and_startup_validation_without_database()
    {
        if (!OperatingSystem.IsWindows()) return;

        var keyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "MD-Gsm", "Keys", "Persons", "MDGsmApi", "workflow-key.dpapi");
        var values = new Dictionary<string, string?>
        {
            [WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ConfigurationKey] = keyPath
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();

        services.AddMdGsmPersistence(configuration);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ITemporaryWorkflowEncryptionKeyProvider));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(TemporaryWorkflowKeyStartupValidator));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(MdGsmDbContext));
    }

    [Fact]
    public void Registers_database_context_when_connection_is_explicitly_configured()
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:MDGsmDatabase"] = "Host=127.0.0.1;Port=5432;Database=mdgsm_stage6_test;Username=mdgsm_test"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();

        services.AddMdGsmPersistence(configuration);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(MdGsmDbContext));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(MDGsm.Persons.Application.IPartySensitiveDataAuthorizer));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(MDGsm.IdentityAccess.Application.ISessionIdentityResolver));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(PostgresTemporaryWorkflowStore) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(ITemporaryWorkflowRetentionStore) &&
            descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(TemporaryWorkflowRetentionWorker));
    }
}
