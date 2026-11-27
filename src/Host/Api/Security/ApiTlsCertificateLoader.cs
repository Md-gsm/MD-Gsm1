using System.Security.Cryptography.X509Certificates;

namespace MDGsm.Api.Security;

/// <summary>
/// Loads one explicitly selected TLS server certificate from the machine certificate
/// store. The API never accepts a certificate path/password from appsettings or a client.
/// </summary>
internal static class ApiTlsCertificateLoader
{
    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

    public static X509Certificate2 LoadFromLocalMachine(string thumbprint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbprint);
        var normalizedThumbprint = new string(thumbprint.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        if (normalizedThumbprint.Length != 40)
            throw new InvalidOperationException("The configured API TLS certificate thumbprint is invalid.");

        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        var matches = store.Certificates.Find(
            X509FindType.FindByThumbprint, normalizedThumbprint, validOnly: false);

        if (matches.Count != 1)
            throw new InvalidOperationException(
                "The configured API TLS certificate is missing or ambiguous in LocalMachine\\My.");

        var certificate = matches[0];
        var now = DateTime.UtcNow;
        if (!certificate.HasPrivateKey ||
            now < certificate.NotBefore.ToUniversalTime() ||
            now >= certificate.NotAfter.ToUniversalTime())
        {
            certificate.Dispose();
            throw new InvalidOperationException(
                "The configured API TLS certificate is not currently valid or its private key is unavailable.");
        }

        var basicConstraints = certificate.Extensions
            .OfType<X509BasicConstraintsExtension>()
            .SingleOrDefault();
        if (basicConstraints?.CertificateAuthority == true)
        {
            certificate.Dispose();
            throw new InvalidOperationException("A CA certificate cannot be used as the API TLS server certificate.");
        }

        var eku = certificate.Extensions
            .OfType<X509EnhancedKeyUsageExtension>()
            .SingleOrDefault();
        if (eku is null || !eku.EnhancedKeyUsages.Cast<Oid>()
                .Any(oid => string.Equals(oid.Value, ServerAuthenticationOid, StringComparison.Ordinal)))
        {
            certificate.Dispose();
            throw new InvalidOperationException(
                "The configured API TLS certificate does not permit TLS server authentication.");
        }

        var keyUsage = certificate.Extensions
            .OfType<X509KeyUsageExtension>()
            .SingleOrDefault();
        if (keyUsage is null ||
            (keyUsage.KeyUsages & X509KeyUsageFlags.DigitalSignature) == 0)
        {
            certificate.Dispose();
            throw new InvalidOperationException(
                "The configured API TLS certificate does not permit digital signatures.");
        }

        return certificate;
    }
}
