using MDGsm.Persons.Application;
using MDGsm.Persons.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace MDGsm.Persons.Infrastructure;

public static class PersonsPersistenceRegistration
{
    public static IServiceCollection AddPersonsPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var workflowKeyPath = configuration[WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ConfigurationKey];
        if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(workflowKeyPath))
        {
            if (!services.Any(descriptor => descriptor.ServiceType == typeof(WindowsDpapiTemporaryWorkflowEncryptionKeyProvider)))
            {
                services.AddSingleton(new WindowsDpapiTemporaryWorkflowEncryptionKeyProvider(workflowKeyPath));
            }
            if (!services.Any(descriptor => descriptor.ServiceType == typeof(ITemporaryWorkflowEncryptionKeyProvider)))
            {
                services.AddSingleton<ITemporaryWorkflowEncryptionKeyProvider>(provider =>
                    provider.GetRequiredService<WindowsDpapiTemporaryWorkflowEncryptionKeyProvider>());
            }
            if (!services.Any(descriptor => descriptor.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService) &&
                                            descriptor.ImplementationType == typeof(TemporaryWorkflowKeyStartupValidator)))
            {
                services.AddHostedService<TemporaryWorkflowKeyStartupValidator>();
            }
        }

        // Do not register runtime Persons stores or workers until the shared Host
        // has explicitly configured the canonical PostgreSQL data source.
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(NpgsqlDataSource)))
            return services;

        services.AddScoped<IPartyRepository, PostgresPartyRepository>();
        services.AddScoped<IPartyReferenceValidator, PostgresPartyReferenceValidator>();
        services.AddSingleton<PostgresTemporaryWorkflowStore>();
        services.AddSingleton<ITemporaryWorkflowRetentionStore>(provider =>
            provider.GetRequiredService<PostgresTemporaryWorkflowStore>());
        services.AddSingleton<ITemporaryWorkflowRetentionAlert, StructuredLogTemporaryWorkflowRetentionAlert>();
        services.AddHostedService<TemporaryWorkflowRetentionWorker>();
        services.AddScoped<IPartyMergeService, PostgresPartyMergeService>();

        if (services.Any(descriptor => descriptor.ServiceType == typeof(ITemporaryWorkflowEncryptionKeyProvider)))
        {
            services.AddScoped<ITemporaryWorkflowPayloadProtector, AesGcmTemporaryWorkflowPayloadProtector>();
            services.AddScoped<ITemporaryWorkflowPromotionService, PostgresTemporaryWorkflowPromotionService>();
        }

        return services;
    }
}
