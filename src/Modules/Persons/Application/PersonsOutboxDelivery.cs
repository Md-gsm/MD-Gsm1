namespace MDGsm.Persons.Application;

/// <summary>
/// Owner-approved PII-free source event contract. The first and only event type is PartyRegistered.
/// </summary>
public static class PersonsOutboxEventContract
{
    public const string PartyRegistered = "PartyRegistered";
    public const string PartyRegisteredVersion = "1.0";
}

/// <summary>
/// Optional causal metadata for a newly-created Party. It contains identifiers only and never business PII.
/// </summary>
public sealed record PartyRegisteredOutboxContext(
    Guid? CorrelationId = null,
    Guid? CausationId = null,
    DateTimeOffset? OccurredAtUtc = null);

/// <summary>Immutable, PII-free event envelope loaded from the Persons source Outbox.</summary>
public sealed record PersonsOutboxEvent(
    Guid EventId,
    string EventType,
    string ContractVersion,
    Guid AggregateId,
    string ActorReference,
    DateTimeOffset OccurredAtUtc,
    Guid? CorrelationId,
    Guid? CausationId,
    Guid IdempotencyKey);

/// <summary>An event leased for one at-least-once delivery attempt.</summary>
public sealed record LeasedPersonsOutboxEvent(PersonsOutboxEvent Event, int AttemptCount);

/// <summary>
/// Persistence port for the mutable delivery state. Event envelopes remain immutable.
/// Every completion/retry operation is fenced by the current lease owner and lease expiry.
/// </summary>
public interface IPersonsOutboxDeliveryStore
{
    Task<IReadOnlyList<LeasedPersonsOutboxEvent>> LeaseDueAsync(
        string leaseOwner,
        int batchSize,
        DateTimeOffset nowUtc,
        DateTimeOffset leaseExpiresAtUtc,
        CancellationToken cancellationToken);

    Task<bool> MarkDeliveredAsync(
        Guid eventId,
        string leaseOwner,
        DateTimeOffset deliveredAtUtc,
        CancellationToken cancellationToken);

    Task<bool> ScheduleRetryAsync(
        Guid eventId,
        string leaseOwner,
        DateTimeOffset nowUtc,
        DateTimeOffset nextAttemptAtUtc,
        string failureCode,
        CancellationToken cancellationToken);
}

/// <summary>
/// Destination adapter boundary. A concrete transport/consumer must be explicitly supplied by a host;
/// this module does not invent a broker, URL, credentials, or downstream consumer.
/// </summary>
public interface IPersonsOutboxPublisher
{
    Task PublishAsync(PersonsOutboxEvent message, CancellationToken cancellationToken);
}

public static class PersonsOutboxDeliveryFailureCodes
{
    public const string PublishFailed = "publish_failed";
    public const string LeaseExpired = "lease_expired";
}
