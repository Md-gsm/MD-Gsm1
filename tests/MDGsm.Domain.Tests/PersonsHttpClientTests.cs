using System.Net;
using System.Text;
using System.Text.Json;
using MDGsm.Client;
using MDGsm.Host.Client;
using MDGsm.Persons.Contracts;
using Xunit;

namespace MDGsm.Domain.Tests;

public sealed class PersonsHttpClientTests
{
    [Fact]
    public async Task IdentityAccessLogin_ReadsOpaqueTokenAndFirstLoginFlag()
    {
        using var http = CreateClient(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/v1/identity/login", request.RequestUri!.AbsolutePath);
            return Json(HttpStatusCode.OK,
                "{\"accessToken\":\"opaque-token\",\"expiresAtUtc\":\"2026-11-24T18:00:00Z\",\"mustChangePassword\":true}");
        });
        var client = new IdentityAccessApiClient(http);

        var result = await client.LoginAsync("admin", "initial-password", Guid.CreateVersion7());

        Assert.Equal("opaque-token", result.AccessToken);
        Assert.True(result.MustChangePassword);
    }

    [Fact]
    public async Task ChangePassword_SendsBearerAndNeverReturnsPasswordData()
    {
        using var http = CreateClient(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/v1/identity/password/change", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("opaque-token", request.Headers.Authorization?.Parameter);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var client = new IdentityAccessApiClient(http);

        await client.ChangeOwnPasswordAsync("opaque-token", "current-password", "new-password-strong");
    }

    [Fact]
    public async Task Logout_SendsBearerToIdentityAccess()
    {
        using var http = CreateClient(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/v1/identity/logout", request.RequestUri!.AbsolutePath);
            Assert.Equal("opaque-token", request.Headers.Authorization?.Parameter);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var client = new IdentityAccessApiClient(http);

        await client.LogoutAsync("opaque-token");
    }

    [Fact]
    public async Task QuickRegistration_SendsBearerAndStableIdempotencyHeader()
    {
        var operationId = Guid.CreateVersion7();
        using var http = CreateClient(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/v2/persons/quick/natural", request.RequestUri!.AbsolutePath);
            Assert.Equal("opaque-token", request.Headers.Authorization?.Parameter);
            Assert.True(request.Headers.TryGetValues("Idempotency-Key", out var values));
            Assert.Equal(operationId.ToString("D"), Assert.Single(values!));
            return Json(HttpStatusCode.OK,
                "{\"outcome\":\"SelectionRequired\",\"partyId\":null,\"created\":false,\"candidates\":[{\"partyId\":\"01935f10-7b4c-7abc-8def-012345678901\",\"partyType\":\"NaturalPerson\",\"displayName\":\"علی رضایی\",\"roles\":[\"Customer\"],\"partyIsActive\":true,\"isMerged\":false,\"hasActiveMobile\":true,\"maskedMobile\":\"0912•••1234\"}]}");
        });
        var client = new PersonsApiClient(http);
        var party = new CreateNaturalPartyRequest(
            "علی", "رضایی", ["Customer"], null, "09121231234", null, null, null, null,
            new PartyCommunicationPreferencesContract(true, true, false), "Unknown", null);

        var result = await client.QuickRegisterNaturalAsync(
            "opaque-token", operationId, new QuickRegisterNaturalPartyRequest(party));

        Assert.Equal("SelectionRequired", result.Outcome);
        Assert.False(result.Created);
        Assert.Equal("0912•••1234", Assert.Single(result.Candidates).MaskedMobile);
    }

    [Fact]
    public async Task PersonsClient_ExposesOnlySafeErrorCodeAndRequestId()
    {
        using var http = CreateClient(_ => Json(HttpStatusCode.ServiceUnavailable,
            "{\"error\":\"persons_write_api_disabled\",\"requestId\":\"req-503\"}"));
        var client = new PersonsApiClient(http);
        var party = new CreateLegalPartyRequest(
            "شرکت نمونه", null, null, null, ["Customer"], null, null, null, null, null,
            new PartyCommunicationPreferencesContract(true, true, false), "Unknown", null);

        var exception = await Assert.ThrowsAsync<ApiClientException>(() =>
            client.QuickRegisterLegalAsync("opaque-token", Guid.CreateVersion7(),
                new QuickRegisterLegalPartyRequest(party)));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.Equal("persons_write_api_disabled", exception.ErrorCode);
        Assert.Equal("req-503", exception.RequestId);
        Assert.DoesNotContain("persons_write_api_disabled", exception.Message, StringComparison.Ordinal);
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        new(new StubHandler(responseFactory)) { BaseAddress = new Uri("https://api.example.test/") };

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string body) =>
        new(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
