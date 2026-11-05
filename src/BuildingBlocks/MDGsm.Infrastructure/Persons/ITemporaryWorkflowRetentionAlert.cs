namespace MDGsm.Infrastructure.Persons;

/// <summary>Receives a short, PII-free failure code for retention sweep failures.</summary>
public interface ITemporaryWorkflowRetentionAlert
{
    Task NotifyFailureAsync(string failureCode, CancellationToken cancellationToken);
}
