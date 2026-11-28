namespace MDGsm.Infrastructure.IdentityAccess.Bootstrap;

/// <summary>
/// Process-wide immutable identifier for one API service process lifetime.
/// The static value is created once by CLR type initialization, so every
/// consumer in this process observes the same identifier even if it creates
/// another wrapper instance. A restarted process initializes a new value.
/// </summary>
public sealed class O46ServiceProcessIdentity
{
    private static readonly Guid ProcessServiceInstanceId = Guid.NewGuid();

    public Guid ServiceInstanceId => ProcessServiceInstanceId;
}
