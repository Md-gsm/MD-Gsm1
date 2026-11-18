using System.Security.Cryptography;

namespace MDGsm.Accounting.Application;

/// <summary>
/// Supplies the canonical UTF-8 representation of a posting request.
/// Implementations must use a deterministic, versioned field ordering and encoding;
/// arbitrary JSON serialization output is not a canonical representation.
/// </summary>
public interface ICanonicalPostingPayload
{
    int CanonicalizationVersion { get; }
    ReadOnlyMemory<byte> GetCanonicalUtf8Payload();
}

public sealed record PostingRequestFingerprint(int CanonicalizationVersion, string PayloadSha256)
{
    public static PostingRequestFingerprint Create(ICanonicalPostingPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.CanonicalizationVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(payload), "Canonicalization version must be positive.");

        var bytes = payload.GetCanonicalUtf8Payload();
        if (bytes.IsEmpty)
            throw new ArgumentException("Canonical posting payload cannot be empty.", nameof(payload));

        var hash = Convert.ToHexString(SHA256.HashData(bytes.Span)).ToLowerInvariant();
        return new PostingRequestFingerprint(payload.CanonicalizationVersion, hash);
    }

    public bool Matches(PostingRequestFingerprint other) =>
        other is not null &&
        CanonicalizationVersion == other.CanonicalizationVersion &&
        string.Equals(PayloadSha256, other.PayloadSha256, StringComparison.Ordinal);
}

public enum PostingIdempotencyDecision
{
    NewRequest,
    ExactReplay,
    Conflict
}

/// <summary>
/// Pure decision function for the application/persistence boundary.
/// It does not read or write storage and never mutates an existing request.
/// </summary>
public static class PostingIdempotencyPolicy
{
    public static PostingIdempotencyDecision Evaluate(
        PostingRequestFingerprint? existing,
        PostingRequestFingerprint incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        if (existing is null)
            return PostingIdempotencyDecision.NewRequest;

        return existing.Matches(incoming)
            ? PostingIdempotencyDecision.ExactReplay
            : PostingIdempotencyDecision.Conflict;
    }
}
