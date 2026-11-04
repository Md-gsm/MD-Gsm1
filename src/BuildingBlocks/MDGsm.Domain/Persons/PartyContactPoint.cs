namespace MDGsm.Domain.Persons;

public enum ContactPointKind { Mobile = 1, Landline = 2, Email = 3 }

public sealed record PartyContactPoint
{
    // Iranian mobile: 09XXXXXXXXX, 9XXXXXXXXX is NOT accepted, +989XXXXXXXXX, 00989XXXXXXXXX, 989XXXXXXXXX.
    private static readonly System.Text.RegularExpressions.Regex IranianMobile =
        new("^(?:\\+98|0098|98|0)9[0-9]{9}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly System.Text.RegularExpressions.Regex InternationalNumber =
        new("^\\+[1-9][0-9]{7,14}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private PartyContactPoint(ContactPointKind kind, string value, bool isPrimary, bool isMessagingAllowed)
    {
        Kind = kind;
        Value = value;
        IsPrimary = isPrimary;
        IsMessagingAllowed = isMessagingAllowed;
    }

    public ContactPointKind Kind { get; }
    public string Value { get; }
    public bool IsPrimary { get; }
    public bool IsMessagingAllowed { get; }

    public static PartyContactPoint Mobile(string value, bool isPrimary = false, bool isMessagingAllowed = false)
    {
        ArgumentNullException.ThrowIfNull(value);
        var compact = PartyIdentifiers.NormalizeDigits(value).Trim().Replace(" ", "").Replace("-", "");
        string normalized;

        if (IranianMobile.IsMatch(compact))
        {
            // Last nine digits are the subscriber part after the leading 9.
            normalized = "+989" + compact[^9..];
        }
        else if (compact.StartsWith("00", StringComparison.Ordinal) && !compact.StartsWith("0098", StringComparison.Ordinal)
                 && InternationalNumber.IsMatch("+" + compact[2..]) && !("+" + compact[2..]).StartsWith("+98", StringComparison.Ordinal))
        {
            normalized = "+" + compact[2..];
        }
        else if (compact.StartsWith('+') && !compact.StartsWith("+98", StringComparison.Ordinal) && InternationalNumber.IsMatch(compact))
        {
            normalized = compact;
        }
        else
        {
            throw new ArgumentException(
                "Mobile number must be an Iranian mobile (09XXXXXXXXX or +989XXXXXXXXX) or a valid international number; landline and malformed numbers are rejected.",
                nameof(value));
        }

        return new(ContactPointKind.Mobile, normalized, isPrimary, isMessagingAllowed);
    }

    public static PartyContactPoint Landline(string value, bool isPrimary = false)
    {
        var normalized = PartyIdentifiers.NormalizeDigits(value).Trim().Replace(" ", "").Replace("-", "");
        if (normalized.Length is < 7 or > 15 || normalized.Any(c => c is < '0' or > '9'))
            throw new ArgumentException("Landline number must contain 7 to 15 digits.", nameof(value));
        return new(ContactPointKind.Landline, normalized, isPrimary, false);
    }

    public static PartyContactPoint Email(string value, bool isPrimary = false)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 254 ||
            !System.Net.Mail.MailAddress.TryCreate(normalized, out var address) ||
            !string.Equals(address.Address, normalized, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A valid email address is required.", nameof(value));
        return new(ContactPointKind.Email, normalized, isPrimary, false);
    }
}
