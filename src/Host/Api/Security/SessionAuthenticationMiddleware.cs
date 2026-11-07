using System.Net.Http.Headers;
using System.Security.Claims;
using MDGsm.IdentityAccess.Application;
using Microsoft.AspNetCore.Http;

namespace MDGsm.Api.Security;

/// <summary>
/// Authenticates protected API requests using only an opaque Bearer token.
/// The token is digested and resolved against PostgreSQL; client-supplied actor IDs are ignored.
/// Sessions requiring a password change are restricted to the password-change endpoint.
/// </summary>
public sealed class SessionAuthenticationMiddleware(RequestDelegate next)
{
    private const string SessionAuthenticationType = "MDGsm.Session";
    private const string PersonsPrefix = "/api/v2/persons";
    private const string PasswordChangePath = "/api/v1/identity/password/change";

    public async Task InvokeAsync(HttpContext context)
    {
        var isPersonsRequest = context.Request.Path.StartsWithSegments(PersonsPrefix, StringComparison.OrdinalIgnoreCase);
        var isPasswordChangeRequest = string.Equals(context.Request.Path.Value, PasswordChangePath, StringComparison.OrdinalIgnoreCase);
        if (!isPersonsRequest && !isPasswordChangeRequest)
        {
            await next(context);
            return;
        }

        var header = context.Request.Headers.Authorization.ToString();
        if (!AuthenticationHeaderValue.TryParse(header, out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || !IsOpaqueTokenShape(authorization.Parameter))
        {
            await Unauthorized(context);
            return;
        }

        var tokenFactory = context.RequestServices.GetService<IAccessTokenFactory>();
        var resolver = context.RequestServices.GetService<ISessionIdentityResolver>();
        var sessions = context.RequestServices.GetService<ISessionStore>();
        var policies = context.RequestServices.GetService<ISystemPolicyStore>();
        var clock = context.RequestServices.GetService<TimeProvider>();
        if (tokenFactory is null || resolver is null || sessions is null || policies is null || clock is null)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        var now = clock.GetUtcNow();
        var digest = tokenFactory.ComputeDigest(authorization.Parameter!);
        if (digest is not { Length: 32 })
        {
            await Unauthorized(context);
            return;
        }

        var identity = await resolver.ResolveAsync(digest, now, context.RequestAborted);
        if (identity is null)
        {
            await Unauthorized(context);
            return;
        }

        if (identity.MustChangePassword && !isPasswordChangeRequest)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new { error = "password_change_required" }, context.RequestAborted);
            return;
        }

        var policy = (await policies.GetSessionPolicyAsync(context.RequestAborted)).Validate();
        var renewed = await sessions.TryHeartbeatAsync(
            digest, identity.CredentialVersion, now, now.Add(policy.LeaseDuration), context.RequestAborted);
        if (!renewed)
        {
            await Unauthorized(context);
            return;
        }

        var accountId = identity.AccountId.Value.ToString("D");
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, accountId),
            new Claim("sub", accountId)
        };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, SessionAuthenticationType));
        await next(context);
    }

    private static bool IsOpaqueTokenShape(string? token)
    {
        if (token is null || token.Length != 43) return false;
        foreach (var c in token)
            if (!(c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'))
                return false;
        return true;
    }

    private static Task Unauthorized(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return context.Response.WriteAsJsonAsync(new { error = "unauthorized" }, context.RequestAborted);
    }
}
