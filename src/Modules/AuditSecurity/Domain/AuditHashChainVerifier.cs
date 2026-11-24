using System.Security.Cryptography;

namespace MDGsm.AuditSecurity.Domain;

public sealed class AuditStreamHead
{
    private readonly byte[] _headHash;

    public AuditStreamHead(Guid auditStreamId, long lastSequence, ReadOnlySpan<byte> headHash)
    {
        if (auditStreamId == Guid.Empty)
            throw new ArgumentException("Audit stream ID is required.", nameof(auditStreamId));
        if (lastSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(lastSequence));
        if (headHash.Length != 32)
            throw new ArgumentException("Audit stream head must be a SHA-256 digest.", nameof(headHash));
        AuditStreamId = auditStreamId;
        LastSequence = lastSequence;
        _headHash = headHash.ToArray();
    }

    public Guid AuditStreamId { get; }
    public long LastSequence { get; }
    public ReadOnlyMemory<byte> HeadHash => _headHash;
}

public sealed record AuditChainVerificationResult(bool IsValid, long? FirstInvalidSequence, string ReasonCode);

public static class AuditHashChainVerifier
{
    public static AuditChainVerificationResult Verify(
        Guid expectedStreamId,
        IEnumerable<AuditEventRecord> records,
        AuditStreamHead storedHead)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(storedHead);

        if (expectedStreamId == Guid.Empty || storedHead.AuditStreamId != expectedStreamId)
            return Invalid(null, "stream_identity_mismatch");

        var ordered = records.OrderBy(x => x.Sequence).ToArray();
        var expectedSequence = 1L;
        var expectedPrevious = AuditEventCanonicalizer.GenesisHash;
        var eventIds = new HashSet<Guid>();

        foreach (var record in ordered)
        {
            if (record.Intent.AuditStreamId != expectedStreamId)
                return Invalid(record.Sequence, "event_stream_mismatch");
            if (record.Sequence != expectedSequence)
                return Invalid(record.Sequence, "sequence_gap_or_duplicate");
            if (!eventIds.Add(record.Intent.EventId))
                return Invalid(record.Sequence, "duplicate_event_id");
            if (!CryptographicOperations.FixedTimeEquals(record.PreviousEventHash.Span, expectedPrevious))
                return Invalid(record.Sequence, "predecessor_hash_mismatch");
            if (!AuditEventCanonicalizer.VerifyRecord(record))
                return Invalid(record.Sequence, "event_canonicalization_or_hash_mismatch");

            expectedPrevious = record.EventHash.ToArray();
            expectedSequence = checked(expectedSequence + 1);
        }

        var verifiedCount = expectedSequence - 1;
        if (storedHead.LastSequence != verifiedCount)
            return Invalid(verifiedCount + 1, "stream_head_sequence_mismatch");
        if (!CryptographicOperations.FixedTimeEquals(storedHead.HeadHash.Span, expectedPrevious))
            return Invalid(verifiedCount == 0 ? null : verifiedCount, "stream_head_hash_mismatch");

        return new AuditChainVerificationResult(true, null, "verified");
    }

    private static AuditChainVerificationResult Invalid(long? sequence, string reason) =>
        new(false, sequence, reason);
}
