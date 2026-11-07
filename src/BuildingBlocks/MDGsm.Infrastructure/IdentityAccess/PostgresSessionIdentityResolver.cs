using MDGsm.IdentityAccess.Application;
using MDGsm.IdentityAccess.Domain;
using MDGsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MDGsm.Infrastructure.IdentityAccess;

/// <summary>
/// Resolves bearer-token digests against PostgreSQL on every protected request.
/// No account ID, credential version, or session state is accepted from the client.
/// A must-change-password session is returned as a restricted identity so the API
/// can permit only the password-change operation until the requirement is cleared.
/// </summary>
public sealed class PostgresSessionIdentityResolver(MdGsmDbContext db) : ISessionIdentityResolver
{
    public async Task<ValidatedSessionIdentity?> ResolveAsync(
        byte[] tokenDigest, DateTimeOffset now, CancellationToken ct)
    {
        if (tokenDigest is not { Length: 32 }) return null;

        var match = await (
            from session in db.IdentitySessions.AsNoTracking()
            join account in db.IdentityAccounts.AsNoTracking()
                on session.AccountId equals account.AccountId
            where session.TokenDigest == tokenDigest
                && session.RevokedAtUtc == null
                && session.LeaseExpiresAtUtc > now
                && session.CredentialVersionAtIssue == account.CredentialVersion
                && account.IsEnabled
            select new { account.AccountId, account.CredentialVersion, account.MustChangePassword })
            .SingleOrDefaultAsync(ct);

        return match is null
            ? null
            : new ValidatedSessionIdentity(
                AccountId.From(match.AccountId), match.CredentialVersion, match.MustChangePassword);
    }
}
