namespace MDGsm.AuditSecurity.Contracts;

/// <summary>
/// Published source-module-neutral audit intent. A source module must first persist its
/// immutable intent in its own transactionally coupled Outbox; delivery is at-least-once.
/// This contract is not a cross-database transaction or a source-module data API.
/// </summary>
public sealed record AuditEventSubmission(
    Guid EventId,
    Guid AuditStreamId,
    short SchemaVersion,
    short CanonicalizationVersion,
    string HashAlgorithmCode,
    string EventTypeCode,
    DateTimeOffset OccurredAtUtc,
    string ActorTypeCode,
    Guid ActorId,
    string? SubjectTypeCode,
    Guid? SubjectId,
    string SourceModuleCode,
    Guid SourceEventId,
    Guid CorrelationId,
    Guid? CausationId,
    string OutcomeCode,
    IReadOnlyDictionary<string, string> Metadata,
    string ContractVersion);

/// <summary>
/// Durable ingestion receipt. IntentDigestHex binds the acknowledgement to the exact
/// canonical source intent; it is not, by itself, a cryptographic signature.
/// The ingestion channel must authenticate the AuditSecurity service endpoint.
/// </summary>
public sealed record AuditAppendReceipt(
    Guid EventId,
    Guid AuditStreamId,
    long Sequence,
    string EventHashHex,
    bool Duplicate,
    string ContractVersion,
    string IntentDigestHex = "");

public interface IAuditEventIngestionContract
{
    Task<AuditAppendReceipt> AppendAsync(
        AuditEventSubmission submission,
        CancellationToken cancellationToken);
}
