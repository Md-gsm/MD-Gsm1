namespace MDGsm.Accounting.Domain;

public enum FiscalPeriodStatus
{
    Open, Closed
}

public sealed record PeriodCloseEvidence(
    bool BankReconciled,
    bool CashReconciled,
    bool PartySubledgersReconciled,
    bool InventoryReconciled,
    bool TaxReconciled,
    bool SuspenseAccountsResolved,
    bool NoPendingJournals,
    bool NoPendingApprovalManualJournals)
{
    public bool IsComplete =>
        BankReconciled && CashReconciled && PartySubledgersReconciled &&
        InventoryReconciled && TaxReconciled && SuspenseAccountsResolved &&
        NoPendingJournals && NoPendingApprovalManualJournals;
}

public sealed record PeriodReopenAuthorization(
    string RequestedBy,
    string ApprovedBy,
    string Reason,
    string AuditEventId);

public sealed record FiscalPeriod(
    Guid PeriodId,
    DateOnly StartsOn,
    DateOnly EndsOn,
    FiscalPeriodStatus Status,
    string? LastChangedBy = null,
    DateTimeOffset? LastChangedAtUtc = null,
    string? LastAuditEventId = null)
{
    public bool AllowsPosting(DateOnly date) =>
        PeriodId != Guid.Empty && StartsOn <= EndsOn &&
        Status == FiscalPeriodStatus.Open && date >= StartsOn && date <= EndsOn;

    public FiscalPeriod Close(PeriodCloseEvidence evidence, string actorId, DateTimeOffset changedAtUtc, string auditEventId)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ValidateAudit(actorId, changedAtUtc, auditEventId);
        if (Status != FiscalPeriodStatus.Open)
            throw new InvalidOperationException("Only an open fiscal period can be closed.");
        if (!evidence.IsComplete)
            throw new InvalidOperationException("Fiscal period close is blocked until every reconciliation and pending-document check is complete.");

        return this with
        {
            Status = FiscalPeriodStatus.Closed,
            LastChangedBy = actorId,
            LastChangedAtUtc = changedAtUtc,
            LastAuditEventId = auditEventId
        };
    }

    public FiscalPeriod Reopen(PeriodReopenAuthorization authorization, DateTimeOffset changedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        if (Status != FiscalPeriodStatus.Closed)
            throw new InvalidOperationException("Only a closed fiscal period can be reopened.");
        if (string.IsNullOrWhiteSpace(authorization.RequestedBy) ||
            string.IsNullOrWhiteSpace(authorization.ApprovedBy) ||
            string.Equals(authorization.RequestedBy, authorization.ApprovedBy, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(authorization.Reason) ||
            string.IsNullOrWhiteSpace(authorization.AuditEventId))
            throw new InvalidOperationException("Reopening requires a reason, audit event, requester and a distinct independent approver.");

        ValidateUtc(changedAtUtc);
        return this with
        {
            Status = FiscalPeriodStatus.Open,
            LastChangedBy = authorization.ApprovedBy,
            LastChangedAtUtc = changedAtUtc,
            LastAuditEventId = authorization.AuditEventId
        };
    }

    private static void ValidateAudit(string actorId, DateTimeOffset changedAtUtc, string auditEventId)
    {
        if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(auditEventId))
            throw new ArgumentException("Actor and audit event identifiers are required.");
        ValidateUtc(changedAtUtc);
    }

    private static void ValidateUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new ArgumentException("Accounting audit timestamps must be UTC.", nameof(value));
    }
}
