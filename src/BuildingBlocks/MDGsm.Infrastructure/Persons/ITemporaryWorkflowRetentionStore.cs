namespace MDGsm.Infrastructure.Persons;

/// <summary>Minimal persistence contract for deterministic retention-worker tests.</summary>
public interface ITemporaryWorkflowRetentionStore
{
    Task<int> PurgeExpiredAsync(int batchSize, CancellationToken cancellationToken = default);
}
