using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MDGsm.AuditSecurity.Contracts;

namespace MDGsm.AuditSecurity.Application;

/// <summary>
/// HTTP transport for source workers. Authentication is supplied by the host-configured
/// HttpClient handler (for example, platform-managed mTLS); secrets and certificates are
/// deliberately outside this library. The remote API must commit before returning a receipt.
/// </summary>
public sealed class HttpAuditEventIngestionClient(
    HttpClient httpClient,
    string relativeEndpoint = "api/v1/audit/events") : IAuditEventIngestionContract
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AuditAppendReceipt> AppendAsync(
        AuditEventSubmission submission,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(submission);

        var normalized = AuditEventSubmissionCanonicalizer.NormalizeAndValidate(submission);
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                relativeEndpoint, normalized, JsonOptions, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var receipt = await response.Content.ReadFromJsonAsync<AuditAppendReceipt>(
                    JsonOptions, cancellationToken);
                if (receipt is null)
                    throw new AuditEventTransientDeliveryException("empty_ingestion_receipt");

                return receipt;
            }

            var failureCode = "http_" + ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (response.StatusCode is HttpStatusCode.RequestTimeout
                or HttpStatusCode.TooManyRequests
                || (int)response.StatusCode >= 500
                || (int)response.StatusCode == 425)
                throw new AuditEventTransientDeliveryException(failureCode);

            if (response.StatusCode is HttpStatusCode.BadRequest
                or HttpStatusCode.Unauthorized
                or HttpStatusCode.Forbidden
                or HttpStatusCode.Conflict
                or HttpStatusCode.UnprocessableEntity
                or HttpStatusCode.NotFound)
                throw new AuditEventDeliveryRejectedException(failureCode);

            throw new AuditEventTransientDeliveryException(failureCode);
        }
        catch (AuditEventDeliveryRejectedException)
        {
            throw;
        }
        catch (AuditEventTransientDeliveryException)
        {
            throw;
        }
        catch (HttpRequestException exception)
        {
            throw new AuditEventTransientDeliveryException("transport_unavailable", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AuditEventTransientDeliveryException("transport_timeout", exception);
        }
        catch (JsonException exception)
        {
            throw new AuditEventTransientDeliveryException("invalid_ingestion_response", exception);
        }
    }
}
