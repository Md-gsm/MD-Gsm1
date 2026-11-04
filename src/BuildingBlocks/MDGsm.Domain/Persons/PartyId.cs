namespace MDGsm.Domain.Persons;

/// <summary>
/// Stable UUIDv7 identity shared by natural persons and legal entities.
/// The identifier carries no business meaning or personal data, but a UUIDv7 does embed its creation
/// time (millisecond precision); treat it as an internal key, not as an anonymous token.
/// </summary>
public readonly record struct PartyId
{
    private PartyId(Guid value) => Value = value;

    public Guid Value { get; }

    /// <summary>True for <c>default(PartyId)</c>, which bypasses validation and must never be persisted.</summary>
    public bool IsEmpty => Value == Guid.Empty;

    public static PartyId New() => new(Guid.CreateVersion7());

    public static PartyId FromGuid(Guid value)
    {
        if (!IsUuidV7(value))
        {
            throw new ArgumentException("PartyId must be a non-empty UUIDv7 value.", nameof(value));
        }

        return new PartyId(value);
    }

    public static bool TryParse(string? value, out PartyId partyId)
    {
        partyId = default;

        // Canonical 8-4-4-4-12 form only: braces, URNs and hyphen-less forms are rejected.
        if (value is null || value.Length != 36 || !Guid.TryParseExact(value, "D", out var parsed) || !IsUuidV7(parsed))
        {
            return false;
        }

        partyId = new PartyId(parsed);
        return true;
    }

    public override string ToString() => Value.ToString("D");

    private static bool IsUuidV7(Guid value)
    {
        if (value == Guid.Empty)
        {
            return false;
        }

        var canonical = value.ToString("N");
        var version = canonical[12];
        var variant = canonical[16];

        return version == '7' && variant is '8' or '9' or 'a' or 'b';
    }
}
