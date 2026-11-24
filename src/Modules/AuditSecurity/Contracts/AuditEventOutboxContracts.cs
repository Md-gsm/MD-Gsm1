using System.Data.Common;

namespace MDGsm.AuditSecurity.Contracts;

public static class AuditEventOutboxWorkerIdentity
{
    public const string LogicalIdentity = "audit-delivery-worker";
    public const string PostgreSqlLogin = "audit_delivery_worker";
    public const string PostgreSqlAclRole = "mdgsm_audit_delivery_worker";
}

public sealed record AuditEventOutboxLease(
    Guid EventId,
    Guid LeaseToken,
    AuditEventSubmission Submission,
    string IntentDigestHex,
    int AttemptCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LeaseExpiresAtUtc);

/// <summary>
/// The source module calls this writer inside the same connection and transaction that persists
/// its own domain fact. The implementation must never open or commit a second transaction.
/// </summary>
public interface IAuditEventOutboxWriter
{
    Task EnqueueAsync(
        DbConnection sourceConnection,
        DbTransaction sourceTransaction,
        AuditEventSubmission submission,
        CancellationToken cancellationToken);
}

/// <summary>
/// Source-module-local delivery metadata port. Each instance addresses exactly one source schema.
/// It cannot read or write another module's business data or AuditSecurity's canonical ledger.
/// </summary>
public interface IAuditEventOutboxDeliveryStore
{
    Task<IReadOnlyList<AuditEventOutboxLease>> ClaimBatchAsync(
        string workerIdentity,
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task MarkAcknowledgedAsync(
        AuditEventOutboxLease lease,
        AuditAppendReceipt receipt,
        CancellationToken cancellationToken);

    Task ScheduleRetryAsync(
        AuditEventOutboxLease lease,
        string failureCode,
        TimeSpan delay,
        CancellationToken cancellationToken);

    Task QuarantineAsync(
        AuditEventOutboxLease lease,
        string reasonCode,
        CancellationToken cancellationToken);
}

public sealed class AuditEventOutboxConflictException(string message) : InvalidOperationException(message);
public sealed class AuditEventOutboxLeaseLostException(string message) : InvalidOperationException(message);

public sealed class AuditEventTransientDeliveryException(string reasonCode, Exception? innerException = null)
    : Exception("Audit event delivery failed transiently: " + reasonCode, innerException)
{
    public string ReasonCode { get; } = AuditEventOutboxReasonCode.Validate(reasonCode);
}

public sealed class AuditEventDeliveryRejectedException(string reasonCode)
    : Exception("Audit event delivery was rejected: " + reasonCode)
{
    public string ReasonCode { get; } = AuditEventOutboxReasonCode.Validate(reasonCode);
}

public static class AuditEventOutboxReasonCode
{
    public static string Validate(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 64 || value.Any(character =>
            !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_')))
            throw new ArgumentException("Outbox reason codes must be lowercase ASCII identifiers.", nameof(value));
        return value;
    }
}
