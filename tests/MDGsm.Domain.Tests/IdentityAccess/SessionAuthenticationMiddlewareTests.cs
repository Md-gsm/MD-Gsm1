using System.Security.Claims;
using MDGsm.Api.Security;
using MDGsm.IdentityAccess.Application;
using MDGsm.IdentityAccess.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MDGsm.Domain.Tests.IdentityAccess;

public sealed class SessionAuthenticationMiddlewareTests
{
    private static readonly Guid TrustedAccount = Guid.Parse("01936f3e-7b2a-7abc-8def-0123456789ab");

    [Fact]
    public async Task Installs_only_database_resolved_actor_and_renews_valid_session()
    {
        var resolver = new StubResolver(new ValidatedSessionIdentity(AccountId.From(TrustedAccount), 7));
        var sessions = new StubSessions(true);
        var services = CreateServices(resolver, sessions);
        var context = CreateContext(services, "/api/v1/persons/" + Guid.NewGuid().ToString("D"));
        context.Request.Headers.Authorization = "Bearer " + new string('A', 43);
        context.Request.Headers["X-Actor-Id"] = Guid.NewGuid().ToString("D");
        var called = false;
        var middleware = new SessionAuthenticationMiddleware(_ => { called = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context);

        Assert.True(called);
        Assert.Equal(TrustedAccount.ToString("D"), context.User.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("MDGsm.Session", context.User.Identity?.AuthenticationType);
        Assert.Equal(7L, sessions.LastCredentialVersion);
        Assert.Equal(32, resolver.LastDigestLength);
        Assert.Equal(1, sessions.HeartbeatCalls);
    }

    [Fact]
    public async Task Rejects_token_when_database_has_no_matching_live_session()
    {
        var context = CreateContext(CreateServices(new StubResolver(null), new StubSessions(false)), "/api/v1/persons/123");
        context.Request.Headers.Authorization = "Bearer " + new string('A', 43);
        var called = false;
        await new SessionAuthenticationMiddleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context);
        Assert.False(called);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Rejects_session_when_lease_renewal_fails()
    {
        var resolver = new StubResolver(new ValidatedSessionIdentity(AccountId.From(TrustedAccount), 7));
        var context = CreateContext(CreateServices(resolver, new StubSessions(false)), "/api/v1/persons/123");
        context.Request.Headers.Authorization = "Bearer " + new string('A', 43);
        var called = false;
        await new SessionAuthenticationMiddleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context);
        Assert.False(called);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Rejects_missing_bearer_token()
    {
        var context = CreateContext(CreateServices(new StubResolver(null), new StubSessions(false)), "/api/v1/persons/123");
        await new SessionAuthenticationMiddleware(_ => Task.CompletedTask).InvokeAsync(context);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task Fails_closed_with_service_unavailable_when_session_services_are_not_registered()
    {
        var context = CreateContext(new ServiceCollection().BuildServiceProvider(), "/api/v1/persons/123");
        context.Request.Headers.Authorization = "Bearer " + new string('A', 43);
        await new SessionAuthenticationMiddleware(_ => Task.CompletedTask).InvokeAsync(context);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    [Fact]
    public async Task Does_not_authenticate_public_health_routes()
    {
        var context = CreateContext(new ServiceCollection().BuildServiceProvider(), "/health/live");
        var called = false;
        await new SessionAuthenticationMiddleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context);
        Assert.True(called);
        Assert.False(context.User.Identity?.IsAuthenticated ?? false);
    }

    private static ServiceProvider CreateServices(StubResolver resolver, StubSessions sessions)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAccessTokenFactory, StubTokenFactory>();
        services.AddSingleton<ISessionIdentityResolver>(resolver);
        services.AddSingleton<ISessionStore>(sessions);
        services.AddSingleton<ISystemPolicyStore, StubPolicyStore>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        return services.BuildServiceProvider();
    }

    private static DefaultHttpContext CreateContext(IServiceProvider services, string path)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class StubTokenFactory : IAccessTokenFactory
    {
        public string CreateOpaqueToken() => new('A', 43);
        public byte[] ComputeDigest(string token) => new byte[32];
    }

    private sealed class StubResolver(ValidatedSessionIdentity? result) : ISessionIdentityResolver
    {
        public int LastDigestLength { get; private set; }
        public Task<ValidatedSessionIdentity?> ResolveAsync(byte[] tokenDigest, DateTimeOffset now, CancellationToken ct)
        {
            LastDigestLength = tokenDigest.Length;
            return Task.FromResult(result);
        }
    }

    private sealed class StubSessions(bool heartbeatResult) : ISessionStore
    {
        public int HeartbeatCalls { get; private set; }
        public long LastCredentialVersion { get; private set; }
        public Task<bool> HasActiveSessionAsync(AccountId id, DateTimeOffset now, CancellationToken ct) => Task.FromResult(false);
        public Task<int> CountDistinctActiveAccountsAsync(DateTimeOffset now, CancellationToken ct) => Task.FromResult(0);
        public Task CreateAsync(AccountId id, byte[] tokenDigest, long credentialVersion, Guid clientInstanceId, DateTimeOffset createdAt, DateTimeOffset leaseExpiresAt, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAllForAccountAsync(AccountId id, DateTimeOffset now, string reason, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> TryHeartbeatAsync(byte[] tokenDigest, long credentialVersion, DateTimeOffset now, DateTimeOffset newLeaseExpiry, CancellationToken ct)
        {
            HeartbeatCalls++;
            LastCredentialVersion = credentialVersion;
            return Task.FromResult(heartbeatResult);
        }
        public Task<bool> RevokeByDigestAsync(byte[] tokenDigest, DateTimeOffset now, string reason, CancellationToken ct) => Task.FromResult(false);
    }

    private sealed class StubPolicyStore : ISystemPolicyStore
    {
        public Task<SessionPolicy> GetSessionPolicyAsync(CancellationToken ct) => Task.FromResult(SessionPolicy.Initial);
    }
}
