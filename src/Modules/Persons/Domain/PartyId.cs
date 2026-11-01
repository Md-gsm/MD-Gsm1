using System.Diagnostics.CodeAnalysis;

namespace MDGsm.Persons.Domain;

/// <summary>
/// Opaque, globally unique identifier shared by natural persons and legal entities.
/// The domain representation is UUIDv7; public contracts serialize it as a canonical UUID string.
/// </summary>
public sealed record PartyId
{
    private PartyId(Guid value)
    {
        if (!IsUuidVersion7(value))
        {
            throw new ArgumentException("PartyId must be a UUIDv7 value.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static PartyId New() => new(Guid.CreateVersion7());

    public static PartyId From(Guid value) => new(value);

    public static PartyId Parse(string value)
    {
        if (!TryParse(value, out var partyId))
        {
            throw new FormatException("The supplied value is not a valid UUIDv7 PartyId.");
        }

        return partyId;
    }

    public static bool TryParse(string? value, [NotNullWhen(true)] out PartyId? partyId)
    {
        if (Guid.TryParseExact(value, "D", out var parsed)
            && string.Equals(value, parsed.ToString("D"), StringComparison.Ordinal)
            && IsUuidVersion7(parsed))
        {
            partyId = new PartyId(parsed);
            return true;
        }

        partyId = null;
        return false;
    }

    public override string ToString() => Value.ToString("D");

    private static bool IsUuidVersion7(Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        if (!value.TryWriteBytes(bytes, bigEndian: true, out var bytesWritten) || bytesWritten != 16)
        {
            return false;
        }

        var versionIsSeven = (bytes[6] & 0xF0) == 0x70;
        var variantIsRfc4122 = (bytes[8] & 0xC0) == 0x80;
        return versionIsSeven && variantIsRfc4122;
    }
}
