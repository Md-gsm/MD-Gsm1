namespace MDGsm.Domain.Persons;

public enum DefaultPaymentMethod { Cash = 1, CardToCard = 2, BankTransfer = 3, Pos = 4, Credit = 5 }

/// <summary>Financial defaults only. Amounts are stored as integer Rial; no balance or ledger is held here.</summary>
public sealed record PartyFinancialProfile
{
    private PartyFinancialProfile(long creditLimitRial, decimal defaultDiscountPercent, DefaultPaymentMethod defaultPaymentMethod, string? bankCardLastFour)
    {
        CreditLimitRial = creditLimitRial;
        DefaultDiscountPercent = defaultDiscountPercent;
        DefaultPaymentMethod = defaultPaymentMethod;
        BankCardLastFour = bankCardLastFour;
    }

    public long CreditLimitRial { get; }
    public decimal DefaultDiscountPercent { get; }
    public DefaultPaymentMethod DefaultPaymentMethod { get; }
    public string? BankCardLastFour { get; }

    public static PartyFinancialProfile Create(
        long creditLimitRial = 0,
        decimal defaultDiscountPercent = 0,
        DefaultPaymentMethod defaultPaymentMethod = DefaultPaymentMethod.Cash,
        string? bankCardLastFour = null)
    {
        if (creditLimitRial < 0) throw new ArgumentOutOfRangeException(nameof(creditLimitRial));
        if (defaultDiscountPercent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(defaultDiscountPercent));
        if (!Enum.IsDefined(defaultPaymentMethod)) throw new ArgumentOutOfRangeException(nameof(defaultPaymentMethod));
        var lastFour = string.IsNullOrWhiteSpace(bankCardLastFour) ? null : PartyIdentifiers.NormalizeDigits(bankCardLastFour).Trim();
        if (lastFour is not null && (lastFour.Length != 4 || lastFour.Any(c => c is < '0' or > '9')))
            throw new ArgumentException("Only the last four card digits may be retained.", nameof(bankCardLastFour));
        return new(creditLimitRial, defaultDiscountPercent, defaultPaymentMethod, lastFour);
    }
}
