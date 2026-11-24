using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MDGsm.Persons.Contracts;

namespace MDGsm.Client;

/// <summary>Bearer-authenticated client for the published Persons quick-registration endpoints.</summary>
public sealed class PersonsApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public Task<PartyQuickRegistrationResponse> QuickRegisterNaturalAsync(
        string accessToken,
        Guid operationId,
        QuickRegisterNaturalPartyRequest request,
        CancellationToken cancellationToken = default) =>
        SendQuickRegistrationAsync(
            "api/v2/persons/quick/natural", accessToken, operationId, request, cancellationToken);

    public Task<PartyQuickRegistrationResponse> QuickRegisterLegalAsync(
        string accessToken,
        Guid operationId,
        QuickRegisterLegalPartyRequest request,
        CancellationToken cancellationToken = default) =>
        SendQuickRegistrationAsync(
            "api/v2/persons/quick/legal", accessToken, operationId, request, cancellationToken);

    private async Task<PartyQuickRegistrationResponse> SendQuickRegistrationAsync<TRequest>(
        string route,
        string accessToken,
        Guid operationId,
        TRequest requestBody,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentNullException.ThrowIfNull(requestBody);
        if (operationId == Guid.Empty)
            throw new ArgumentException("A non-empty operation identifier is required.", nameof(operationId));

        using var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(requestBody, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", operationId.ToString("D"));

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw await IdentityAccessApiClient.CreateExceptionAsync(response, cancellationToken).ConfigureAwait(false);

        try
        {
            var result = await response.Content.ReadFromJsonAsync<PartyQuickRegistrationResponse>(
                JsonOptions, cancellationToken).ConfigureAwait(false);
            if (result is null || string.IsNullOrWhiteSpace(result.Outcome) || result.Candidates is null)
                throw new InvalidDataException("Persons returned an incomplete quick-registration response.");
            return result;
        }
        catch (JsonException)
        {
            throw new InvalidDataException("Persons returned an invalid quick-registration response.");
        }
    }
}
