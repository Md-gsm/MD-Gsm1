using System.Security.Cryptography;
using System.Text;

namespace MDGsm.Accounting.Domain;

public sealed class PostingIdentity
{
    private const char Separator = '\u001f';

    private PostingIdentity(string sourceModule, string sourceDocumentId, string sourceEventId,
        string postingPurpose, string sourceVersion, string? sourceSnapshotId, string? allocationId)
    {
        SourceModule = sourceModule;
        SourceDocumentId = sourceDocumentId;
        SourceEventId = sourceEventId;
        PostingPurpose = postingPurpose;
        SourceVersion = sourceVersion;
        SourceSnapshotId = sourceSnapshotId;
        AllocationId = allocationId;
    }

    public string SourceModule { get; }
    public string SourceDocumentId { get; }
    public string SourceEventId { get; }
    public string PostingPurpose { get; }
    public string SourceVersion { get; }
    public string? SourceSnapshotId { get; }
    public string? AllocationId { get; }

    public string IdempotencyKey
    {
        get
        {
            var canonical = string.Join(Separator, SourceModule, SourceDocumentId, SourceEventId,
                PostingPurpose, SourceVersion, SourceSnapshotId ?? string.Empty, AllocationId ?? string.Empty);
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        }
    }

    public static PostingIdentity Create(string sourceModule, string sourceDocumentId, string sourceEventId,
        string postingPurpose, string sourceVersion, string? sourceSnapshotId = null, string? allocationId = null)
    {
        ValidateRequired(sourceModule, nameof(sourceModule));
        ValidateRequired(sourceDocumentId, nameof(sourceDocumentId));
        ValidateRequired(sourceEventId, nameof(sourceEventId));
        ValidateRequired(postingPurpose, nameof(postingPurpose));
        ValidateRequired(sourceVersion, nameof(sourceVersion));
        ValidateOptional(sourceSnapshotId, nameof(sourceSnapshotId));
        ValidateOptional(allocationId, nameof(allocationId));

        return new PostingIdentity(sourceModule, sourceDocumentId, sourceEventId, postingPurpose,
            sourceVersion, sourceSnapshotId, allocationId);
    }

    private static void ValidateRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Any(char.IsControl))
            throw new ArgumentException("Posting identity components must be non-empty canonical strings without control characters.", parameterName);
    }

    private static void ValidateOptional(string? value, string parameterName)
    {
        if (value is not null && (value != value.Trim() || value.Any(char.IsControl)))
            throw new ArgumentException("Optional posting identity components must be canonical strings without control characters.", parameterName);
    }
}
