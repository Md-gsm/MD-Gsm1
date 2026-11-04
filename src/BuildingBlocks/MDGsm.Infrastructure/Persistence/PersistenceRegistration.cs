using MDGsm.Application.Persons;
using MDGsm.Infrastructure.Persons;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace MDGsm.Infrastructure.Persistence;

public static class PersistenceRegistration
{
    public const string ConnectionStringName = "MDGsmDatabase";

    /// <summary>Registers PostgreSQL persistence only when an explicit connection string is configured.</summary>
    public static IServiceCollection AddMdGsmPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return services;
        }

        services.AddDbContext<MdGsmDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.EnableRetryOnFailure(maxRetryCount: 3)));

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddScoped<IPartyRepository, PostgresPartyRepository>();
        return services;
    }

    /// <summary>Registers cryptographic primitives; it does not enable authentication endpoints.</summary>
    public static IServiceCollection AddIdentityAccessSecurity(this IServiceCollection services)
    {
        services.AddSingleton<MDGsm.IdentityAccess.Application.IPasswordHasher, IdentityAccess.PlatformPasswordHasher>();
        services.AddSingleton<MDGsm.IdentityAccess.Application.IAccessTokenFactory, IdentityAccess.OpaqueAccessTokenFactory>();
        services.AddSingleton<MDGsm.IdentityAccess.Application.IPrivacyDigest, IdentityAccess.OpaqueAccessTokenFactory>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
