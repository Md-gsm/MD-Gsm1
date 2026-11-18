using System.Security.Cryptography;
using System.Text;
using MDGsm.IdentityAccess.Application;
using Microsoft.Extensions.Configuration;

namespace MDGsm.Infrastructure.IdentityAccess;

public sealed class HmacPrivacyDigest : IPrivacyDigest
{
    public const string ConfigurationKey = "IdentityAccess:PrivacyHmacKey";
    private readonly byte[] _key;

    public HmacPrivacyDigest(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var encoded = configuration[ConfigurationKey];
        if (string.IsNullOrWhiteSpace(encoded))
            throw new InvalidOperationException("Required privacy digest secret is missing.");
        try { _key = Convert.FromBase64String(encoded); }
        catch (FormatException ex) { throw new InvalidOperationException("Privacy digest secret must be Base64.", ex); }
        if (_key.Length < 32)
        {
            CryptographicOperations.ZeroMemory(_key);
            throw new InvalidOperationException("Privacy digest secret must contain at least 32 bytes.");
        }
    }

    public byte[] ComputeSha256(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(value));
    }
}