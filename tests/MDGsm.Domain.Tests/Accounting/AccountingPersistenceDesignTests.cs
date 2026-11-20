using System.Text;
using MDGsm.Accounting.Application;
using MDGsm.Accounting.Domain;
using Xunit;

namespace MDGsm.Domain.Tests.Accounting;

[Trait("TestType", "UnitSubset")]
public sealed class AccountingPersistenceDesignTests
{
    private static readonly DateOnly Date = new(2026, 11, 18);
    private static readonly DateTimeOffset Utc = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("TestId", "MDG-ACC-AP01-001")]
    public void Journal_requires_and_retains_an_explicit_coa_snapshot()
    {
        var periodId = Guid.CreateVersion7();
        var snapshotId = Guid.CreateVersion7();
        var journal = Journal.CreateDraft(
            Guid.CreateVersion7(), periodId, snapshotId, Date,
            PostingIdentity.Create("Sales", "sale-1", "handover", "SALE", "1"),
            Lines(), "SALE-1", "actor-1", Utc, "audit-create");

        Assert.Equal(snapshotId, journal.CoaSnapshotId);
        Assert.Equal(snapshotId, journal.Finalize(
            new FiscalPeriod(periodId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), FiscalPeriodStatus.Open),
            "actor-1", Utc, "audit-finalize").CoaSnapshotId);
    }

    [Fact]
    [Trait("TestId", "MDG-ACC-AP01-002")]
    public void Journal_reversal_retains_the_original_coa_snapshot()
    {
        var periodId = Guid.CreateVersion7();
        var snapshotId = Guid.CreateVersion7();
        var draft = Journal.CreateDraft(
            Guid.CreateVersion7(), periodId, snapshotId, Date,
            PostingIdentity.Create("Sales", "sale-1", "handover", "SALE", "1"),
            Lines(), "SALE-1", "actor-1", Utc, "audit-create");
        var finalized = draft.Finalize(
            new FiscalPeriod(periodId, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), FiscalPeriodStatus.Open),
            "actor-1", Utc, "audit-finalize");

        var reversal = finalized.CreateReversal(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Date,
            PostingIdentity.Create("Sales", "sale-1", "handover", "REVERSAL", "1"),
            "actor-2", Utc, "audit-reversal");

        Assert.Equal(snapshotId, reversal.CoaSnapshotId);
        Assert.Equal(finalized.JournalId, reversal.ReversalOfJournalId);
    }

    [Fact]
    [Trait("TestId", "MDG-ACC-AP01-003")]
    public void Journal_rejects_an_empty_coa_snapshot_identifier()
    {
        Assert.Throws<ArgumentException>(() => Journal.CreateDraft(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.Empty, Date,
            PostingIdentity.Create("Sales", "sale-1", "handover", "SALE", "1"),
            Lines(), "SALE-1", "actor-1", Utc, "audit-create"));
    }

    [Fact]
    [Trait("TestId", "MDG-ACC-AP02-001")]
    public void Same_identity_and_same_canonical_payload_is_an_exact_replay()
    {
        var first = PostingRequestFingerprint.Create(new Payload(1, "SALE-1|100|IRR"));
        var retry = PostingRequestFingerprint.Create(new Payload(1, "SALE-1|100|IRR"));

        Assert.Equal(first, retry);
        Assert.Equal(PostingIdempotencyDecision.ExactReplay,
            PostingIdempotencyPolicy.Evaluate(first, retry));
    }

    [Fact]
    [Trait("TestId", "MDG-ACC-AP02-002")]
    public void Same_identity_with_changed_payload_is_a_conflict()
    {
        var stored = PostingRequestFingerprint.Create(new Payload(1, "SALE-1|100|IRR"));
        var changed = PostingRequestFingerprint.Create(new Payload(1, "SALE-1|101|IRR"));

        Assert.NotEqual(stored.PayloadSha256, changed.PayloadSha256);
        Assert.Equal(PostingIdempotencyDecision.Conflict,
            PostingIdempotencyPolicy.Evaluate(stored, changed));
    }

    [Fact]
    [Trait("TestId", "MDG-ACC-AP02-003")]
    public void Same_payload_with_changed_canonicalization_version_is_a_conflict()
    {
        var stored = PostingRequestFingerprint.Create(new Payload(1, "SALE-1|100|IRR"));
        var changedVersion = PostingRequestFingerprint.Create(new Payload(2, "SALE-1|100|IRR"));

        Assert.Equal(stored.PayloadSha256, changedVersion.PayloadSha256);
        Assert.Equal(PostingIdempotencyDecision.Conflict,
            PostingIdempotencyPolicy.Evaluate(stored, changedVersion));
    }

    [Fact]
    [Trait("TestId", "MDG-ACC-AP02-004")]
    public void Missing_stored_fingerprint_is_a_new_request()
    {
        var incoming = PostingRequestFingerprint.Create(new Payload(1, "SALE-1|100|IRR"));
        Assert.Equal(PostingIdempotencyDecision.NewRequest,
            PostingIdempotencyPolicy.Evaluate(null, incoming));
    }

    [Fact]
    public void Fingerprint_rejects_invalid_version_and_empty_canonical_payload()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PostingRequestFingerprint.Create(new Payload(0, "SALE-1|100|IRR")));
        Assert.Throws<ArgumentException>(() =>
            PostingRequestFingerprint.Create(new Payload(1, "")));
    }

    private static JournalLine[] Lines() =>
    [
        JournalLine.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "1101", new(100), IrrAmount.Zero),
        JournalLine.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), "4101", IrrAmount.Zero, new(100))
    ];

    private sealed record Payload(int CanonicalizationVersion, string Value) : ICanonicalPostingPayload
    {
        public ReadOnlyMemory<byte> GetCanonicalUtf8Payload() => Encoding.UTF8.GetBytes(Value);
    }
}
