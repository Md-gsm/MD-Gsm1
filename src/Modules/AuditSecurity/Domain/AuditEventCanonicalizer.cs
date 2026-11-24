using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MDGsm.AuditSecurity.Domain;

public static class AuditEventCanonicalizer
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly byte[] GenesisHashValue =
        SHA256.HashData(StrictUtf8.GetBytes("MDGSM-AUDIT-GENESIS-V1"));

    public static byte[] GenesisHash => GenesisHashValue.ToArray();

    public static byte[] ComputeIntentDigest(AuditEventIntent incoming) =>
        SHA256.HashData(SerializeIntent(incoming.NormalizeAndValidate()));

    public static byte[] SerializeCanonicalIntent(AuditEventIntent incoming) =>
        SerializeIntent(incoming.NormalizeAndValidate());

    public static AuditEventRecord CreateRecord(
        AuditEventIntent incoming,
        long sequence,
        DateTimeOffset databaseRecordedAtUtc,
        ReadOnlySpan<byte> previousEventHash)
    {
        var intent = incoming.NormalizeAndValidate();
        if (sequence < 1)
            throw new ArgumentOutOfRangeException(nameof(sequence));
        if (previousEventHash.Length != 32)
            throw new ArgumentException("Previous event hash must be exactly 32 bytes.", nameof(previousEventHash));

        var recordedAt = NormalizeTimestamp(databaseRecordedAtUtc);
        var intentDigest = SHA256.HashData(SerializeIntent(intent));
        var previousHash = previousEventHash.ToArray();
        var payload = SerializeEvent(intent, sequence, recordedAt, previousHash);
        var eventHash = SHA256.HashData(payload);
        return new AuditEventRecord(intent, sequence, recordedAt, intentDigest, previousHash, payload, eventHash);
    }

    public static bool VerifyRecord(AuditEventRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        try
        {
            var intent = record.Intent.NormalizeAndValidate();
            if (record.Sequence < 1 || record.IntentDigest.Length != 32
                || record.PreviousEventHash.Length != 32 || record.EventHash.Length != 32)
                return false;

            var digest = SHA256.HashData(SerializeIntent(intent));
            var payload = SerializeEvent(intent, record.Sequence,
                NormalizeTimestamp(record.RecordedAtUtc), record.PreviousEventHash.Span);
            var eventHash = SHA256.HashData(payload);

            return CryptographicOperations.FixedTimeEquals(digest, record.IntentDigest.Span)
                && payload.AsSpan().SequenceEqual(record.CanonicalPayload.Span)
                && CryptographicOperations.FixedTimeEquals(eventHash, record.EventHash.Span);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or NotSupportedException
                or EncoderFallbackException or FormatException)
        {
            return false;
        }
    }

    public static DateTimeOffset NormalizeTimestamp(DateTimeOffset value)
    {
        var utcTicks = value.ToUniversalTime().Ticks;
        return new DateTimeOffset(utcTicks - (utcTicks % 10), TimeSpan.Zero);
    }

    public static string FormatTimestamp(DateTimeOffset value) =>
        NormalizeTimestamp(value).ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);

    private static byte[] SerializeIntent(AuditEventIntent value)
    {
        using var stream = new MemoryStream();
        WriteString(stream, "MDGSM-AUDIT-INTENT-V1");
        WriteString(stream, value.EventId.ToString("N"));
        WriteString(stream, value.AuditStreamId.ToString("N"));
        WriteInt32(stream, value.SchemaVersion);
        WriteInt32(stream, value.CanonicalizationVersion);
        WriteString(stream, value.HashAlgorithmCode);
        WriteString(stream, value.EventTypeCode);
        WriteString(stream, FormatTimestamp(value.OccurredAtUtc));
        WriteString(stream, value.ActorTypeCode);
        WriteString(stream, value.ActorId.ToString("N"));
        WriteNullableString(stream, value.SubjectTypeCode);
        WriteNullableString(stream, value.SubjectId?.ToString("N"));
        WriteString(stream, value.SourceModuleCode);
        WriteString(stream, value.SourceEventId.ToString("N"));
        WriteString(stream, value.CorrelationId.ToString("N"));
        WriteNullableString(stream, value.CausationId?.ToString("N"));
        WriteString(stream, value.OutcomeCode);
        WriteString(stream, CanonicalMetadata(value.Metadata));
        return stream.ToArray();
    }

    private static byte[] SerializeEvent(
        AuditEventIntent intent,
        long sequence,
        DateTimeOffset recordedAtUtc,
        ReadOnlySpan<byte> previousHash)
    {
        using var stream = new MemoryStream();
        WriteString(stream, "MDGSM-AUDIT-EVENT-V1");
        WriteBytes(stream, SerializeIntent(intent));
        WriteInt64(stream, sequence);
        WriteString(stream, FormatTimestamp(recordedAtUtc));
        WriteBytes(stream, previousHash);
        return stream.ToArray();
    }

    private static string CanonicalMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        var sorted = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in metadata)
        {
            var key = item.Key.Normalize(NormalizationForm.FormC);
            var value = item.Value.Normalize(NormalizationForm.FormC);
            if (!sorted.TryAdd(key, value))
                throw new InvalidOperationException("Audit metadata keys collide after NFC normalization.");
        }

        return string.Join(";", sorted.Select(pair =>
            Convert.ToBase64String(StrictUtf8.GetBytes(pair.Key)) + ":"
            + Convert.ToBase64String(StrictUtf8.GetBytes(pair.Value))));
    }

    private static void WriteString(Stream stream, string value) =>
        WriteBytes(stream, StrictUtf8.GetBytes(value.Normalize(NormalizationForm.FormC)));

    private static void WriteNullableString(Stream stream, string? value)
    {
        stream.WriteByte(value is null ? (byte)0 : (byte)1);
        if (value is not null)
            WriteString(stream, value);
    }

    private static void WriteBytes(Stream stream, ReadOnlySpan<byte> value)
    {
        WriteInt32(stream, value.Length);
        stream.Write(value);
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteInt64(Stream stream, long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        stream.Write(buffer);
    }
}
