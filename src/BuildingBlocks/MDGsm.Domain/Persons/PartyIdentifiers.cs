namespace MDGsm.Domain.Persons;

/// <summary>Normalized Iranian and international identity values. Validation is format-only; legal verification is external.</summary>
public static class PartyIdentifiers
{
    public static string NormalizeDigits(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return string.Concat(value.Select(character => character switch
        {
            >= '۰' and <= '۹' => (char)('0' + character - '۰'),
            >= '٠' and <= '٩' => (char)('0' + character - '٠'),
            _ => character
        }));
    }

    /// <summary>Masks a phone number so that only the last two digits remain visible.</summary>
    public static string MaskPhone(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Length <= 2 ? new string('*', value.Length) : new string('*', value.Length - 2) + value[^2..];
    }

    /// <summary>Masks an identifier so that only the last three characters remain visible.</summary>
    public static string MaskIdentifier(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Length <= 3 ? new string('*', value.Length) : new string('*', value.Length - 3) + value[^3..];
    }

    /// <summary>Masks a person's name, retaining only the first character of each name part.</summary>
    public static string MaskPersonName(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', parts.Select(part => part.Length <= 1 ? "*" : part[..1] + new string('*', Math.Min(part.Length - 1, 7))));
    }

    /// <summary>Masks an email address, keeping only the domain.</summary>
    public static string MaskEmail(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var at = value.LastIndexOf('@');
        return at < 0 ? "***" : "***" + value[at..];
    }

    public static string? NormalizeIranianNationalId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = NormalizeDigits(value).Trim().Replace("-", "").Replace(" ", "");
        if (normalized.Length != 10 || normalized.Any(c => c is < '0' or > '9'))
            throw new ArgumentException("National identity number must contain exactly 10 digits.", nameof(value));
        return normalized;
    }

    public static string? NormalizeIranianLegalNationalId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = NormalizeDigits(value).Trim().Replace("-", "").Replace(" ", "");
        if (normalized.Length != 11 || normalized.Any(c => c is < '0' or > '9'))
            throw new ArgumentException("Legal national identity number must contain exactly 11 digits.", nameof(value));
        return normalized;
    }

    public static string? NormalizeEconomicCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = NormalizeDigits(value).Trim().Replace("-", "").Replace(" ", "");
        if (normalized.Length is < 10 or > 14 || normalized.Any(c => c is < '0' or > '9'))
            throw new ArgumentException("Economic code must contain 10 to 14 digits.", nameof(value));
        return normalized;
    }
}
