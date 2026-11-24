using System.Security.Cryptography;
using MDGsm.AuditSecurity.Domain;

namespace MDGsm.AuditSecurity.Application;

/// <summary>
/// Publishes a checkpoint only after verifying a consistent local chain snapshot and validating
/// the previously anchored checkpoint. Signing keys and immutable storage remain provider ports.
/// </summary>
public sealed class AuditCheckpointPublisher(
    IAuditEventStore store,
    IAuditCheckpointSigner signer,
    IAuditCheckpointPublicKeyResolver keyResolver,
    IImmutableAuditCheckpointAnchor anchor,
    TimeProvider timeProvider)
{
    public async Task<AuditCheckpointAnchorReceipt> PublishAsync(
        Guid auditStreamId,
        CancellationToken cancellationToken)
    {
        var snapshot = await store.ReadSnapshotAsync(auditStreamId, cancellationToken);
        var verification = AuditHashChainVerifier.Verify(auditStreamId, snapshot.Events, snapshot.Head);
        if (!verification.IsValid)
            throw new InvalidOperationException("Audit stream verification failed: " + verification.ReasonCode + ".");

        var previous = await anchor.ReadLatestAsync(auditStreamId, cancellationToken);
        string? previousReference = null;
        if (previous is not null)
        {
            if (previous.Payload.AuditStreamId != auditStreamId)
                throw new InvalidOperationException("The external checkpoint anchor returned another stream.");

            var previousKey = await keyResolver.ResolveTrustedSubjectPublicKeyInfoAsync(
                previous.Payload.KeyId, cancellationToken);
            if (previousKey is null || !AuditCheckpointVerifier.Verify(
                previous, previous.Payload.KeyId, previousKey.Value.Span))
                throw new InvalidOperationException("The latest external checkpoint is not trusted.");

            if (previous.Payload.LastSequence > snapshot.Head.LastSequence)
                throw new InvalidOperationException("The external checkpoint is ahead of the local stream.");

            var anchoredHash = Convert.FromHexString(previous.Payload.HeadHashHex);
            if (previous.Payload.LastSequence == 0)
            {
                if (!CryptographicOperations.FixedTimeEquals(anchoredHash, AuditEventCanonicalizer.GenesisHash))
                    throw new InvalidOperationException("The external genesis checkpoint does not match the canonical genesis hash.");
            }
            else
            {
                var matchingEvent = snapshot.Events.SingleOrDefault(
                    item => item.Sequence == previous.Payload.LastSequence);
                if (matchingEvent is null
                    || !CryptographicOperations.FixedTimeEquals(matchingEvent.EventHash.Span, anchoredHash))
                    throw new InvalidOperationException("The local stream does not descend from the latest external checkpoint.");
            }

            previousReference = AuditCheckpointCanonicalizer.ComputeReference(previous);
        }

        var payload = new AuditCheckpointPayload(
            auditStreamId,
            snapshot.Head.LastSequence,
            Convert.ToHexString(snapshot.Head.HeadHash.Span),
            signer.ActiveKeyId,
            AuditEventCanonicalizer.NormalizeTimestamp(timeProvider.GetUtcNow()),
            1,
            1,
            "SHA-256",
            previousReference);

        var envelope = await signer.SignAsync(payload, cancellationToken);
        if (!AuditCheckpointCanonicalizer.Serialize(envelope.Payload)
            .AsSpan().SequenceEqual(AuditCheckpointCanonicalizer.Serialize(payload)))
            throw new InvalidOperationException("The checkpoint signer changed the canonical checkpoint payload.");

        var trustedKey = await keyResolver.ResolveTrustedSubjectPublicKeyInfoAsync(
            envelope.Payload.KeyId, cancellationToken);
        if (trustedKey is null || !AuditCheckpointVerifier.Verify(
            envelope, envelope.Payload.KeyId, trustedKey.Value.Span))
            throw new InvalidOperationException("The signed checkpoint could not be verified against a trusted key.");

        // The anchor must perform conditional append against the exact reference we read.
        // This prevents two concurrent publishers from creating competing checkpoint branches.
        var receipt = await anchor.WriteImmutableAsync(envelope, previousReference, cancellationToken);
        var expectedReference = AuditCheckpointCanonicalizer.ComputeReference(envelope);
        if (!receipt.ImmutableRetentionConfirmed
            || !string.Equals(receipt.ImmutableObjectReference, expectedReference, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The external anchor did not confirm the expected immutable checkpoint reference.");

        return receipt;
    }
}
