using System.Security.Cryptography;
using System.Text;
using MDGsm.Application.Persons;
using MDGsm.Domain.Persons;
using MDGsm.Infrastructure.Persons;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class TemporaryWorkflowPromotionTests
{
    [Fact]
    public void Payload_parser_accepts_versioned_natural_party_and_returns_only_masked_mobile_in_preview()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "partyType": "NaturalPerson",
              "roles": ["Customer", "Colleague"],
              "givenName": "سارا",
              "familyName": "محمدی",
              "nationalId": "1234567890",
              "primaryMobile": "09125550123",
              "acquaintanceSource": "WalkIn",
              "communication": {
                "repairStatusSmsEnabled": true,
                "readyForDeliverySmsEnabled": true,
                "promotionsSmsEnabled": false
              },
              "workClassification": "Technician"
            }
            """;

        var decoded = TemporaryWorkflowPayloadParser.Parse(Encoding.UTF8.GetBytes(json));

        Assert.Equal(1, decoded.SchemaVersion);
        Assert.Equal(PartyType.NaturalPerson, decoded.PartyType);
        var registration = Assert.IsType<RegisterNaturalParty>(decoded.Registration);
        Assert.Equal("سارا", registration.GivenName);
        Assert.Equal(PartyBusinessRoles.Customer | PartyBusinessRoles.Colleague, registration.Roles);
        Assert.True(decoded.HasNationalIdentifier);
        Assert.False(decoded.HasAddress);
        Assert.NotNull(decoded.MaskedPrimaryMobile);
        Assert.DoesNotContain("09125550123", decoded.MaskedPrimaryMobile, StringComparison.Ordinal);
    }

    [Fact]
    public void Payload_parser_accepts_legal_party_without_natural_identity_fields()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "partyType": "LegalEntity",
              "roles": ["Customer"],
              "legalName": "شرکت نمونه",
              "legalNationalId": "12345678901",
              "economicCode": "1234567890",
              "primaryMobile": "09125550124"
            }
            """;

        var decoded = TemporaryWorkflowPayloadParser.Parse(Encoding.UTF8.GetBytes(json));

        Assert.Equal(PartyType.LegalPerson, decoded.PartyType);
        Assert.IsType<RegisterLegalParty>(decoded.Registration);
        Assert.True(decoded.HasNationalIdentifier);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":2,\"partyType\":\"NaturalPerson\",\"roles\":[\"Customer\"]}")]
    [InlineData("{\"schemaVersion\":1,\"partyType\":\"Unknown\",\"roles\":[\"Customer\"]}")]
    [InlineData("{\"schemaVersion\":1,\"partyType\":\"NaturalPerson\",\"roles\":[\"Admin\"]}")]
    [InlineData("{\"schemaVersion\":1,\"partyType\":\"NaturalPerson\",\"roles\":[\"Customer\"],\"givenName\":\"A\",\"familyName\":\"B\",\"legalName\":\"forbidden\"}")]
    [InlineData("{\"schemaVersion\":1,\"partyType\":\"NaturalPerson\",\"roles\":[\"Customer\"],\"givenName\":\"A\",\"familyName\":\"B\",\"unknownPii\":\"must-not-be-accepted\"}")]
    public void Payload_parser_rejects_unknown_or_mixed_contract_data_without_echoing_payload(string json)
    {
        var exception = Assert.Throws<TemporaryWorkflowPayloadException>(
            () => TemporaryWorkflowPayloadParser.Parse(Encoding.UTF8.GetBytes(json)));

        Assert.DoesNotContain("must-not-be-accepted", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("unknownPii", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AesGcm_payload_protector_authenticates_ciphertext_and_binds_workflow_and_key_version()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var provider = new TestKeyProvider(key);
        var protector = new AesGcmTemporaryWorkflowPayloadProtector(provider);
        var workflowId = Guid.CreateVersion7();
        const string keyVersion = "test-v1";
        var plaintext = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"partyType\":\"NaturalPerson\",\"roles\":[\"Customer\"]}");
        var ciphertext = Encrypt(workflowId, keyVersion, key, plaintext);

        var decoded = await protector.UnprotectAsync(workflowId, keyVersion, ciphertext, default);
        Assert.Equal(plaintext, decoded);

        await Assert.ThrowsAsync<TemporaryWorkflowPayloadException>(() =>
            protector.UnprotectAsync(Guid.CreateVersion7(), keyVersion, ciphertext, default));

        var tampered = ciphertext.ToArray();
        tampered[^1] ^= 0x01;
        await Assert.ThrowsAsync<TemporaryWorkflowPayloadException>(() =>
            protector.UnprotectAsync(workflowId, keyVersion, tampered, default));
    }

    private static byte[] Encrypt(Guid workflowId, string keyVersion, byte[] key, byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var encrypted = new byte[plaintext.Length];
        var aad = AesGcmTemporaryWorkflowPayloadProtector.CreateAssociatedData(workflowId, keyVersion);
        using (var aes = new AesGcm(key, tagSizeInBytes: 16))
            aes.Encrypt(nonce, plaintext, encrypted, tag, aad);
        CryptographicOperations.ZeroMemory(aad);
        return [.. nonce, .. tag, .. encrypted];
    }

    private sealed class TestKeyProvider(byte[] key) : ITemporaryWorkflowEncryptionKeyProvider
    {
        public ValueTask<byte[]> GetKeyAsync(string keyVersion, CancellationToken cancellationToken) =>
            ValueTask.FromResult(key.ToArray());
    }
}
