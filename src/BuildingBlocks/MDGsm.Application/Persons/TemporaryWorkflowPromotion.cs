using System.Text.Json;
using System.Text.Json.Serialization;
using MDGsm.Domain.Persons;

namespace MDGsm.Application.Persons;

public interface ITemporaryWorkflowPromotionService
{
    Task<TemporaryWorkflowPromotionPreview> PreviewAsync(
        Guid workflowRecordId, Guid actorId, Guid? correlationId, CancellationToken cancellationToken);

    Task<TemporaryWorkflowPromotionResult> PromoteAsync(
        TemporaryWorkflowPromotionCommand command, CancellationToken cancellationToken);
}

public interface ITemporaryWorkflowPayloadProtector
{
    Task<byte[]> UnprotectAsync(
        Guid workflowRecordId, string keyVersion, ReadOnlyMemory<byte> ciphertext,
        CancellationToken cancellationToken);
}

public interface ITemporaryWorkflowEncryptionKeyProvider
{
    ValueTask<ReadOnlyMemory<byte>> GetKeyAsync(string keyVersion, CancellationToken cancellationToken);
}

public interface IPersonsWorkflowPromotionAuthorizer
{
    Task<bool> HasPermissionAsync(Guid actorId, string permissionKey, CancellationToken cancellationToken);
}

public sealed record TemporaryWorkflowPromotionCommand(
    Guid WorkflowRecordId,
    Guid OperationId,
    Guid ActorId,
    string? SelectedPartyId,
    bool CreateNewParty,
    bool ConfirmDistinctParty,
    Guid? CorrelationId = null);

public enum TemporaryWorkflowPromotionOutcome
{
    Created = 1,
    Existing = 2,
    DuplicateReviewRequired = 3,
    Replayed = 4
}

public sealed record TemporaryWorkflowPromotionResult(
    TemporaryWorkflowPromotionOutcome Outcome,
    string? PartyId,
    bool Created,
    bool Replayed,
    IReadOnlyList<PartyQuickRegistrationCandidate> Candidates);

public sealed record TemporaryWorkflowPromotionPreview(
    Guid WorkflowRecordId,
    int PayloadSchemaVersion,
    string PartyType,
    string DisplayName,
    string? MaskedPrimaryMobile,
    bool HasNationalIdentifier,
    bool HasAddress,
    DateTimeOffset ExpiresAtUtc);

public sealed class TemporaryWorkflowUnavailableException()
    : Exception("The temporary workflow is not available for promotion.");

public sealed class TemporaryWorkflowOwnershipException()
    : Exception("The authenticated actor does not own this temporary workflow.");

public sealed class TemporaryWorkflowAlreadyPromotedException()
    : Exception("The temporary workflow has already been promoted by another operation.");

public sealed class TemporaryWorkflowPayloadException()
    : Exception("The temporary workflow payload is invalid or cannot be authenticated.");

public sealed record TemporaryWorkflowDecodedParty(
    int SchemaVersion,
    PartyType PartyType,
    IPartyRegistrationCommon Registration,
    string DisplayName,
    string? MaskedPrimaryMobile,
    bool HasNationalIdentifier,
    bool HasAddress);

public sealed record TemporaryWorkflowPartyPayload
{
    public int SchemaVersion { get; init; }
    public string PartyType { get; init; } = string.Empty;
    public string[] Roles { get; init; } = [];
    public string? GivenName { get; init; }
    public string? FamilyName { get; init; }
    public string? LegalName { get; init; }
    public string? NationalId { get; init; }
    public string? LegalNationalId { get; init; }
    public string? EconomicCode { get; init; }
    public string? ResponsiblePersonName { get; init; }
    public string? PrimaryMobile { get; init; }
    public string? AlternateMobile { get; init; }
    public string? Landline { get; init; }
    public string? Email { get; init; }
    public TemporaryWorkflowAddressPayload? Address { get; init; }
    public string? AcquaintanceSource { get; init; }
    public string? InternalNote { get; init; }
    public TemporaryWorkflowCommunicationPayload? Communication { get; init; }
    public string? WorkClassification { get; init; }
}

public sealed record TemporaryWorkflowAddressPayload(
    string? CountryCode,
    string? Province,
    string? City,
    string? PostalCode,
    string? AddressLine);

public sealed record TemporaryWorkflowCommunicationPayload(
    bool? RepairStatusSmsEnabled,
    bool? ReadyForDeliverySmsEnabled,
    bool? PromotionsSmsEnabled);

public static class TemporaryWorkflowPayloadParser
{
    private const int MaxPayloadBytes = 64 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow
    };

    public static TemporaryWorkflowDecodedParty Parse(ReadOnlySpan<byte> plaintext)
    {
        if (plaintext.Length is < 2 or > MaxPayloadBytes)
            throw new TemporaryWorkflowPayloadException();

        TemporaryWorkflowPartyPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<TemporaryWorkflowPartyPayload>(plaintext, Options)
                ?? throw new TemporaryWorkflowPayloadException();
        }
        catch (JsonException)
        {
            throw new TemporaryWorkflowPayloadException();
        }

        if (payload.SchemaVersion != 1 || string.IsNullOrWhiteSpace(payload.PartyType))
            throw new TemporaryWorkflowPayloadException();

        var roles = ParseRoles(payload.Roles);
        var source = ParseEnum(payload.AcquaintanceSource, PartyAcquaintanceSource.Unknown);
        var communication = new PartyCommunicationPreferences(
            payload.Communication?.RepairStatusSmsEnabled ?? true,
            payload.Communication?.ReadyForDeliverySmsEnabled ?? true,
            payload.Communication?.PromotionsSmsEnabled ?? false);

        var address = payload.Address;
        var addressValues = address is null
            ? new string?[] { null, null, null, null }
            : new[] { address.Province, address.City, address.PostalCode, address.AddressLine };
        if (addressValues.Any(value => !string.IsNullOrWhiteSpace(value)) &&
            addressValues.Any(string.IsNullOrWhiteSpace))
            throw new TemporaryWorkflowPayloadException();

        var countryCode = string.IsNullOrWhiteSpace(address?.CountryCode) ? "IR" : address.CountryCode!;
        var hasAddress = addressValues.All(value => !string.IsNullOrWhiteSpace(value));
        string? maskedMobile = null;
        if (!string.IsNullOrWhiteSpace(payload.PrimaryMobile))
        {
            try
            {
                maskedMobile = PartyIdentifiers.MaskPhone(PartyContactPoint.Mobile(payload.PrimaryMobile).Value);
            }
            catch (ArgumentException)
            {
                throw new TemporaryWorkflowPayloadException();
            }
        }

        var hasNationalIdentifier =
            !string.IsNullOrWhiteSpace(payload.NationalId) ||
            !string.IsNullOrWhiteSpace(payload.LegalNationalId);

        if (string.Equals(payload.PartyType, "NaturalPerson", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(payload.GivenName) ||
                string.IsNullOrWhiteSpace(payload.FamilyName) ||
                !string.IsNullOrWhiteSpace(payload.LegalName) ||
                !string.IsNullOrWhiteSpace(payload.LegalNationalId) ||
                !string.IsNullOrWhiteSpace(payload.EconomicCode))
                throw new TemporaryWorkflowPayloadException();

            var workClassification = ParseNullableEnum<WorkClassification>(payload.WorkClassification);
            var registration = new RegisterNaturalParty(
                payload.GivenName, payload.FamilyName, roles, payload.NationalId,
                payload.PrimaryMobile, payload.AlternateMobile, payload.Landline, payload.Email,
                address?.Province, address?.City, address?.PostalCode, address?.AddressLine,
                source, payload.InternalNote, communication, workClassification, countryCode);
            return new TemporaryWorkflowDecodedParty(
                payload.SchemaVersion, PartyType.NaturalPerson, registration,
                $"{payload.GivenName} {payload.FamilyName}", maskedMobile,
                hasNationalIdentifier, hasAddress);
        }

        if (string.Equals(payload.PartyType, "LegalEntity", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(payload.LegalName) ||
                !string.IsNullOrWhiteSpace(payload.GivenName) ||
                !string.IsNullOrWhiteSpace(payload.FamilyName) ||
                !string.IsNullOrWhiteSpace(payload.NationalId) ||
                !string.IsNullOrWhiteSpace(payload.WorkClassification))
                throw new TemporaryWorkflowPayloadException();

            var registration = new RegisterLegalParty(
                payload.LegalName, payload.LegalNationalId, payload.EconomicCode,
                payload.ResponsiblePersonName, roles, payload.PrimaryMobile,
                payload.AlternateMobile, payload.Landline, payload.Email,
                address?.Province, address?.City, address?.PostalCode, address?.AddressLine,
                source, payload.InternalNote, communication, null, countryCode);
            return new TemporaryWorkflowDecodedParty(
                payload.SchemaVersion, PartyType.LegalPerson, registration,
                payload.LegalName, maskedMobile, hasNationalIdentifier, hasAddress);
        }

        throw new TemporaryWorkflowPayloadException();
    }

    private static PartyBusinessRoles ParseRoles(string[]? values)
    {
        if (values is null || values.Length == 0 || values.Any(string.IsNullOrWhiteSpace) ||
            values.Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new TemporaryWorkflowPayloadException();

        var roles = PartyBusinessRoles.None;
        foreach (var value in values)
        {
            roles |= value switch
            {
                "Customer" => PartyBusinessRoles.Customer,
                "Colleague" => PartyBusinessRoles.Colleague,
                _ => throw new TemporaryWorkflowPayloadException()
            };
        }
        return roles;
    }

    private static T ParseEnum<T>(string? value, T fallback) where T : struct, Enum =>
        ParseNullableEnum<T>(value) ?? fallback;

    private static T? ParseNullableEnum<T>(string? value) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Enum.TryParse<T>(value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new TemporaryWorkflowPayloadException();
    }
}
