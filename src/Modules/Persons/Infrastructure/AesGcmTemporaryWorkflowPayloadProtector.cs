using System.Security.Cryptography;
using System.Text;
using MDGsm.Persons.Application;

namespace MDGsm.Persons.Infrastructure;

/// <summary>
/// AES-256-GCM adapter for temporary workflow payloads. Key custody is deliberately
/// delegated to an injected provider; this repository does not select or persist a key source.
/// Ciphertext wire format is nonce(12) || tag(16) || ciphertext.
/// </summary>
public sealed class AesGcmTemporaryWorkflowPayloadProtector(
    ITemporaryWorkflowEncryptionKeyProvider keyProvider) : ITemporaryWorkflowPayloadProtector
{
    public async Task<byte[]> UnprotectAsync(
        Guid workflowRecordId, string keyVersion, ReadOnlyMemory<byte> ciphertext,
        CancellationToken cancellationToken)
    {
        if (workflowRecordId == Guid.Empty || string.IsNullOrWhiteSpace(keyVersion) || ciphertext.Length < 28)
            throw new TemporaryWorkflowPayloadException();

        var key = await keyProvider.GetKeyAsync(keyVersion, cancellationToken);
        if (key.Length != 32)
        {
            CryptographicOperations.ZeroMemory(key);
            throw new TemporaryWorkflowPayloadException();
        }

        var nonce = ciphertext.Slice(0, 12);
        var tag = ciphertext.Slice(12, 16);
        var encrypted = ciphertext.Slice(28);
        var plaintext = new byte[encrypted.Length];
        var associatedData = CreateAssociatedData(workflowRecordId, keyVersion);
        try
        {
            using var aes = new AesGcm(key, tagSizeInBytes: 16);
            aes.Decrypt(nonce.Span, encrypted.Span, tag.Span, plaintext, associatedData);
            return plaintext;
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new TemporaryWorkflowPayloadException();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(associatedData);
        }
    }

    public static byte[] CreateAssociatedData(Guid workflowRecordId, string keyVersion) =>
        Encoding.UTF8.GetBytes($"MD-Gsm/Persons/TemporaryWorkflow/v1/{workflowRecordId:D}/{keyVersion}");
}
