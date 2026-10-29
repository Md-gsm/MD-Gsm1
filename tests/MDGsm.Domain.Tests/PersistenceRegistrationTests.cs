using MDGsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    public void Registers_database_context_when_connection_is_explicitly_configured()
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:MDGsmDatabase"] = "Host=127.0.0.1;Port=5432;Database=mdgsm_stage6_test;Username=mdgsm_test;Password=not-a-real-secret"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();

        services.AddMdGsmPersistence(configuration);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(MdGsmDbContext));
    }
}
