using System.Globalization;
using System.Security.Claims;

namespace MDGsm.Api.Security;

/// <summary>
/// Exposes only request-scoped identity facts installed by SessionAuthenticationMiddleware.
/// These claims are not accepted from client headers or request data.
/// </summary>
public interface IAuthenticatedActorContext
{
    Guid AccountId { get; }
    Guid SessionId { get; }
    long CredentialVersion { get; }
    DateTimeOffset AuthenticatedAtUtc { get; }
}

public sealed class HttpAuthenticatedActorContext(IHttpContextAccessor accessor) : IAuthenticatedActorContext
{
    private ClaimsPrincipal Principal
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true
                || !string.Equals(principal.Identity.AuthenticationType, "MDGsm.Session", StringComparison.Ordinal))
                throw new UnauthorizedAccessException("A validated server-side session is required.");
            return principal;
        }
    }

    public Guid AccountId => ReadGuid(ClaimTypes.NameIdentifier);
    public Guid SessionId => ReadGuid("mdgsm:session_id");

    public long CredentialVersion
    {
        get
        {
            var raw = Principal.FindFirstValue("mdgsm:credential_version");
            if (!long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version <= 0)
                throw new UnauthorizedAccessException("The validated session credential version is missing.");
            return version;
        }
    }

    public DateTimeOffset AuthenticatedAtUtc
    {
        get
        {
            var raw = Principal.FindFirstValue("mdgsm:authenticated_at_utc");
            if (!DateTimeOffset.TryParseExact(raw, "O", CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var value))
                throw new UnauthorizedAccessException("The validated session timestamp is missing.");
            return value.ToUniversalTime();
        }
    }

    private Guid ReadGuid(string claimType)
    {
        var value = Principal.FindFirstValue(claimType);
        if (!Guid.TryParseExact(value, "D", out var id) || id == Guid.Empty)
            throw new UnauthorizedAccessException("A validated server-side session is required.");
        return id;
    }
}
