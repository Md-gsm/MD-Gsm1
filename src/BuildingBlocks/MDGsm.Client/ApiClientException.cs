using System.Net;

namespace MDGsm.Client;

/// <summary>Represents a non-success HTTP response without exposing request data or response payloads.</summary>
public sealed class ApiClientException : Exception
{
    public ApiClientException(HttpStatusCode statusCode, string? requestId, string? errorCode = null)
        : base($"The API returned HTTP {(int)statusCode} ({statusCode}).")
    {
        StatusCode = statusCode;
        RequestId = requestId;
        ErrorCode = errorCode;
    }

    public HttpStatusCode StatusCode { get; }
    public string? RequestId { get; }
    public string? ErrorCode { get; }
}
