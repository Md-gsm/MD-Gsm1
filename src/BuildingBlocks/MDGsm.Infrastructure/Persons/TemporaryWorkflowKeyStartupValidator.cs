using System.Security.Cryptography;
using MDGsm.Application.Persons;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MDGsm.Infrastructure.Persons;

/// <summary>
/// Verifies the configured DPAPI key under the actual API process identity before startup completes.
/// </summary>
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
