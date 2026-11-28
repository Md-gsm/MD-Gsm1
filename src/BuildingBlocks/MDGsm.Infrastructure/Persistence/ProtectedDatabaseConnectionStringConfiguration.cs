using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace MDGsm.Infrastructure.Persistence;

/// <summary>
/// Loads the API's database connection string from a machine-protected file.
/// The file ACL must grant read access only to SYSTEM, Administrators and the
/// exact MDGsmApi service SID. DPAPI LocalMachine is not an ACL substitute.
/// </summary>
public static class ProtectedDatabaseConnectionStringConfiguration
{
    public const string ConnectionStringKey = "ConnectionStrings:MDGsmDatabase";

    public static IConfigurationBuilder AddProtectedDatabaseConnectionString(
        this IConfigurationBuilder builder,
        string protectedFilePath)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedFilePath);

        builder.Add(new ProtectedDatabaseConnectionStringSource(
            Path.GetFullPath(protectedFilePath)));
        return builder;
    }

    private sealed class ProtectedDatabaseConnectionStringSource(string path)
        : IConfigurationSource
    {
        public IConfigurationProvider Build(IConfigurationBuilder builder) =>
            new ProtectedDatabaseConnectionStringProvider(path);
    }

    private sealed class ProtectedDatabaseConnectionStringProvider(string path)
        : ConfigurationProvider
    {
        public override void Load()
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException(
                    "The protected API database connection file is Windows-only.");

            if (!File.Exists(path))
                throw new InvalidOperationException(
                    "The protected API database connection file is missing. Run the central-server provisioning repair.");

            byte[]? ciphertext = null;
            byte[]? plaintext = null;
            try
            {
                ciphertext = File.ReadAllBytes(path);
                if (ciphertext.Length is < 32 or > 16384)
                    throw new InvalidOperationException(
                        "The protected API database connection file has an invalid size.");

                plaintext = ProtectedData.Unprotect(
                    ciphertext, optionalEntropy: null, DataProtectionScope.LocalMachine);

                var connectionString = new UTF8Encoding(false, true).GetString(plaintext);
                var parsed = new NpgsqlConnectionStringBuilder(connectionString);

                // This product build uses a local PostgreSQL cluster behind the
                // central API. PostgreSQL is never a client-facing network service.
                if (parsed.Host is not ("127.0.0.1" or "::1" or "localhost")
                    || parsed.Port is < 1 or > 65535
                    || parsed.Database != "mdgsm"
                    || parsed.Username != "mdgsm_api_runtime"
                    || string.IsNullOrWhiteSpace(parsed.Password))
                {
                    throw new InvalidOperationException(
                        "The protected API database connection does not match the approved local runtime identity.");
                }

                Data[ConnectionStringKey] = connectionString;
            }
            catch (CryptographicException)
            {
                throw new InvalidOperationException(
                    "The protected API database connection cannot be decrypted by this service machine.");
            }
            finally
            {
                if (ciphertext is not null)
                    CryptographicOperations.ZeroMemory(ciphertext);
                if (plaintext is not null)
                    CryptographicOperations.ZeroMemory(plaintext);
            }
        }
    }
}
