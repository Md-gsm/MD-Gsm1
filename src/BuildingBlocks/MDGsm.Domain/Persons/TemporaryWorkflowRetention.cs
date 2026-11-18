namespace MDGsm.Domain.Persons;

/// <summary>Policy state only; encrypted PII payload storage belongs to Infrastructure.</summary>
public sealed class TemporaryWorkflowRetention
{
    public const int DefaultTtlDays = 7;
    public const int AbsoluteMaxAgeDays = 30;
    public const int MaxRenewals = 1;

    private TemporaryWorkflowRetention(Guid id, DateTimeOffset created, DateTimeOffset expires,
        DateTimeOffset absoluteExpires, int renewals, bool open)
    {
        WorkflowRecordId = id; CreatedAtUtc = created; ExpiresAtUtc = expires;
        AbsoluteExpiresAtUtc = absoluteExpires; RenewalCount = renewals; WorkflowOpen = open;
    }

    public Guid WorkflowRecordId { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public DateTimeOffset AbsoluteExpiresAtUtc { get; }
    public int RenewalCount { get; }
    public bool WorkflowOpen { get; }

    public static TemporaryWorkflowRetention Create(Guid id, DateTimeOffset createdAtUtc)
    {
        if (id == Guid.Empty) throw new ArgumentException("A workflow record identifier is required.", nameof(id));
        var created = createdAtUtc.ToUniversalTime();
        return new(id, created, created.AddDays(DefaultTtlDays), created.AddDays(AbsoluteMaxAgeDays), 0, true);
    }

    public bool IsAccessibleAt(DateTimeOffset nowUtc) => WorkflowOpen && nowUtc.ToUniversalTime() < ExpiresAtUtc;
    public bool IsPurgeDueAt(DateTimeOffset nowUtc) => nowUtc.ToUniversalTime() >= ExpiresAtUtc;
    public TemporaryWorkflowRetention Close() =>
        new(WorkflowRecordId, CreatedAtUtc, ExpiresAtUtc, AbsoluteExpiresAtUtc, RenewalCount, false);

    public TemporaryWorkflowRetention Renew(DateTimeOffset nowUtc, TimeSpan extension, string reason, bool authorized)
    {
        var now = nowUtc.ToUniversalTime();
        if (!authorized) throw new UnauthorizedAccessException("Explicit renewal authorization is required.");
        if (!WorkflowOpen || now >= ExpiresAtUtc) throw new InvalidOperationException("Only an open, unexpired workflow can be renewed.");
        if (RenewalCount >= MaxRenewals) throw new InvalidOperationException("The single permitted renewal has already been used.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 8 || reason.Length > 500)
            throw new ArgumentException("A renewal reason between 8 and 500 characters is required.", nameof(reason));
        if (extension <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(extension));
        var requested = ExpiresAtUtc + extension;
        var capped = requested < AbsoluteExpiresAtUtc ? requested : AbsoluteExpiresAtUtc;
        if (capped <= ExpiresAtUtc) throw new InvalidOperationException("The absolute 30-day retention limit has been reached.");
        return new(WorkflowRecordId, CreatedAtUtc, capped, AbsoluteExpiresAtUtc, RenewalCount + 1, true);
    }
}
