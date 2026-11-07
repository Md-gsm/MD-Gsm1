using System.Net;
using System.Text;
using MDGsm.Client;
using Xunit;

namespace MDGsm.Domain.Tests;

public sealed class ApiClientTests
{
    [Fact]
    public async Task GetSystemInfoAsync_DeserializesStandardSuccessEnvelope()
    {
        using var http = CreateClient(HttpStatusCode.OK, "{\"data\":{\"product\":\"MD-Gsm\",\"apiVersion\":\"v1\",\"releaseStage\":\"foundation\"},\"requestId\":\"req-123\"}");
        var client = new ApiClient(http);

        var result = await client.GetSystemInfoAsync();

        Assert.Equal("MD-Gsm", result.Product);
        Assert.Equal("v1", result.ApiVersion);
        Assert.Equal("foundation", result.ReleaseStage);
    }

    [Fact]
    public async Task GetSystemInfoAsync_ThrowsSafeExceptionForHttpError()
    {
        using var http = CreateClient(HttpStatusCode.InternalServerError, "{\"title\":\"An unexpected error occurred.\",\"status\":500,\"requestId\":\"req-500\"}");
        var client = new ApiClient(http);

        var error = await Assert.ThrowsAsync<ApiClientException>(() => client.GetSystemInfoAsync());

        Assert.Equal(HttpStatusCode.InternalServerError, error.StatusCode);
        Assert.Equal("req-500", error.RequestId);
        Assert.DoesNotContain("unexpected error", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetSystemInfoAsync_RejectsInvalidEnvelope()
    {
        using var http = CreateClient(HttpStatusCode.OK, "{\"data\":{\"product\":\"MD-Gsm\"}}");
        var client = new ApiClient(http);

        await Assert.ThrowsAsync<InvalidDataException>(() => client.GetSystemInfoAsync());
    }

    private static HttpClient CreateClient(HttpStatusCode statusCode, string body)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(statusCode)
        { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("api/v1/system/info", request.RequestUri!.PathAndQuery.TrimStart('/'));
            return Task.FromResult(responseFactory(request));
        }
    }
}
