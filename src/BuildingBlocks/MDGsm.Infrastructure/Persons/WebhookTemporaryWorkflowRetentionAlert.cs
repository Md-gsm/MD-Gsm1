using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MDGsm.Infrastructure.Persons;

/// <summary>
/// Delivers a minimal, PII-free operational alert to the configured HTTPS webhook.
/// The endpoint is deployment configuration, never source-controlled.
/// </summary>
public sealed class WebhookTemporaryWorkflowRetentionAlert(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<WebhookTemporaryWorkflowRetentionAlert> logger,
    TimeProvider timeProvider) : ITemporaryWorkflowRetentionAlert
{
    public const string WebhookConfigurationKey = "Operations:Alerts:WebhookUrl";

    public async Task NotifyFailureAsync(string failureCode, CancellationToken cancellationToken)
    {
        var configured = configuration[WebhookConfigurationKey];
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps)
        {
            logger.LogCritical("Temporary workflow retention alert destination is missing or is not HTTPS. ConfigurationKey={ConfigurationKey}",
                WebhookConfigurationKey);
            throw new InvalidOperationException("A valid HTTPS operational alert destination is required.");
        }

        using var response = await httpClientFactory.CreateClient(nameof(WebhookTemporaryWorkflowRetentionAlert))
            .PostAsJsonAsync(endpoint, new
            {
                eventType = "persons.temporary_workflow_retention.failed",
                failureCode,
                occurredAtUtc = timeProvider.GetUtcNow()
            }, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogCritical("Temporary workflow retention alert endpoint rejected delivery. HttpStatusCode={HttpStatusCode}",
                (int)response.StatusCode);
            throw new HttpRequestException("Operational alert endpoint rejected the notification.");
        }

        logger.LogWarning("Temporary workflow retention alert delivered. FailureCode={FailureCode}", failureCode);
    }
}
