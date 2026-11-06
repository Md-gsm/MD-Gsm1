using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PersonsApiContractTests
{
    [Fact]
    public void OpenApi_contract_declares_the_session_protected_party_details_route()
    {
        var root = FindRepositoryRoot();
        var contract = File.ReadAllText(Path.Combine(root, "docs", "persons", "contracts", "persons.openapi.yaml"));

        Assert.Contains("openapi: 3.1.0", contract, StringComparison.Ordinal);
        Assert.Contains("/api/v1/persons/{partyId}:", contract, StringComparison.Ordinal);
        Assert.Contains("opaqueBearer:", contract, StringComparison.Ordinal);
        Assert.Contains("scheme: bearer", contract, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs", "persons", "contracts", "persons.openapi.yaml")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the MD-Gsm repository root for the Persons OpenAPI contract test.");
    }
}
