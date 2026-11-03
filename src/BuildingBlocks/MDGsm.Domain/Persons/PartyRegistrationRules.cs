namespace MDGsm.Domain.Persons;

/// <summary>Cross-field validation shared by registration workflows.</summary>
public static class PartyRegistrationRules
{
    public static void ValidateRoles(PartyBusinessRoles roles)
    {
        const PartyBusinessRoles allowed = PartyBusinessRoles.Customer | PartyBusinessRoles.Colleague;
        if (roles == PartyBusinessRoles.None || (roles & ~allowed) != 0)
            throw new ArgumentOutOfRangeException(nameof(roles), "At least one supported role is required.");
    }

    public static string RequireInternalNote(string? note)
    {
        var value = note?.Trim() ?? string.Empty;
        if (value.Length > 2000) throw new ArgumentException("Internal note cannot exceed 2000 characters.", nameof(note));
        return value;
    }
}
