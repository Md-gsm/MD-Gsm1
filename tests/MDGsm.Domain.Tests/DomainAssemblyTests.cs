using MDGsm.Domain;
using Xunit;

namespace MDGsm.Domain.Tests;

public sealed class DomainAssemblyTests
{
    [Fact]
    public void Product_name_is_MD_Gsm()
    {
        Assert.Equal("MD-Gsm", DomainAssembly.ProductName);
    }
}
