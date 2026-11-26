using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MDGsm.IdentityAccess.Contracts;
using MDGsm.Client;

namespace MDGsm.Host.Client;

/// <summary>Versioned IdentityAccess HTTP client. Tokens are held by the caller in memory only.</summary>
public sealed class IdentityAccessApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<LoginResponse> LoginAsync(
        string username,
        string password,
        Guid clientInstanceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        if (clientInstanceId == Guid.Empty)
            throw new ArgumentException("A client instance identifier is required.", nameof(clientInstanceId));

        using var response = await httpClient.PostAsJsonAsync(
            "api/v1/identity/login",
            new LoginRequest(username.Trim(), password, clientInstanceId),
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw await CreateExceptionAsync(response, cancellationToken).ConfigureAwait(false);

        try
        {
            var login = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (login is null || string.IsNullOrWhiteSpace(login.AccessToken) || login.ExpiresAtUtc == default)
                throw new InvalidDataException("IdentityAccess returned an incomplete login response.");
            return login;
        }
        catch (JsonException)
        {
            throw new InvalidDataException("IdentityAccess returned an invalid login response.");
        }
    }

    public async Task ChangeOwnPasswordAsync(
        string accessToken,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentPassword);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPassword);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/identity/password/change")
        {
            Content = JsonContent.Create(
                new ChangePasswordRequest(currentPassword, newPassword),
                options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw await CreateExceptionAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task LogoutAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/identity/logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw await CreateExceptionAsync(response, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<ApiClientException> CreateExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string? requestId = null;
        string? errorCode = null;
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(
                stream, cancellationToken: cancellationToken).ConfigureAwait(false);

            var root = document.RootElement;
            if (root.TryGetProperty("requestId", out var id) && id.ValueKind == JsonValueKind.String)
                requestId = id.GetString();
            else if (root.TryGetProperty("extensions", out var extensions) &&
                     extensions.ValueKind == JsonValueKind.Object &&
                     extensions.TryGetProperty("requestId", out id) &&
                     id.ValueKind == JsonValueKind.String)
                requestId = id.GetString();

            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                errorCode = error.GetString();
        }
        catch (JsonException) { }
        catch (IOException) { }

        return new ApiClientException(response.StatusCode, requestId, errorCode);
    }
}
