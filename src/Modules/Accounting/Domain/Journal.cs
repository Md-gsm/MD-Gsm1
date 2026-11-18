using System.Numerics;

namespace MDGsm.Accounting.Domain;

public enum JournalStatus
{
    Draft, Finalized
}

public sealed class Journal
{
    private readonly IReadOnlyList<JournalLine> _lines;

    private Journal(Guid journalId, Guid fiscalPeriodId, DateOnly journalDate, PostingIdentity postingIdentity,
        IReadOnlyList<JournalLine> lines, string sourceDocumentReference, string createdBy,
        DateTimeOffset createdAtUtc, JournalStatus status, string? finalizedBy,
        DateTimeOffset? finalizedAtUtc, Guid? reversalOfJournalId)
    {
        JournalId = journalId;
        FiscalPeriodId = fiscalPeriodId;
        JournalDate = journalDate;
        PostingIdentity = postingIdentity;
        _lines = Array.AsReadOnly(lines.ToArray());
        SourceDocumentReference = sourceDocumentReference;
        CreatedBy = createdBy;
        CreatedAtUtc = createdAtUtc;
        Status = status;
        FinalizedBy = finalizedBy;
        FinalizedAtUtc = finalizedAtUtc;
        ReversalOfJournalId = reversalOfJournalId;
    }

    public Guid JournalId { get; }
    public Guid FiscalPeriodId { get; }
    public DateOnly JournalDate { get; }
    public PostingIdentity PostingIdentity { get; }
    public IReadOnlyList<JournalLine> Lines => _lines;
    public string SourceDocumentReference { get; }
    public string CreatedBy { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public JournalStatus Status { get; }
    public string? FinalizedBy { get; }
    public DateTimeOffset? FinalizedAtUtc { get; }
    public Guid? ReversalOfJournalId { get; }

    public IrrAmount TotalDebits =>
        new(checked((long)_lines.Aggregate(BigInteger.Zero, (sum, line) => sum + line.Debit.Value)));

    public IrrAmount TotalCredits =>
        new(checked((long)_lines.Aggregate(BigInteger.Zero, (sum, line) => sum + line.Credit.Value)));

    public static Journal CreateDraft(Guid journalId, Guid fiscalPeriodId, DateOnly journalDate,
        PostingIdentity postingIdentity, IEnumerable<JournalLine> lines, string sourceDocumentReference,
        string createdBy, DateTimeOffset createdAtUtc, Guid? reversalOfJournalId = null)
    {
        ArgumentNullException.ThrowIfNull(postingIdentity);
        ArgumentNullException.ThrowIfNull(lines);

        if (journalId == Guid.Empty || fiscalPeriodId == Guid.Empty)
            throw new ArgumentException("Journal and fiscal period identifiers are required.");
        if (reversalOfJournalId == Guid.Empty)
            throw new ArgumentException("A reversal reference cannot be an empty identifier.");
        if (string.IsNullOrWhiteSpace(sourceDocumentReference) || string.IsNullOrWhiteSpace(createdBy))
            throw new ArgumentException("Source document reference and creator are required.");
        if (createdAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Journal timestamps must be UTC.", nameof(createdAtUtc));

        var materialized = lines.ToArray();
        if (materialized.Length < 2)
            throw new InvalidOperationException("A journal requires at least two lines.");
        if (materialized.Any(line => line is null))
            throw new InvalidOperationException("Journal lines cannot be null.");
        if (materialized.Select(line => line.LineId).Distinct().Count() != materialized.Length)
            throw new InvalidOperationException("Journal line identifiers must be unique.");

        foreach (var line in materialized)
            _ = JournalLine.Create(line.LineId, line.AccountId, line.AccountCode, line.Debit,
                line.Credit, line.PartySubledgerReference, line.DimensionsJson);

        var totalDebits = materialized.Aggregate(BigInteger.Zero, (sum, line) => sum + line.Debit.Value);
        var totalCredits = materialized.Aggregate(BigInteger.Zero, (sum, line) => sum + line.Credit.Value);
        if (totalDebits > long.MaxValue || totalCredits > long.MaxValue)
            throw new OverflowException("Journal totals exceed the supported IRR range.");
        if (totalDebits != totalCredits)
            throw new InvalidOperationException("Journal is not balanced.");

        return new Journal(journalId, fiscalPeriodId, journalDate, postingIdentity, materialized,
            sourceDocumentReference, createdBy, createdAtUtc, JournalStatus.Draft, null, null, reversalOfJournalId);
    }

    public Journal Finalize(FiscalPeriod period, string actorId, DateTimeOffset finalizedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(period);
        if (Status != JournalStatus.Draft)
            throw new InvalidOperationException("Only a draft journal can be finalized.");
        if (period.PeriodId != FiscalPeriodId || !period.AllowsPosting(JournalDate))
            throw new InvalidOperationException("Journal date is outside an open fiscal period.");
        if (string.IsNullOrWhiteSpace(actorId))
            throw new ArgumentException("Finalizing actor is required.", nameof(actorId));
        if (finalizedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Journal timestamps must be UTC.", nameof(finalizedAtUtc));

        return new Journal(JournalId, FiscalPeriodId, JournalDate, PostingIdentity, _lines,
            SourceDocumentReference, CreatedBy, CreatedAtUtc, JournalStatus.Finalized,
            actorId, finalizedAtUtc, ReversalOfJournalId);
    }

    public Journal CreateReversal(Guid reversalJournalId, Guid reversalFiscalPeriodId, DateOnly reversalDate,
        PostingIdentity reversalIdentity, string createdBy, DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(reversalIdentity);
        if (Status != JournalStatus.Finalized)
            throw new InvalidOperationException("Only a finalized journal can be reversed.");
        if (!string.Equals(reversalIdentity.PostingPurpose, "REVERSAL", StringComparison.Ordinal))
            throw new InvalidOperationException("A reversal must use a distinct REVERSAL posting identity.");

        var reversedLines = _lines.Select(line => JournalLine.Create(
            Guid.CreateVersion7(), line.AccountId, line.AccountCode, line.Credit, line.Debit,
            line.PartySubledgerReference, line.DimensionsJson));

        return CreateDraft(reversalJournalId, reversalFiscalPeriodId, reversalDate, reversalIdentity,
            reversedLines, SourceDocumentReference, createdBy, createdAtUtc, JournalId);
    }
}
