using Microsoft.Extensions.Logging;

namespace MDGsm.Infrastructure.Persons;

/// <summary>
/// Emits a PII-free critical structured event. Production hosting must route this event to
/// an alert destination with the approved five-minute delivery SLA.
/// </summary>
public sealed class StructuredLogTemporaryWorkflowRetentionAlert(
    ILogger<StructuredLogTemporaryWorkflowRetentionAlert> logger) : ITemporaryWorkflowRetentionAlert
{
    public Task NotifyFailureAsync(string failureCode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogCritical("SECURITY_ALERT Temporary workflow cleanup failed. FailureCode={FailureCode}", failureCode);
        return Task.CompletedTask;
    }
}
