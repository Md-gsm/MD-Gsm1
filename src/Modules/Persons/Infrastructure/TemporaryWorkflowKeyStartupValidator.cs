using System.Runtime.Versioning;
using System.Security.Cryptography;
using MDGsm.Persons.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MDGsm.Persons.Infrastructure;

/// <summary>
/// Verifies the configured DPAPI key under the actual API process identity before startup completes.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class TemporaryWorkflowKeyStartupValidator(
    WindowsDpapiTemporaryWorkflowEncryptionKeyProvider keyProvider,
    ILogger<TemporaryWorkflowKeyStartupValidator> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        keyProvider.ValidateKeyStorageForCurrentService();
        logger.LogInformation("Persons workflow key owner and DACL validation succeeded under the MDGsmApi service token.");

        var key = await keyProvider.GetKeyAsync(
            WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.KeyVersion,
            cancellationToken);
        try
        {
            if (key.Length != 32)
                throw new InvalidOperationException("The Persons workflow key has an invalid length.");
            logger.LogInformation("Persons workflow key startup validation succeeded for the configured Windows service identity.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
