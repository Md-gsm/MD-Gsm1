using Microsoft.Extensions.Logging;

namespace MDGsm.Infrastructure.Persons;

/// <summary>
/// Emits a PII-free critical structured event. Production hosting must route this event to
/// an alert destination with the approved five-minute delivery SLA.
/// </summary>
public sealed class StructuredLogTemporaryWorkflowRetentionAlert(
    ILogger<StructuredLogTemporaryWorkflowRetentionAlert> logger) : ITemporaryWorkflowRetentionAlert
{
    public Task NotifyFailureAsync(
        TemporaryWorkflowRetentionFailureCode failureCode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(failureCode))
            throw new ArgumentOutOfRangeException(nameof(failureCode));

        logger.LogCritical(
            "SECURITY_ALERT Temporary workflow cleanup failed. FailureCode={FailureCode}",
            failureCode);
        return Task.CompletedTask;
    }
}
