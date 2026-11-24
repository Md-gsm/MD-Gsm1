using System;
using System.Threading;
using System.Threading.Tasks;
using MDGsm.IdentityAccess.Application;
using MDGsm.IdentityAccess.Contracts;
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
    public async Task Public_authorization_contract_is_registered_and_fails_closed_without_database()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddScoped<IValidatedRequestPrincipalAccessor>(_ => new TestPrincipal());
        services.AddMdGsmPersistence(configuration);

        await using var provider = services.BuildServiceProvider();
        var contract = provider.GetRequiredService<IIdentityAccessAuthorizationContract>();

        var permission = await contract.EvaluateCurrentPermissionAsync("reports.read", CancellationToken.None);
        var owner = await contract.ResolveCurrentProductOwnerAuthorityAsync(CancellationToken.None);

        Assert.Equal(PermissionDecisionStatus.Unavailable, permission.Status);
        Assert.False(permission.IsAllowed);
        Assert.Equal(ProductOwnerAuthorityStatus.Unavailable, owner.Status);
        Assert.False(owner.IsBoundOwner);
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
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(MDGsm.Application.Persons.IPartySensitiveDataAuthorizer));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(MDGsm.IdentityAccess.Application.ISessionIdentityResolver));
    }

    private sealed class TestPrincipal : IValidatedRequestPrincipalAccessor
    {
        public Guid AccountId => Guid.Parse("60000000-0000-7000-8000-000000000001");
        public Guid SessionId => Guid.Parse("60000000-0000-7000-8000-000000000002");
        public long CredentialVersion => 1;
        public DateTimeOffset AuthenticatedAtUtc => DateTimeOffset.UnixEpoch;
    }
}
