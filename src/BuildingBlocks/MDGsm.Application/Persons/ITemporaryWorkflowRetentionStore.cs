namespace MDGsm.Application.Persons;

/// <summary>Application-owned port for idempotent expiry and ciphertext-purge sweeps.</summary>
public interface ITemporaryWorkflowRetentionStore
{
    Task<int> PurgeExpiredAsync(
        DateTimeOffset nowUtc,
        int batchSize = 250,
        CancellationToken cancellationToken = default);
}
