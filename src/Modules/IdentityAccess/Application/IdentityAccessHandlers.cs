using MDGsm.IdentityAccess.Domain;

namespace MDGsm.IdentityAccess.Application;

public enum LoginFailure
{
    None = 0,
    InvalidCredentials = 1,
    SourceRateLimited = 2,
    SessionCapacityReached = 3
}

public sealed record LoginCommand(string Username, string Password, Guid ClientInstanceId,
    string SourceDigest, string CorrelationId);

public sealed record LoginOutcome(bool Succeeded, LoginFailure Failure, string? AccessToken,
    DateTimeOffset? LeaseExpiresAtUtc, bool MustChangePassword)
{
    public static LoginOutcome Fail(LoginFailure reason) => new(false, reason, null, null, false);
}

public sealed class LoginHandler(
    IIdentityAccessUnitOfWork unitOfWork,
    IAccountStore accounts,
    IPasswordHasher passwordHasher,
    IAccessTokenFactory tokenFactory,
    IPrivacyDigest privacyDigest,
    ISessionStore sessions,
    ILoginAttemptStore attempts,
    ISystemPolicyStore policies,
    IAuditWriter audit,
    IDatabaseClock databaseClock)
{
    public Task<LoginOutcome> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return unitOfWork.ExecuteAsync(async ct =>
        {
            var now = await databaseClock.GetUtcNowAsync(ct);
            if (await attempts.IsSourceRateLimitedAsync(command.SourceDigest, now, ct))
                return LoginOutcome.Fail(LoginFailure.SourceRateLimited);

            // Malformed requests must still consume the source rate-limit budget and
            // perform dummy password work; otherwise empty passwords/client IDs could
            // bypass the same throttling applied to ordinary credential failures.
            if (string.IsNullOrEmpty(command.Password) || command.ClientInstanceId == Guid.Empty)
            {
                _ = passwordHasher.Verify(passwordHasher.DummyHash, command.Password ?? string.Empty);
                var invalidLoginDigest = Convert.ToHexString(
                    privacyDigest.ComputeSha256(command.Username ?? string.Empty));
                await attempts.RecordAsync(invalidLoginDigest, command.SourceDigest, "invalid", now, ct);
                await audit.AppendAsync("identity.login.failed", null, null,
                    "invalid_credentials", command.CorrelationId, ct);
                return LoginOutcome.Fail(LoginFailure.InvalidCredentials);
            }

            string normalized;
            try { normalized = Username.Parse(command.Username).NormalizedValue; }
            catch (Exception exception) when (exception is FormatException or ArgumentNullException)
            {
                _ = passwordHasher.Verify(passwordHasher.DummyHash, command.Password);
                await attempts.RecordAsync(Convert.ToHexString(privacyDigest.ComputeSha256(command.Username ?? "")),
                    command.SourceDigest, "invalid", now, ct);
                return LoginOutcome.Fail(LoginFailure.InvalidCredentials);
            }

            var loginDigest = Convert.ToHexString(privacyDigest.ComputeSha256(normalized));
            var account = await accounts.FindByNormalizedUsernameAsync(normalized, ct);
            if (account is null)
            {
                _ = passwordHasher.Verify(passwordHasher.DummyHash, command.Password);
                await attempts.RecordAsync(loginDigest, command.SourceDigest, "invalid", now, ct);
                await audit.AppendAsync("identity.login.failed", null, null, "invalid_credentials", command.CorrelationId, ct);
                return LoginOutcome.Fail(LoginFailure.InvalidCredentials);
            }

            // Equalize PBKDF2 work for known, unknown, disabled and locked accounts.
            var eligible = account.IsEnabled && !account.IsLocked(now);
            var candidateHash = eligible ? account.PasswordHash : passwordHasher.DummyHash;
            var passwordMatches = passwordHasher.Verify(candidateHash, command.Password);
            if (!eligible || !passwordMatches)
            {
                if (eligible)
                {
                    account.RecordFailedLogin(now);
                    await accounts.SaveAsync(account, ct);
                }
                await attempts.RecordAsync(loginDigest, command.SourceDigest, "invalid", now, ct);
                await audit.AppendAsync("identity.login.failed", null, account.Id, "invalid_credentials", command.CorrelationId, ct);
                return LoginOutcome.Fail(LoginFailure.InvalidCredentials);
            }

            var policy = (await policies.GetSessionPolicyAsync(ct)).Validate();
            var alreadyActive = await sessions.HasActiveSessionAsync(account.Id, now, ct);
            var activeAccounts = await sessions.CountDistinctActiveAccountsAsync(now, ct);
            if (!alreadyActive && activeAccounts >= policy.MaxActiveUsers)
            {
                await attempts.RecordAsync(loginDigest, command.SourceDigest, "capacity_rejected", now, ct);
                return LoginOutcome.Fail(LoginFailure.SessionCapacityReached);
            }

            var rawToken = tokenFactory.CreateOpaqueToken();
            if (string.IsNullOrWhiteSpace(rawToken))
                throw new InvalidOperationException("Token factory returned an empty token.");
            var digest = tokenFactory.ComputeDigest(rawToken);
            if (digest.Length != 32)
                throw new InvalidOperationException("Opaque token digest must be exactly 32 bytes.");

            var leaseExpiry = now.Add(policy.LeaseDuration);
            await sessions.CreateAsync(account.Id, digest, account.CredentialVersion,
                command.ClientInstanceId, now, leaseExpiry, ct);
            account.RecordSuccessfulLogin();
            await accounts.SaveAsync(account, ct);
            await attempts.RecordAsync(loginDigest, command.SourceDigest, "success", now, ct);
            await audit.AppendAsync("identity.login.succeeded", account.Id, account.Id, "success", command.CorrelationId, ct);
            return new LoginOutcome(true, LoginFailure.None, rawToken, leaseExpiry, account.MustChangePassword);
        }, cancellationToken);
    }
}

public sealed class ChangeOwnPasswordHandler(
    IIdentityAccessUnitOfWork unitOfWork, IAccountStore accounts, IPasswordHasher hasher,
    ISessionStore sessions, IAuditWriter audit, IDatabaseClock databaseClock)
{
    public Task<bool> HandleAsync(AccountId accountId, string currentPassword, string newPassword,
        string correlationId, CancellationToken ct)
    {
        if (!PasswordPolicy.IsValidChange(currentPassword, newPassword))
            return Task.FromResult(false);

        return unitOfWork.ExecuteAsync(async token =>
        {
            var account = await accounts.FindByIdAsync(accountId, token);
            if (account is null || !hasher.Verify(account.PasswordHash, currentPassword)) return false;
            var hash = hasher.Hash(newPassword);
            account.ChangePassword(hash, hasher.CurrentVersion);
            await accounts.SaveAsync(account, token);
            await sessions.RevokeAllForAccountAsync(accountId, await databaseClock.GetUtcNowAsync(token), "password_changed", token);
            await audit.AppendAsync("identity.password.changed", accountId, accountId, "success", correlationId, token);
            return true;
        }, ct);
    }
}

public sealed class AdminResetPasswordHandler(
    IIdentityAccessUnitOfWork unitOfWork, IAccountStore accounts, IPasswordHasher hasher,
    IRolePermissionStore permissions, ISessionStore sessions, IAuditWriter audit, IDatabaseClock databaseClock)
{
    public Task<bool> HandleAsync(AccountId administratorId, AccountId targetId, string temporaryPassword,
        string correlationId, CancellationToken ct)
    {
        if (!PasswordPolicy.IsValid(temporaryPassword))
            return Task.FromResult(false);

        return unitOfWork.ExecuteAsync(async token =>
        {
            if (!await permissions.HasPermissionAsync(administratorId, "identity.credentials.reset", token))
            {
                await audit.AppendAsync("identity.password.admin_reset", administratorId, targetId,
                    "denied_missing_permission", correlationId, token);
                return false;
            }
            var target = await accounts.FindByIdAsync(targetId, token);
            if (target is null) return false;
            if (await permissions.HasRoleAsync(targetId, RoleCatalog.SuperAdmin, token))
            {
                await audit.AppendAsync("identity.password.admin_reset", administratorId, targetId,
                    "denied_super_admin_recovery_required", correlationId, token);
                return false;
            }
            target.ChangePassword(hasher.Hash(temporaryPassword),
                hasher.CurrentVersion, mustChangePassword: true);
            await accounts.SaveAsync(target, token);
            await sessions.RevokeAllForAccountAsync(targetId, await databaseClock.GetUtcNowAsync(token), "administrator_password_reset", token);
            await audit.AppendAsync("identity.password.admin_reset", administratorId, targetId, "success", correlationId, token);
            return true;
        }, ct);
    }
}


public sealed class LogoutHandler(
    IIdentityAccessUnitOfWork unitOfWork,
    IAccessTokenFactory tokenFactory,
    ISessionIdentityResolver resolver,
    ISessionStore sessions,
    IAuditWriter audit,
    IDatabaseClock databaseClock)
{
    public Task HandleAsync(string accessToken, string correlationId, CancellationToken ct)
    {
        if (!IsOpaqueTokenShape(accessToken)) return Task.CompletedTask;
        var digest = tokenFactory.ComputeDigest(accessToken);
        if (digest is not { Length: 32 }) return Task.CompletedTask;

        return unitOfWork.ExecuteAsync(async token =>
        {
            var now = await databaseClock.GetUtcNowAsync(token);
            var identity = await resolver.ResolveAsync(digest, now, token);
            var revoked = await sessions.RevokeByDigestAsync(digest, now, "logout", token);
            if (revoked)
                await audit.AppendAsync("identity.session.logout", identity?.AccountId, identity?.AccountId,
                    "success", correlationId, token);
            return true;
        }, ct);
    }

    private static bool IsOpaqueTokenShape(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length != 43) return false;
        foreach (var c in token)
            if (!(c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'))
                return false;
        return true;
    }
}

public sealed class SessionHeartbeatHandler(
    ISystemPolicyStore policies, IAccessTokenFactory tokenFactory, ISessionStore sessions, IDatabaseClock databaseClock)
{
    public async Task<bool> HandleAsync(string accessToken, long credentialVersion, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return false;
        var policy = (await policies.GetSessionPolicyAsync(ct)).Validate();
        var now = await databaseClock.GetUtcNowAsync(ct);
        return await sessions.TryHeartbeatAsync(tokenFactory.ComputeDigest(accessToken), credentialVersion,
            now, now.Add(policy.LeaseDuration), ct);
    }
}
