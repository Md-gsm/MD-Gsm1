using System.Net;

namespace MDGsm.Client;

/// <summary>Represents a non-success HTTP response without exposing the server response body.</summary>
public sealed class ApiClientException : Exception
{
    public ApiClientException(HttpStatusCode statusCode, string? requestId)
        : base($"The API returned HTTP {(int)statusCode} ({statusCode}).")
    {
        StatusCode = statusCode;
        RequestId = requestId;
    }

    public HttpStatusCode StatusCode { get; }
    public string? RequestId { get; }
}
