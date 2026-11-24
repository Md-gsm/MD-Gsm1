using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace MDGsm.AuditSecurity.Contracts;

/// <summary>
/// Produces the exact v1 intent bytes used by the AuditSecurity Domain canonicalizer.
/// This copy lives in Contracts so source modules can durably enqueue an intent without
/// referencing the AuditSecurity Domain assembly. Contract-to-domain golden vectors guard drift.
/// </summary>
public static class AuditEventSubmissionCanonicalizer
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static AuditEventSubmission NormalizeAndValidate(AuditEventSubmission incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(incoming.Metadata);

        if (incoming.EventId == Guid.Empty || incoming.AuditStreamId == Guid.Empty
            || incoming.ActorId == Guid.Empty || incoming.SourceEventId == Guid.Empty
            || incoming.CorrelationId == Guid.Empty)
            throw new ArgumentException("Audit submission identifiers must be non-empty.");
        if ((incoming.SubjectTypeCode is null) != (incoming.SubjectId is null))
            throw new ArgumentException("Subject type and subject ID must both be present or both be null.");
        if (incoming.SubjectId == Guid.Empty || incoming.CausationId == Guid.Empty)
            throw new ArgumentException("Optional audit identifiers cannot contain an empty UUID.");
        if (incoming.SchemaVersion != 1 || incoming.CanonicalizationVersion != 1)
            throw new NotSupportedException("Only audit schema version 1 and canonicalization version 1 are supported.");
        if (!string.Equals(incoming.ContractVersion, ModuleContractVersion.Current, StringComparison.Ordinal))
            throw new NotSupportedException("Unsupported AuditSecurity module contract version.");

        var algorithm = NormalizeRequiredCode(incoming.HashAlgorithmCode, nameof(incoming.HashAlgorithmCode));
        if (!string.Equals(algorithm, "SHA-256", StringComparison.Ordinal))
            throw new NotSupportedException("Only SHA-256 is supported by the current audit event contract.");

        var metadata = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in incoming.Metadata)
        {
            if (item.Key is null || item.Value is null)
                throw new ArgumentException("Audit metadata keys and values cannot be null.", nameof(incoming));
            var key = item.Key.Normalize(NormalizationForm.FormC);
            var value = item.Value.Normalize(NormalizationForm.FormC);
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Audit metadata keys cannot be empty.", nameof(incoming));
            if (!metadata.TryAdd(key, value))
                throw new ArgumentException("Audit metadata keys collide after NFC normalization.", nameof(incoming));
            _ = StrictUtf8.GetByteCount(key);
            _ = StrictUtf8.GetByteCount(value);
        }

        return incoming with
        {
            HashAlgorithmCode = algorithm,
            EventTypeCode = NormalizeRequiredCode(incoming.EventTypeCode, nameof(incoming.EventTypeCode)),
            OccurredAtUtc = NormalizeTimestamp(incoming.OccurredAtUtc),
            ActorTypeCode = NormalizeRequiredCode(incoming.ActorTypeCode, nameof(incoming.ActorTypeCode)),
            SubjectTypeCode = incoming.SubjectTypeCode is null
                ? null
                : NormalizeRequiredCode(incoming.SubjectTypeCode, nameof(incoming.SubjectTypeCode)),
            SourceModuleCode = NormalizeRequiredCode(incoming.SourceModuleCode, nameof(incoming.SourceModuleCode)),
            OutcomeCode = NormalizeRequiredCode(incoming.OutcomeCode, nameof(incoming.OutcomeCode)),
            Metadata = new ReadOnlyDictionary<string, string>(metadata)
        };
    }

    public static byte[] SerializeIntent(AuditEventSubmission incoming)
    {
        var value = NormalizeAndValidate(incoming);
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

    public static byte[] ComputeIntentDigest(AuditEventSubmission incoming) =>
        SHA256.HashData(SerializeIntent(incoming));

    public static string ComputeIntentDigestHex(AuditEventSubmission incoming) =>
        Convert.ToHexString(ComputeIntentDigest(incoming));

    public static DateTimeOffset NormalizeTimestamp(DateTimeOffset value)
    {
        var utcTicks = value.ToUniversalTime().Ticks;
        return new DateTimeOffset(utcTicks - (utcTicks % 10), TimeSpan.Zero);
    }

    private static string FormatTimestamp(DateTimeOffset value) =>
        NormalizeTimestamp(value).ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);

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

    private static string NormalizeRequiredCode(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Audit code values are required.", parameterName);
        var normalized = value.Normalize(NormalizationForm.FormC);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
            throw new ArgumentException("Audit code values must already be NFC-normalized.", parameterName);
        if (normalized.Length > 128 || normalized.Any(char.IsControl))
            throw new ArgumentException("Audit code values are invalid.", parameterName);
        _ = StrictUtf8.GetByteCount(normalized);
        return normalized;
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
}
