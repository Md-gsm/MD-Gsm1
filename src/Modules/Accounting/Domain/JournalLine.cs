namespace MDGsm.Accounting.Domain;

public sealed record JournalLine(
    Guid LineId,
    Guid AccountId,
    string AccountCode,
    IrrAmount Debit,
    IrrAmount Credit,
    string? PartySubledgerReference,
    string? DimensionsJson)
{
    public static JournalLine Create(Guid lineId, Guid accountId, string accountCode,
        IrrAmount debit, IrrAmount credit, string? partySubledgerReference = null, string? dimensionsJson = null)
    {
        if (lineId == Guid.Empty || accountId == Guid.Empty)
            throw new ArgumentException("Journal line and account identifiers are required.");
        if (string.IsNullOrWhiteSpace(accountCode) || accountCode.Any(character => !char.IsAsciiDigit(character)))
            throw new ArgumentException("Account code must contain only ASCII digits.", nameof(accountCode));
        if (debit.Value < 0 || credit.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(debit), "Debit and credit amounts cannot be negative.");
        if ((debit.Value > 0) == (credit.Value > 0))
            throw new ArgumentException("Exactly one of debit or credit must be positive.");

        return new JournalLine(lineId, accountId, accountCode, debit, credit, partySubledgerReference, dimensionsJson);
    }
}
