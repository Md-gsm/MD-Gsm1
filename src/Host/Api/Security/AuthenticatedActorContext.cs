using System.Security.Claims;

namespace MDGsm.Api.Security;

/// <summary>Exposes only the account identity installed by SessionAuthenticationMiddleware.</summary>
public interface IAuthenticatedActorContext
{
    Guid AccountId { get; }
}

public sealed class HttpAuthenticatedActorContext(IHttpContextAccessor accessor) : IAuthenticatedActorContext
{
    public Guid AccountId
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            var identity = principal?.Identity;
            var value = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (identity?.IsAuthenticated != true
                || !string.Equals(identity.AuthenticationType, "MDGsm.Session", StringComparison.Ordinal)
                || !Guid.TryParseExact(value, "D", out var accountId)
                || accountId == Guid.Empty)
                throw new UnauthorizedAccessException("A validated server-side session is required.");
            return accountId;
        }
    }
}
