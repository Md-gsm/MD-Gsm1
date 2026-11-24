using System.Collections.ObjectModel;
using System.Text;

namespace MDGsm.AuditSecurity.Domain;

public sealed record AuditEventIntent(
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
    IReadOnlyDictionary<string, string> Metadata)
{
    public AuditEventIntent NormalizeAndValidate()
    {
        ArgumentNullException.ThrowIfNull(Metadata);
        if (EventId == Guid.Empty || AuditStreamId == Guid.Empty || ActorId == Guid.Empty
            || SourceEventId == Guid.Empty || CorrelationId == Guid.Empty)
            throw new ArgumentException("Audit event and identity identifiers must be non-empty.");
        if ((SubjectTypeCode is null) != (SubjectId is null))
            throw new ArgumentException("Subject type and subject ID must both be present or both be null.");
        if (SubjectId == Guid.Empty || CausationId == Guid.Empty)
            throw new ArgumentException("Optional audit identifiers cannot contain an empty UUID.");
        if (SchemaVersion != 1 || CanonicalizationVersion != 1)
            throw new NotSupportedException("Only audit schema version 1 and canonicalization version 1 are supported.");

        var algorithm = NormalizeRequiredCode(HashAlgorithmCode, nameof(HashAlgorithmCode));
        if (!string.Equals(algorithm, "SHA-256", StringComparison.Ordinal))
            throw new NotSupportedException("Only SHA-256 is supported by the current audit event contract.");

        var metadata = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in Metadata)
        {
            if (item.Key is null || item.Value is null)
                throw new ArgumentException("Audit metadata keys and values cannot be null.", nameof(Metadata));
            var key = item.Key.Normalize(NormalizationForm.FormC);
            var value = item.Value.Normalize(NormalizationForm.FormC);
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Audit metadata keys cannot be empty.", nameof(Metadata));
            if (!metadata.TryAdd(key, value))
                throw new ArgumentException("Audit metadata keys collide after NFC normalization.", nameof(Metadata));
        }

        var strictUtf8 = new UTF8Encoding(false, true);
        foreach (var item in metadata)
        {
            _ = strictUtf8.GetByteCount(item.Key);
            _ = strictUtf8.GetByteCount(item.Value);
        }

        return this with
        {
            HashAlgorithmCode = algorithm,
            EventTypeCode = NormalizeRequiredCode(EventTypeCode, nameof(EventTypeCode)),
            OccurredAtUtc = AuditEventCanonicalizer.NormalizeTimestamp(OccurredAtUtc),
            ActorTypeCode = NormalizeRequiredCode(ActorTypeCode, nameof(ActorTypeCode)),
            SubjectTypeCode = SubjectTypeCode is null ? null : NormalizeRequiredCode(SubjectTypeCode, nameof(SubjectTypeCode)),
            SourceModuleCode = NormalizeRequiredCode(SourceModuleCode, nameof(SourceModuleCode)),
            OutcomeCode = NormalizeRequiredCode(OutcomeCode, nameof(OutcomeCode)),
            Metadata = new ReadOnlyDictionary<string, string>(metadata)
        };
    }

    private static string NormalizeRequiredCode(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Audit code values are required.", parameterName);
        var normalized = value.Normalize(NormalizationForm.FormC);
        if (!string.Equals(normalized, normalized.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Audit code values cannot have leading or trailing whitespace.", parameterName);
        return normalized;
    }
}
