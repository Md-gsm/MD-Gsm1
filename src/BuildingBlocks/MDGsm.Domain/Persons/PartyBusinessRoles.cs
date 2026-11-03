namespace MDGsm.Domain.Persons;

/// <summary>
/// Commercial relationship flags. One party may be both a customer and a colleague.
/// Company is an identity form (LegalPerson), not a commercial role.
/// </summary>
[Flags]
public enum PartyBusinessRoles
{
    None = 0,
    Customer = 1,
    Colleague = 2
}
