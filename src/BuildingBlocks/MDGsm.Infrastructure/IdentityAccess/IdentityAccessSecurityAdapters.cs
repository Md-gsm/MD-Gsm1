using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.WebUtilities;

namespace MDGsm.Infrastructure.IdentityAccess;

internal sealed class PasswordHashSubject
{
    public static PasswordHashSubject Instance { get; } = new();
    private PasswordHashSubject() { }
}

/// <summary>Uses the platform ASP.NET Core Identity v3 PBKDF2 password hasher.</summary>
public sealed class PlatformPasswordHasher : MDGsm.IdentityAccess.Application.IPasswordHasher
{
    private readonly PasswordHasher<PasswordHashSubject> _hasher =
        new(Options.Create(new PasswordHasherOptions { CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3 }));

    public short CurrentVersion => 1;
    public string DummyHash { get; }

    public PlatformPasswordHasher()
    {
        DummyHash = _hasher.HashPassword(PasswordHashSubject.Instance, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return _hasher.HashPassword(PasswordHashSubject.Instance, password);
    }

    public bool Verify(string passwordHash, string password)
    {
        if (string.IsNullOrEmpty(passwordHash) || password is null) return false;
        var result = _hasher.VerifyHashedPassword(PasswordHashSubject.Instance, passwordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}

/// <summary>Issues 256-bit opaque bearer tokens and stores only SHA-256 digests.</summary>
public sealed class OpaqueAccessTokenFactory : MDGsm.IdentityAccess.Application.IAccessTokenFactory
{
    public string CreateOpaqueToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public byte[] ComputeDigest(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }

    public byte[] ComputeSha256(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return SHA256.HashData(Encoding.UTF8.GetBytes(value));
    }
}
