namespace MDGsm.AuditSecurity.Domain;

public sealed class AuditEventRecord
{
    private readonly byte[] _intentDigest;
    private readonly byte[] _previousEventHash;
    private readonly byte[] _canonicalPayload;
    private readonly byte[] _eventHash;

    internal AuditEventRecord(
        AuditEventIntent intent,
        long sequence,
        DateTimeOffset recordedAtUtc,
        byte[] intentDigest,
        byte[] previousEventHash,
        byte[] canonicalPayload,
        byte[] eventHash)
    {
        Intent = intent.NormalizeAndValidate();
        Sequence = sequence;
        RecordedAtUtc = AuditEventCanonicalizer.NormalizeTimestamp(recordedAtUtc);
        _intentDigest = intentDigest.ToArray();
        _previousEventHash = previousEventHash.ToArray();
        _canonicalPayload = canonicalPayload.ToArray();
        _eventHash = eventHash.ToArray();
    }

    public AuditEventIntent Intent { get; }
    public long Sequence { get; }
    public DateTimeOffset RecordedAtUtc { get; }
    public ReadOnlyMemory<byte> IntentDigest => _intentDigest;
    public ReadOnlyMemory<byte> PreviousEventHash => _previousEventHash;
    public ReadOnlyMemory<byte> CanonicalPayload => _canonicalPayload;
    public ReadOnlyMemory<byte> EventHash => _eventHash;

    public static AuditEventRecord Rehydrate(
        AuditEventIntent intent,
        long sequence,
        DateTimeOffset recordedAtUtc,
        ReadOnlySpan<byte> intentDigest,
        ReadOnlySpan<byte> previousEventHash,
        ReadOnlySpan<byte> canonicalPayload,
        ReadOnlySpan<byte> eventHash) =>
        new(intent, sequence, recordedAtUtc, intentDigest.ToArray(), previousEventHash.ToArray(),
            canonicalPayload.ToArray(), eventHash.ToArray());
}
