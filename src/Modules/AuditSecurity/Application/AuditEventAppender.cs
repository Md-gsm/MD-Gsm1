using System.Security.Cryptography;
using MDGsm.AuditSecurity.Contracts;
using MDGsm.AuditSecurity.Domain;

namespace MDGsm.AuditSecurity.Application;

/// <summary>
/// Provider-neutral append orchestration. The persistence adapter supplies the PostgreSQL
/// transaction, row lock, database time and atomic insert/head update.
/// </summary>
public sealed class AuditEventAppender(
    IAuditEventStore store,
    IAuditEventMetadataPolicy metadataPolicy)
{
    public async Task<AuditAppendReceipt> AppendAsync(
        AuditEventIntent incoming,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(metadataPolicy);

        var intent = incoming.NormalizeAndValidate();
        if (!metadataPolicy.IsApproved(intent, out var reasonCode))
            throw new AuditEventMetadataRejectedException(reasonCode);

        var intentDigest = AuditEventCanonicalizer.ComputeIntentDigest(intent);
        var intentDigestHex = Convert.ToHexString(intentDigest);
        await using var transaction = await store.BeginAppendTransactionAsync(
            intent.AuditStreamId, cancellationToken);

        // The stream lock is acquired before idempotency lookup and sequence allocation.
        var state = await transaction.LockStreamHeadAsync(cancellationToken);
        if (state.Head.AuditStreamId != intent.AuditStreamId)
            throw new InvalidOperationException("The locked stream head does not match the requested stream.");
        if (state.Head.HeadHash.Length != 32 || state.Head.LastSequence < 0)
            throw new InvalidOperationException("The stored audit stream head is invalid.");

        // EventId is globally unique in the persistence contract, not just unique per stream.
        var existing = await transaction.FindByEventIdAsync(intent.EventId, cancellationToken);
        if (existing is not null)
        {
            if (CryptographicOperations.FixedTimeEquals(existing.IntentDigest.Span, intentDigest))
            {
                return new AuditAppendReceipt(
                    existing.Intent.EventId,
                    existing.Intent.AuditStreamId,
                    existing.Sequence,
                    Convert.ToHexString(existing.EventHash.Span),
                    true,
                    ModuleContractVersion.Current,
                    intentDigestHex);
            }

            throw new AuditEventConflictException(
                "EventId was already committed with a different canonical intent.");
        }

        var nextSequence = checked(state.Head.LastSequence + 1);
        var record = AuditEventCanonicalizer.CreateRecord(
            intent, nextSequence, state.DatabaseRecordedAtUtc, state.Head.HeadHash.Span);

        await transaction.InsertEventAsync(record, cancellationToken);
        await transaction.AdvanceStreamHeadAsync(
            intent.AuditStreamId, nextSequence, record.EventHash, cancellationToken);

        // A transport failure during Commit may have an unknown outcome. The caller retries the
        // same EventId and identical intent; the unique EventId lookup resolves the original row.
        await transaction.CommitAsync(cancellationToken);

        return new AuditAppendReceipt(
            intent.EventId,
            intent.AuditStreamId,
            record.Sequence,
            Convert.ToHexString(record.EventHash.Span),
            false,
            ModuleContractVersion.Current,
            intentDigestHex);
    }
}

public sealed class AuditEventIngestionContractAdapter(AuditEventAppender appender)
    : IAuditEventIngestionContract
{
    public Task<AuditAppendReceipt> AppendAsync(
        AuditEventSubmission submission,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);
        if (!string.Equals(submission.ContractVersion, ModuleContractVersion.Current, StringComparison.Ordinal))
            throw new NotSupportedException("Unsupported AuditSecurity ingestion contract version.");

        var normalized = AuditEventSubmissionCanonicalizer.NormalizeAndValidate(submission);
        var intent = new AuditEventIntent(
            normalized.EventId,
            normalized.AuditStreamId,
            normalized.SchemaVersion,
            normalized.CanonicalizationVersion,
            normalized.HashAlgorithmCode,
            normalized.EventTypeCode,
            normalized.OccurredAtUtc,
            normalized.ActorTypeCode,
            normalized.ActorId,
            normalized.SubjectTypeCode,
            normalized.SubjectId,
            normalized.SourceModuleCode,
            normalized.SourceEventId,
            normalized.CorrelationId,
            normalized.CausationId,
            normalized.OutcomeCode,
            normalized.Metadata);

        return appender.AppendAsync(intent, cancellationToken);
    }
}

public sealed class AuditChainVerificationService(IAuditEventStore store)
{
    public async Task<AuditChainVerificationResult> VerifyAsync(
        Guid auditStreamId,
        CancellationToken cancellationToken)
    {
        var snapshot = await store.ReadSnapshotAsync(auditStreamId, cancellationToken);
        return AuditHashChainVerifier.Verify(auditStreamId, snapshot.Events, snapshot.Head);
    }
}
