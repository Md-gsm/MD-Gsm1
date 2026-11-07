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

    /// <summary>A work classification applies only to natural persons that hold the Colleague role.</summary>
    public static void ValidateWorkClassification(PartyType type, PartyBusinessRoles roles, WorkClassification? classification)
    {
        if (classification is null) return;
        if (!Enum.IsDefined(classification.Value))
            throw new ArgumentOutOfRangeException(nameof(classification));
        if (type != PartyType.NaturalPerson || !roles.HasFlag(PartyBusinessRoles.Colleague))
            throw new ArgumentException("Work classification is allowed only for natural persons with the Colleague role.", nameof(classification));
    }

    public static string RequireInternalNote(string? note)
    {
        var value = note?.Trim() ?? string.Empty;
        if (value.Length > 2000) throw new ArgumentException("Internal note cannot exceed 2000 characters.", nameof(note));
        return value;
    }
}
