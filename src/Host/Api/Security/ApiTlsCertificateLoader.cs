using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace MDGsm.Api.Security;

/// <summary>
/// Loads one explicitly selected TLS server certificate from the machine certificate
/// store. It fails closed unless the certificate identity, key usage and trust chain
/// match the machine-local API network configuration.
/// </summary>
internal static class ApiTlsCertificateLoader
{
    private const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";
    private const string SubjectAlternativeNameOid = "2.5.29.17";

    public static X509Certificate2 LoadFromLocalMachine(
        string thumbprint,
        string expectedRootThumbprint,
        string expectedServerName,
        string expectedBindAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(thumbprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedRootThumbprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedServerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedBindAddress);

        var normalizedThumbprint = new string(thumbprint.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        var normalizedRootThumbprint = new string(expectedRootThumbprint.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        if (normalizedThumbprint.Length != 40 || normalizedRootThumbprint.Length != 40)
            throw new InvalidOperationException("The configured API or root certificate thumbprint is invalid.");

        if (!IPAddress.TryParse(expectedBindAddress, out var bindAddress) ||
            bindAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            throw new InvalidOperationException("The configured API TLS bind address is not a valid IPv4 address.");

        using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);
        var matches = store.Certificates.Find(
            X509FindType.FindByThumbprint, normalizedThumbprint, validOnly: false);

        if (matches.Count != 1)
            throw new InvalidOperationException(
                "The configured API TLS certificate is missing or ambiguous in LocalMachine\\My.");

        var certificate = matches[0];
        try
        {
            var now = DateTime.UtcNow;
            if (!certificate.HasPrivateKey ||
                now < certificate.NotBefore.ToUniversalTime() ||
                now >= certificate.NotAfter.ToUniversalTime())
                throw new InvalidOperationException(
                    "The configured API TLS certificate is not currently valid or its private key is unavailable.");

            using (var publicKey = certificate.GetRSAPublicKey())
            {
                if (publicKey is null || publicKey.KeySize < 3072)
                    throw new InvalidOperationException("The configured API TLS certificate must use an RSA-3072-or-stronger public key.");
            }

            var basicConstraints = certificate.Extensions
                .OfType<X509BasicConstraintsExtension>()
                .SingleOrDefault();
            if (basicConstraints?.CertificateAuthority == true)
                throw new InvalidOperationException("A CA certificate cannot be used as the API TLS server certificate.");

            var eku = certificate.Extensions
                .OfType<X509EnhancedKeyUsageExtension>()
                .SingleOrDefault();
            if (eku is null || !eku.EnhancedKeyUsages.Cast<Oid>()
                    .Any(oid => string.Equals(oid.Value, ServerAuthenticationOid, StringComparison.Ordinal)))
                throw new InvalidOperationException(
                    "The configured API TLS certificate does not permit TLS server authentication.");

            var keyUsage = certificate.Extensions
                .OfType<X509KeyUsageExtension>()
                .SingleOrDefault();
            if (keyUsage is null ||
                (keyUsage.KeyUsages & X509KeyUsageFlags.DigitalSignature) == 0)
                throw new InvalidOperationException(
                    "The configured API TLS certificate does not permit digital signatures.");

            if (!HasExpectedSubjectAlternativeName(certificate, expectedServerName, bindAddress))
                throw new InvalidOperationException(
                    "The configured API TLS certificate subject alternative name does not match the selected API server name.");

            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
            chain.ChainPolicy.VerificationTime = now;
            if (!chain.Build(certificate))
                throw new InvalidOperationException(
                    "The configured API TLS certificate does not chain to a trusted machine root certificate.");

            if (chain.ChainElements.Count < 2)
                throw new InvalidOperationException("The configured API TLS certificate chain does not contain its pinned root CA.");

            var rootCertificate = chain.ChainElements[^1].Certificate;
            var actualRootThumbprint = rootCertificate.Thumbprint;
            if (!string.Equals(actualRootThumbprint, normalizedRootThumbprint, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The configured API TLS certificate does not terminate at the pinned MD-Gsm root CA.");

            using (var rootPublicKey = rootCertificate.GetRSAPublicKey())
            {
                var rootConstraints = rootCertificate.Extensions
                    .OfType<X509BasicConstraintsExtension>()
                    .SingleOrDefault();
                var rootUsage = rootCertificate.Extensions
                    .OfType<X509KeyUsageExtension>()
                    .SingleOrDefault();
                if (rootPublicKey is null || rootPublicKey.KeySize < 3072 ||
                    rootConstraints?.CertificateAuthority != true ||
                    rootUsage is null ||
                    (rootUsage.KeyUsages & X509KeyUsageFlags.KeyCertSign) == 0)
                    throw new InvalidOperationException("The pinned MD-Gsm root CA does not meet the approved key and CA constraints.");
            }

            return certificate;
        }
        catch
        {
            certificate.Dispose();
            throw;
        }
    }

    private static bool HasExpectedSubjectAlternativeName(
        X509Certificate2 certificate,
        string expectedServerName,
        IPAddress bindAddress)
    {
        var sanExtensions = certificate.Extensions
            .OfType<X509Extension>()
            .Where(extension => string.Equals(
                extension.Oid?.Value, SubjectAlternativeNameOid, StringComparison.Ordinal))
            .ToArray();

        if (sanExtensions.Length != 1)
            return false;

        byte[] expected;
        if (IPAddress.TryParse(expectedServerName, out var expectedIp))
        {
            if (!expectedIp.Equals(bindAddress) ||
                expectedIp.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                return false;

            var addressBytes = expectedIp.GetAddressBytes();
            expected = new byte[] { 0x30, 0x06, 0x87, 0x04 }
                .Concat(addressBytes)
                .ToArray();
        }
        else
        {
            if (expectedServerName.Length is 0 or > 253 ||
                expectedServerName.Any(character => character > 0x7f) ||
                expectedServerName.Any(character =>
                    !(char.IsAsciiLetterOrDigit(character) || character is '-' or '.')))
                return false;

            var dnsBytes = Encoding.ASCII.GetBytes(expectedServerName);
            if (dnsBytes.Length > 253)
                return false;

            var sequenceContentLength = dnsBytes.Length + 2;
            var headerLength = sequenceContentLength < 128 ? 4 : 5;
            expected = new byte[dnsBytes.Length + headerLength];
            expected[0] = 0x30;
            if (sequenceContentLength < 128)
            {
                expected[1] = checked((byte)sequenceContentLength);
                expected[2] = 0x82;
                expected[3] = checked((byte)dnsBytes.Length);
            }
            else
            {
                expected[1] = 0x81;
                expected[2] = checked((byte)sequenceContentLength);
                expected[3] = 0x82;
                expected[4] = checked((byte)dnsBytes.Length);
            }
            Buffer.BlockCopy(dnsBytes, 0, expected, headerLength, dnsBytes.Length);
        }

        var actual = sanExtensions[0].RawData;
        return actual.Length == expected.Length &&
            CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
