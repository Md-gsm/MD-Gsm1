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
        var safeCode = IsSafeCode(failureCode) ? failureCode : "invalid_failure_code";
        logger.LogCritical("SECURITY_ALERT Temporary workflow cleanup failed. FailureCode={FailureCode}", safeCode);
        return Task.CompletedTask;
    }

    private static bool IsSafeCode(string? value) =>
        !string.IsNullOrEmpty(value) &&
        value.Length <= 80 &&
        value.All(character =>
            character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-' or ':');
}
