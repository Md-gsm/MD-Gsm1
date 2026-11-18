namespace MDGsm.IdentityAccess.Contracts;
public sealed record LoginRequest(string Username,string Password,Guid ClientInstanceId);
public sealed record LoginResponse(string AccessToken,DateTimeOffset ExpiresAtUtc,bool MustChangePassword);
public sealed record ChangePasswordRequest(string CurrentPassword,string NewPassword);
public sealed record AdminResetPasswordRequest(Guid AccountId,string TemporaryPassword);
public sealed record SessionSummary(Guid SessionId,DateTimeOffset CreatedAtUtc,DateTimeOffset LastSeenAtUtc,DateTimeOffset LeaseExpiresAtUtc);
