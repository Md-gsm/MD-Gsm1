namespace MDGsm.Persons.Domain;

public sealed record PartyCommunicationPreferences(
    bool RepairStatusSmsEnabled,
    bool ReadyForDeliverySmsEnabled,
    bool PromotionsSmsEnabled)
{
    public static PartyCommunicationPreferences Default { get; } = new(true, true, false);
}
