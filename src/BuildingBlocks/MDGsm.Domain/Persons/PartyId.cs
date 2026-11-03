namespace MDGsm.Domain.Persons;

/// <summary>
/// Stable UUIDv7 identity shared by natural persons and legal entities.
/// The identifier contains no business meaning or personal information.
/// </summary>
public readonly record struct PartyId
{
    private PartyId(Guid value) => Value = value;

    public Guid Value { get; }

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

        if (!Guid.TryParse(value, out var parsed) || !IsUuidV7(parsed))
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
