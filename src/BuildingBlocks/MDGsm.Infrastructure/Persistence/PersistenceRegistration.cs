using MDGsm.Persons.Application;
using MDGsm.Persons.Infrastructure;
using MDGsm.IdentityAccess.Application;
using MDGsm.Infrastructure.IdentityAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace MDGsm.Infrastructure.Persistence;

public static class PersistenceRegistration
{
    public const string ConnectionStringName = "MDGsmDatabase";

    public static IServiceCollection AddMdGsmPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddPersonsKeyProvider(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            return services;

        services.AddDbContext<MdGsmDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 3)));
        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddPersonsInfrastructure();

        // The installer provisions an empty database by design and does not apply
        // application migrations. Do not start a table-dependent retention worker
        // until an operator explicitly enables it after the Persons schema is ready.
        if (configuration.GetValue<bool>("Persons:TemporaryWorkflowRetentionEnabled"))
            services.AddPersonsRetentionWorker();

        // IdentityAccess-owned adapters implement Persons-owned application ports.
        // Persons.Infrastructure has no dependency on IdentityAccess.
        services.AddScoped<IPartySensitiveDataAuthorizer, IdentityAccessPartySensitiveDataAuthorizer>();
        services.AddScoped<IPersonsWorkflowPromotionAuthorizer, IdentityAccessPersonsWorkflowPromotionAuthorizer>();

        services.AddScoped<PostgresIdentityAccessStores>();
        services.AddScoped<IAccountStore>(provider => provider.GetRequiredService<PostgresIdentityAccessStores>());
        services.AddScoped<ISessionStore>(provider => provider.GetRequiredService<PostgresIdentityAccessStores>());
        services.AddScoped<ISessionIdentityResolver, PostgresSessionIdentityResolver>();
        services.AddScoped<ILoginAttemptStore>(provider => provider.GetRequiredService<PostgresIdentityAccessStores>());
        services.AddScoped<IRolePermissionStore>(provider => provider.GetRequiredService<PostgresIdentityAccessStores>());
        services.AddScoped<IAuditWriter>(provider => provider.GetRequiredService<PostgresIdentityAccessStores>());
        services.AddScoped<ISystemPolicyStore>(provider => provider.GetRequiredService<PostgresIdentityAccessStores>());
        services.AddScoped<IDatabaseClock>(provider => provider.GetRequiredService<PostgresIdentityAccessStores>());
        services.AddScoped<IRecoveryStore>(provider => provider.GetRequiredService<PostgresIdentityAccessStores>());
        services.AddScoped<IIdentityAccessUnitOfWork, IdentityAccessUnitOfWork>();
        return services;
    }

    public static IServiceCollection AddIdentityAccessSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var privacyDigest = new HmacPrivacyDigest(configuration);
        services.AddSingleton<IPasswordHasher, PlatformPasswordHasher>();
        services.AddSingleton<IAccessTokenFactory, OpaqueAccessTokenFactory>();
        services.AddSingleton<IPrivacyDigest>(privacyDigest);
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
