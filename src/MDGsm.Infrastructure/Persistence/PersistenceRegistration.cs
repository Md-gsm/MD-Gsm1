using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}
