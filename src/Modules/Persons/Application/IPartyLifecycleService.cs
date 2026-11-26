namespace MDGsm.Persons.Application;

public enum PartyLifecycleChangeStatus
{
    Applied,
    NotFound,
    VersionConflict,
    PartyInactiveOrMerged
}

public sealed record PartyLifecycleChangeResult(
    PartyLifecycleChangeStatus Status,
    long? CurrentRowVersion)
{
    public bool Applied => Status == PartyLifecycleChangeStatus.Applied;
}

public interface IPartyLifecycleService
{
    Task<PartyLifecycleChangeResult> SetPartyActiveAsync(
        Guid partyId, bool isActive, long expectedRowVersion, string actorId, string reason,
        CancellationToken cancellationToken);

    Task<PartyLifecycleChangeResult> SetContactPointActiveAsync(
        Guid contactPointId, bool isActive, long expectedRowVersion, string actorId, string reason,
        CancellationToken cancellationToken);

    Task<PartyLifecycleChangeResult> UpdateContactPointValueAsync(
        Guid contactPointId, string value, long expectedRowVersion, string actorId, string reason,
        CancellationToken cancellationToken);

    Task<PartyLifecycleChangeResult> SetAddressActiveAsync(
        Guid addressId, bool isActive, long expectedRowVersion, string actorId, string reason,
        CancellationToken cancellationToken);

    Task<PartyLifecycleChangeResult> UpdateAddressAsync(
        Guid addressId, string countryCode, string province, string city, string postalCode, string addressLine,
        long expectedRowVersion, string actorId, string reason, CancellationToken cancellationToken);
}
