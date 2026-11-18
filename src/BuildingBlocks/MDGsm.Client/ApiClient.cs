using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MDGsm.Client;

/// <summary>Small typed client for the currently implemented, non-sensitive system-info endpoint.</summary>
public sealed class ApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = true };

    public async Task<SystemInfoDto> GetSystemInfoAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("api/v1/system/info", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var requestId = await TryReadRequestIdAsync(response, cancellationToken).ConfigureAwait(false);
            throw new ApiClientException(response.StatusCode, requestId);
        }

        ApiResponseDto<SystemInfoDto>? envelope;
        try
        {
            envelope = await response.Content.ReadFromJsonAsync<ApiResponseDto<SystemInfoDto>>(JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            throw new InvalidDataException("The API returned an invalid response format.");
        }

        if (envelope?.Data is null || string.IsNullOrWhiteSpace(envelope.RequestId))
            throw new InvalidDataException("The API response did not contain the required data and request identifier.");

        return envelope.Data;
    }

    private static async Task<string?> TryReadRequestIdAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken).ConfigureAwait(false);
            if (document.RootElement.TryGetProperty("requestId", out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
            if (document.RootElement.TryGetProperty("extensions", out var extensions) && extensions.ValueKind == JsonValueKind.Object &&
                extensions.TryGetProperty("requestId", out value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }
        catch (JsonException) { }
        catch (IOException) { }
        return null;
    }

    private sealed record ApiResponseDto<T>(T? Data, string? RequestId);
}
