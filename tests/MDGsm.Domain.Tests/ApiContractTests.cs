using MDGsm.Application.Contracts;
using Xunit;

namespace MDGsm.Domain.Tests;

public sealed class ApiContractTests
{
    [Fact]
    public void SuccessEnvelope_ExposesDataAndRequestId()
    {
        var response = new ApiResponse<string>("ok", "request-123");
        Assert.Equal("ok", response.Data);
        Assert.Equal("request-123", response.RequestId);
    }

    [Fact]
    public void SuccessEnvelope_IsImmutableRecord()
    {
        var response = new ApiResponse<int>(7, "request-456");
        var updated = response with { Data = 8 };
        Assert.Equal(7, response.Data);
        Assert.Equal(8, updated.Data);
    }
}
