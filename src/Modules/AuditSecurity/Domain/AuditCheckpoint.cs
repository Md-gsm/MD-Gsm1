using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace MDGsm.AuditSecurity.Domain;

public sealed record AuditCheckpointPayload(
    Guid AuditStreamId,
    long LastSequence,
    string HeadHashHex,
    string KeyId,
    DateTimeOffset CreatedAtUtc,
    short SchemaVersion,
    short CanonicalizationVersion,
    string HashAlgorithmCode,
    string? PreviousCheckpointReferenceHex);

public sealed class AuditCheckpointEnvelope
{
    private readonly byte[] _signature;

    public AuditCheckpointEnvelope(AuditCheckpointPayload payload, ReadOnlySpan<byte> signature)
    {
        Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        _signature = signature.ToArray();
    }

    public AuditCheckpointPayload Payload { get; }
    public ReadOnlyMemory<byte> Signature => _signature;
}

public static class AuditCheckpointCanonicalizer
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static byte[] Serialize(AuditCheckpointPayload incoming)
    {
        var payload = Validate(incoming);
        using var stream = new MemoryStream();
        WriteString(stream, "MDGSM-AUDIT-CHECKPOINT-V1");
        WriteString(stream, payload.AuditStreamId.ToString("N"));
        WriteInt64(stream, payload.LastSequence);
        WriteBytes(stream, Convert.FromHexString(payload.HeadHashHex));
        WriteString(stream, payload.KeyId);
        WriteString(stream, AuditEventCanonicalizer.FormatTimestamp(payload.CreatedAtUtc));
        WriteInt32(stream, payload.SchemaVersion);
        WriteInt32(stream, payload.CanonicalizationVersion);
        WriteString(stream, payload.HashAlgorithmCode);
        WriteNullableBytes(stream, payload.PreviousCheckpointReferenceHex is null
            ? null
            : Convert.FromHexString(payload.PreviousCheckpointReferenceHex));
        return stream.ToArray();
    }

    public static string ComputeReference(AuditCheckpointEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        using var stream = new MemoryStream();
        WriteBytes(stream, Serialize(envelope.Payload));
        WriteBytes(stream, envelope.Signature.Span);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    public static AuditCheckpointPayload Validate(AuditCheckpointPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.AuditStreamId == Guid.Empty || payload.LastSequence < 0)
            throw new ArgumentException("Checkpoint stream identity and sequence are invalid.", nameof(payload));
        if (payload.SchemaVersion != 1 || payload.CanonicalizationVersion != 1
            || !string.Equals(payload.HashAlgorithmCode, "SHA-256", StringComparison.Ordinal))
            throw new NotSupportedException("Unsupported audit checkpoint version or hash algorithm.");
        if (string.IsNullOrWhiteSpace(payload.KeyId)
            || !string.Equals(payload.KeyId, payload.KeyId.Normalize(NormalizationForm.FormC), StringComparison.Ordinal))
            throw new ArgumentException("Checkpoint key ID is required and must be NFC-normalized.", nameof(payload));
        ValidateDigest(payload.HeadHashHex, nameof(payload.HeadHashHex));
        if (payload.PreviousCheckpointReferenceHex is not null)
            ValidateDigest(payload.PreviousCheckpointReferenceHex, nameof(payload.PreviousCheckpointReferenceHex));

        return payload with { CreatedAtUtc = AuditEventCanonicalizer.NormalizeTimestamp(payload.CreatedAtUtc) };
    }

    private static void ValidateDigest(string value, string parameterName)
    {
        if (value.Length != 64)
            throw new ArgumentException("Checkpoint references must be 32-byte SHA-256 hex values.", parameterName);
        try { _ = Convert.FromHexString(value); }
        catch (FormatException exception)
        {
            throw new ArgumentException("Checkpoint references must be hexadecimal.", parameterName, exception);
        }
    }

    private static void WriteString(Stream stream, string value) =>
        WriteBytes(stream, StrictUtf8.GetBytes(value.Normalize(NormalizationForm.FormC)));

    private static void WriteNullableBytes(Stream stream, byte[]? value)
    {
        stream.WriteByte(value is null ? (byte)0 : (byte)1);
        if (value is not null)
            WriteBytes(stream, value);
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

public static class AuditCheckpointVerifier
{
    public const int RequiredRsaKeySize = 3072;

    public static bool Verify(
        AuditCheckpointEnvelope envelope,
        string expectedKeyId,
        ReadOnlySpan<byte> trustedSubjectPublicKeyInfo)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (string.IsNullOrWhiteSpace(expectedKeyId)
            || !string.Equals(envelope.Payload.KeyId, expectedKeyId, StringComparison.Ordinal)
            || envelope.Signature.Length == 0
            || trustedSubjectPublicKeyInfo.IsEmpty)
            return false;

        try
        {
            var payloadBytes = AuditCheckpointCanonicalizer.Serialize(envelope.Payload);
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(trustedSubjectPublicKeyInfo, out var bytesRead);
            return bytesRead == trustedSubjectPublicKeyInfo.Length
                && rsa.KeySize == RequiredRsaKeySize
                && rsa.VerifyData(payloadBytes, envelope.Signature.Span,
                    HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }
        catch (Exception exception) when (
            exception is CryptographicException or ArgumentException or FormatException or NotSupportedException)
        {
            return false;
        }
    }
}
