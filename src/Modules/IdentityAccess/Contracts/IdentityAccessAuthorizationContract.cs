namespace MDGsm.IdentityAccess.Contracts;

/// <summary>
/// Public, request-scoped IdentityAccess boundary for module consumers.
/// No caller-supplied AccountId, role, owner flag, token or session secret is accepted.
/// </summary>
public interface IIdentityAccessAuthorizationContract
{
    Task<CurrentPrincipalDecision> ResolveCurrentPrincipalAsync(CancellationToken cancellationToken);

    Task<PermissionDecision> EvaluateCurrentPermissionAsync(
        string permissionKey,
        CancellationToken cancellationToken);

    Task<ProductOwnerAuthorityDecision> ResolveCurrentProductOwnerAuthorityAsync(
        CancellationToken cancellationToken);
}

public enum PrincipalDecisionStatus
{
    Authenticated = 1,
    Unauthenticated = 2,
    Unavailable = 3
}

/// <summary>
/// Describes the server-validated request principal and the session version that established it.
/// PrincipalVersion is the current credential version at authentication time. Session and account
/// validity must be rechecked together with a permission query; this snapshot alone is not
/// commit-time authorization for a future mutating module.
/// </summary>
public sealed record CurrentPrincipalDecision(
    PrincipalDecisionStatus Status,
    Guid? AccountId,
    Guid? SessionId,
    long? PrincipalVersion,
    string? AccountState,
    string? SessionState,
    DateTimeOffset? AuthenticatedAtUtc,
    string ContractVersion,
    string ReasonCode)
{
    public bool IsAuthenticated =>
        Status == PrincipalDecisionStatus.Authenticated
        && AccountId is { } accountId && accountId != Guid.Empty
        && SessionId is { } sessionId && sessionId != Guid.Empty
        && PrincipalVersion is > 0
        && AccountState == "ENABLED"
        && SessionState == "ACTIVE"
        && AuthenticatedAtUtc is not null;
}

public enum PermissionDecisionStatus
{
    Allowed = 1,
    Denied = 2,
    Unavailable = 3
}

/// <summary>
/// The decision is evaluated against the current server-side permission grant and a live,
/// non-revoked session in one IdentityAccess persistence statement.
/// </summary>
public sealed record PermissionDecision(
    PermissionDecisionStatus Status,
    string PermissionKey,
    Guid? PrincipalAccountId,
    Guid? PrincipalSessionId,
    long? PrincipalVersion,
    string ContractVersion,
    string ReasonCode)
{
    public bool IsAllowed => Status == PermissionDecisionStatus.Allowed;
}

public enum ProductOwnerAuthorityStatus
{
    BoundOwner = 1,
    NotOwner = 2,
    Unavailable = 3
}

/// <summary>
/// Product Owner authority is a separately protected server-side binding. It is not a role,
/// permission, caller-supplied flag, username, email, GitHub identity or AccountId chosen by
/// the caller. BindingVersion is required when a trusted binding is active.
/// </summary>
public sealed record ProductOwnerAuthorityDecision(
    ProductOwnerAuthorityStatus Status,
    Guid? PrincipalAccountId,
    Guid? PrincipalSessionId,
    long? BindingVersion,
    string ContractVersion,
    string ReasonCode)
{
    public bool IsBoundOwner =>
        Status == ProductOwnerAuthorityStatus.BoundOwner
        && PrincipalAccountId is { } accountId && accountId != Guid.Empty
        && PrincipalSessionId is { } sessionId && sessionId != Guid.Empty
        && BindingVersion is > 0;
}
