namespace MDGsm.Infrastructure.IdentityAccess.Bootstrap;

/// <summary>
/// Immutable identifier created once by the API host's DI singleton for the
/// current Windows service process. A new process constructs a new instance and
/// therefore receives a new ServiceInstanceId; callers cannot supply or restore
/// a prior value.
/// </summary>
public sealed class O46ServiceProcessIdentity
{
    public Guid ServiceInstanceId { get; } = Guid.NewGuid();
}
