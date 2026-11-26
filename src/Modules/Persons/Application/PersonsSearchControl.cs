namespace MDGsm.Persons.Application;

public enum PersonsSearchSecurityViolation
{
    PermissionDenied,
    ActorIdentityTampering,
    PurposeTampering,
    AuthorizationBypass
}

public enum PersonsSearchControlRejection
{
    None,
    RateLimited,
    ActorLocked
}

public sealed record PersonsSearchControlDecision(
    bool Allowed,
    PersonsSearchControlRejection Rejection,
    int RetryAfterSeconds)
{
    public static PersonsSearchControlDecision Permit() =>
        new(true, PersonsSearchControlRejection.None, 0);

    public static PersonsSearchControlDecision RateLimited(int retryAfterSeconds) =>
        new(false, PersonsSearchControlRejection.RateLimited, Math.Clamp(retryAfterSeconds, 1, 86400));

    public static PersonsSearchControlDecision Locked(int retryAfterSeconds) =>
        new(false, PersonsSearchControlRejection.ActorLocked, Math.Clamp(retryAfterSeconds, 1, 900));
}

public interface IPersonsSearchControl
{
    Task<PersonsSearchControlDecision> TryConsumeAsync(string actorId, string purpose, CancellationToken cancellationToken);
    Task RecordSecurityViolationAsync(string actorId, PersonsSearchSecurityViolation violation, string requestId, CancellationToken cancellationToken);
    Task AdminUnlockAsync(string actorId, string adminActorId, string reason, string requestId, CancellationToken cancellationToken);
}
