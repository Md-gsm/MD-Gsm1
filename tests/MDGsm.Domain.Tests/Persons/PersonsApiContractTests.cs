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
        Assert.Contains("/api/v2/persons/{partyId}:", contract, StringComparison.Ordinal);
        Assert.Contains("opaqueBearer:", contract, StringComparison.Ordinal);
        Assert.Contains("scheme: bearer", contract, StringComparison.Ordinal);
        Assert.Contains("required: [givenName, familyName, roles]", contract, StringComparison.Ordinal);
        Assert.Contains("required: [legalName, roles]", contract, StringComparison.Ordinal);
        Assert.Contains("primaryMobile: { type: [string, 'null'] }", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("FinancialProfile:", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("creditLimitRial", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("defaultDiscountPercent", contract, StringComparison.Ordinal);
    }


    [Fact]
    public void Natural_and_legal_write_routes_are_feature_gated_and_permission_checked()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "Host", "Api", "Program.cs"));
        Assert.Contains("app.MapPost(\"/api/v2/persons\"", program, StringComparison.Ordinal);
        Assert.Contains("app.MapPost(\"/api/v2/persons/legal\"", program, StringComparison.Ordinal);
        Assert.Contains("Persons:WriteApiEnabled", program, StringComparison.Ordinal);
        Assert.Contains("HasPermissionAsync(accountId, \"persons.create\"", program, StringComparison.Ordinal);
        Assert.Contains("IdempotencyKeyConflictException", program, StringComparison.Ordinal);
        Assert.Contains("app.MapPost(\"/api/v2/persons/quick/natural\"", program, StringComparison.Ordinal);
        Assert.Contains("app.MapPost(\"/api/v2/persons/quick/legal\"", program, StringComparison.Ordinal);
        Assert.Contains("HasPermissionAsync(accountId, \"persons.read\"", program, StringComparison.Ordinal);
    }


    [Fact]
    public void Party_detail_endpoint_maps_through_the_versioned_public_contract()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "Host", "Api", "Program.cs"));
        var mapper = File.ReadAllText(Path.Combine(root, "src", "Host", "Api", "Persons", "PersonsRegistrationMapper.cs"));

        Assert.Contains("Results.Ok(PersonsRegistrationMapper.Map(details))", program, StringComparison.Ordinal);
        Assert.Contains("public static PartyResponse Map(PartyDetails details)", mapper, StringComparison.Ordinal);
        Assert.Contains("PartyType.LegalPerson => \"LegalEntity\"", mapper, StringComparison.Ordinal);
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
