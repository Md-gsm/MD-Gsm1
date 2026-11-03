namespace MDGsm.Domain.Persons;

public sealed record PartyCommunicationPreferences(
    bool RepairStatusSmsEnabled,
    bool ReadyForDeliverySmsEnabled,
    bool PromotionsSmsEnabled)
{
    public static PartyCommunicationPreferences Default { get; } = new(true, true, false);
}
