namespace MDGsm.Domain.Persons;

public enum ContactPointKind { Mobile = 1, Landline = 2, Email = 3 }

public sealed record PartyContactPoint
{
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
        var normalized = PartyIdentifiers.NormalizeDigits(value).Trim().Replace(" ", "").Replace("-", "");
        if (normalized.StartsWith("00", StringComparison.Ordinal)) normalized = "+" + normalized[2..];
        if (normalized.StartsWith("0", StringComparison.Ordinal)) normalized = "+98" + normalized[1..];
        if (normalized.StartsWith("98", StringComparison.Ordinal)) normalized = "+" + normalized;
        if (!normalized.StartsWith('+') || normalized.Length is < 9 or > 16 || normalized[1..].Any(c => c is < '0' or > '9'))
            throw new ArgumentException("Mobile number must be a valid international number; Iranian 09 numbers are normalized to +98.", nameof(value));
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
