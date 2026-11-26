using MDGsm.Persons.Application;
using MDGsm.Persons.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace MDGsm.Persons.Infrastructure;

public static class PersonsInfrastructureRegistration
{
    public static IServiceCollection AddPersonsKeyProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (OperatingSystem.IsWindows())
            services.AddSingleton<ITemporaryWorkflowKeyProvisioner, WindowsDpapiTemporaryWorkflowKeyProvisioner>();

        var workflowKeyPath = configuration[WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ConfigurationKey];
        if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(workflowKeyPath))
        {
            services.AddSingleton(new WindowsDpapiTemporaryWorkflowEncryptionKeyProvider(workflowKeyPath));
            services.AddSingleton<ITemporaryWorkflowEncryptionKeyProvider>(
                provider => provider.GetRequiredService<WindowsDpapiTemporaryWorkflowEncryptionKeyProvider>());
            services.AddHostedService<TemporaryWorkflowKeyStartupValidator>();
        }

        return services;
    }

    /// <summary>
    /// Explicitly opts a host into the one-batch dispatcher after it has supplied
    /// an approved transport adapter. This does not register a timer or hosted worker.
    /// </summary>
    public static IServiceCollection AddPersonsOutboxDispatcher(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IPersonsOutboxPublisher)))
            throw new InvalidOperationException(
                "Register an approved IPersonsOutboxPublisher before enabling the Persons Outbox dispatcher.");
        services.AddScoped<PersonsOutboxDispatcher>();
        return services;
    }

    public static IServiceCollection AddPersonsInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IPartyRepository, PostgresPartyRepository>();
        services.AddScoped<IPersonsOutboxDeliveryStore, PostgresPersonsOutboxDeliveryStore>();
        services.AddScoped<IPartyLifecycleService, PostgresPartyLifecycleService>();
        services.AddScoped<IPartyLifecycleHistoryReader, PostgresPartyLifecycleHistoryReader>();
        services.AddScoped<IPartyMergeParticipant, PostgresPersonsPartyMergeParticipant>();
        services.AddScoped<IPersonsSearchControl, PostgresPersonsSearchControl>();
        services.AddScoped<IPartyReferenceValidator, PostgresPartyReferenceValidator>();
        services.AddScoped<IPartyMergeCoordinator, PostgresPartyMergeService>();
        services.AddSingleton<PostgresTemporaryWorkflowStore>();
        services.AddSingleton<ITemporaryWorkflowRetentionStore>(
            provider => provider.GetRequiredService<PostgresTemporaryWorkflowStore>());
        services.AddSingleton<ITemporaryWorkflowRetentionAlert, StructuredLogTemporaryWorkflowRetentionAlert>();
        services.AddHostedService<TemporaryWorkflowRetentionWorker>();

        if (services.Any(descriptor => descriptor.ServiceType == typeof(ITemporaryWorkflowEncryptionKeyProvider)))
        {
            services.AddScoped<ITemporaryWorkflowPayloadProtector, AesGcmTemporaryWorkflowPayloadProtector>();
            services.AddScoped<ITemporaryWorkflowPromotionService, PostgresTemporaryWorkflowPromotionService>();
        }

        return services;
    }
}

public sealed class PersonsInfrastructureAssemblyMarker
{
}
