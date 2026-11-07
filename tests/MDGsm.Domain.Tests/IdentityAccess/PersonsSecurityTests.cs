using MDGsm.Api.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MDGsm.Domain.Tests.IdentityAccess;

public sealed class PersonsSecurityTests
{
    [Fact]
    public async Task Persons_route_rejects_a_request_without_a_validated_session()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };
        context.Request.Path = "/api/v1/persons/01936f3e-7b2a-7abc-8def-0123456789ab";
        context.Response.Body = new MemoryStream();
        var nextCalled = false;

        await new SessionAuthenticationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }).InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }
}
