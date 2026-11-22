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

public sealed record CurrentPrincipalDecision(
    PrincipalDecisionStatus Status,
    Guid? AccountId,
    string ContractVersion,
    string ReasonCode)
{
    public bool IsAuthenticated => Status == PrincipalDecisionStatus.Authenticated
        && AccountId is { } id
        && id != Guid.Empty;
}

public enum PermissionDecisionStatus
{
    Allowed = 1,
    Denied = 2,
    Unavailable = 3
}

public sealed record PermissionDecision(
    PermissionDecisionStatus Status,
    string PermissionKey,
    Guid? PrincipalAccountId,
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
    long? BindingVersion,
    string ContractVersion,
    string ReasonCode)
{
    public bool IsBoundOwner => Status == ProductOwnerAuthorityStatus.BoundOwner
        && PrincipalAccountId is { } id
        && id != Guid.Empty
        && BindingVersion is > 0;
}
