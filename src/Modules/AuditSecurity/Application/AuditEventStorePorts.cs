using MDGsm.AuditSecurity.Domain;

namespace MDGsm.AuditSecurity.Application;

/// <summary>
/// Transactional persistence boundary. BeginAppendTransactionAsync binds a transaction to one
/// stream. LockStreamHeadAsync must lock that stream-head row FOR UPDATE and obtain the database
/// clock_timestamp() in the same transaction. InsertEventAsync and AdvanceStreamHeadAsync must
/// commit atomically. Dispose without Commit must roll back.
/// </summary>
public interface IAuditEventStore
{
    Task<IAuditAppendTransaction> BeginAppendTransactionAsync(
        Guid auditStreamId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns events and stream head from one consistent, read-only database snapshot.
    /// </summary>
    Task<AuditStreamSnapshot> ReadSnapshotAsync(
        Guid auditStreamId,
        CancellationToken cancellationToken);
}

public interface IAuditAppendTransaction : IAsyncDisposable
{
    Task<AuditAppendState> LockStreamHeadAsync(CancellationToken cancellationToken);
    Task<AuditEventRecord?> FindByEventIdAsync(Guid eventId, CancellationToken cancellationToken);
    Task InsertEventAsync(AuditEventRecord record, CancellationToken cancellationToken);
    Task AdvanceStreamHeadAsync(
        Guid auditStreamId,
        long newSequence,
        ReadOnlyMemory<byte> newHeadHash,
        CancellationToken cancellationToken);
    Task CommitAsync(CancellationToken cancellationToken);
}

public sealed record AuditAppendState(AuditStreamHead Head, DateTimeOffset DatabaseRecordedAtUtc);
public sealed record AuditStreamSnapshot(AuditStreamHead Head, IReadOnlyList<AuditEventRecord> Events);

public interface IAuditEventMetadataPolicy
{
    /// <summary>
    /// Approves event-type-specific metadata. Unknown event types and unapproved keys/values
    /// must fail closed. Credentials, password material, tokens, OTPs, recovery codes and
    /// unnecessary raw PII are always prohibited.
    /// </summary>
    bool IsApproved(AuditEventIntent intent, out string reasonCode);
}

public sealed class EmptyOnlyAuditEventMetadataPolicy : IAuditEventMetadataPolicy
{
    public bool IsApproved(AuditEventIntent intent, out string reasonCode)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var allowed = intent.Metadata.Count == 0;
        reasonCode = allowed ? "empty_metadata_allowed" : "metadata_allowlist_not_configured";
        return allowed;
    }
}

public sealed class AuditEventConflictException(string message) : InvalidOperationException(message);

public sealed class AuditEventMetadataRejectedException(string reasonCode)
    : InvalidOperationException("Audit event metadata was rejected: " + reasonCode)
{
    public string ReasonCode { get; } = reasonCode;
}

public sealed class AuditCheckpointAnchorReceipt(
    string immutableObjectReference,
    bool immutableRetentionConfirmed)
{
    public string ImmutableObjectReference { get; } = immutableObjectReference;
    public bool ImmutableRetentionConfirmed { get; } = immutableRetentionConfirmed;
}

public interface IAuditCheckpointSigner
{
    /// <summary>The active key identifier selected by the platform signer at signing time.</summary>
    string ActiveKeyId { get; }

    Task<AuditCheckpointEnvelope> SignAsync(
        AuditCheckpointPayload payload,
        CancellationToken cancellationToken);
}

public interface IAuditCheckpointPublicKeyResolver
{
    Task<ReadOnlyMemory<byte>?> ResolveTrustedSubjectPublicKeyInfoAsync(
        string keyId,
        CancellationToken cancellationToken);
}

public interface IImmutableAuditCheckpointAnchor
{
    Task<AuditCheckpointEnvelope?> ReadLatestAsync(
        Guid auditStreamId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Must be write-once in an independently administered immutable-retention domain.
    /// A database or local-file implementation is not an acceptable production anchor.
    /// </summary>
    Task<AuditCheckpointAnchorReceipt> WriteImmutableAsync(
        AuditCheckpointEnvelope envelope,
        string? expectedPreviousReference,
        CancellationToken cancellationToken);
}
