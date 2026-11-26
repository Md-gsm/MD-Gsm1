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
        Assert.DoesNotContain("operationId: searchParties", contract, StringComparison.Ordinal);
        Assert.Contains("  /api/v2/persons/search:", contract, StringComparison.Ordinal);
        Assert.Contains("PartyResponse:", contract, StringComparison.Ordinal);
        Assert.Contains("workClassification:", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("address    PartyResponse:", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("\\n: { anyOf:", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v2/persons/quick/", contract, StringComparison.Ordinal);
    }


    [Fact]
    public void Search_route_is_documented_with_fail_closed_global_read_and_default_off_flag()
    {
        var root = FindRepositoryRoot();
        var contract = File.ReadAllText(Path.Combine(root, "docs", "persons", "contracts", "persons.openapi.yaml"));
        var program = File.ReadAllText(Path.Combine(root, "src", "Host", "Api", "Program.cs"));
        var settings = File.ReadAllText(Path.Combine(root, "src", "Host", "Api", "appsettings.json"));

        Assert.Contains("  /api/v2/persons/search:", contract, StringComparison.Ordinal);
        Assert.Contains("Persons:SearchApiEnabled", contract, StringComparison.Ordinal);
        Assert.Contains("persons.read", contract, StringComparison.Ordinal);
        Assert.Contains("maxLength: 160", contract, StringComparison.Ordinal);
        Assert.Contains("maximum: 10", contract, StringComparison.Ordinal);
        Assert.Contains("maskedMobile", contract, StringComparison.Ordinal);
        Assert.Contains("PersonsGlobalReadAuthorization.CanReadAsync", program, StringComparison.Ordinal);
        Assert.Contains("RecordSecurityViolationAsync", program, StringComparison.Ordinal);
        Assert.Contains("TryConsumeAsync", program, StringComparison.Ordinal);
        Assert.Contains("\"SearchApiEnabled\": false", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void Canonical_natural_and_legal_write_routes_always_use_duplicate_resolution()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "Host", "Api", "Program.cs"));
        var openApi = File.ReadAllText(Path.Combine(root, "docs", "persons", "contracts", "persons.openapi.yaml"));

        Assert.Contains("app.MapPost(\"/api/v2/persons\"", program, StringComparison.Ordinal);
        Assert.Contains("app.MapPost(\"/api/v2/persons/legal\"", program, StringComparison.Ordinal);
        Assert.Contains("QuickRegisterNaturalPartyRequest request", program, StringComparison.Ordinal);
        Assert.Contains("QuickRegisterLegalPartyRequest request", program, StringComparison.Ordinal);
        Assert.Contains("QuickRegisterNaturalAsync", program, StringComparison.Ordinal);
        Assert.Contains("QuickRegisterLegalAsync", program, StringComparison.Ordinal);
        Assert.Contains("request.ConfirmDistinctParty", program, StringComparison.Ordinal);
        Assert.Contains("confirmDistinctParty", openApi, StringComparison.Ordinal);
        Assert.Contains("/api/v2/persons/workflows/{workflowRecordId}/promote", openApi, StringComparison.Ordinal);
        Assert.Contains("Persons:WorkflowPromotionApiEnabled", openApi, StringComparison.Ordinal);
        Assert.Contains("WorkflowPromotionApiEnabled", program, StringComparison.Ordinal);
        Assert.Contains("Persons:WriteApiEnabled", program, StringComparison.Ordinal);
        Assert.Contains("HasPermissionAsync(accountId, \"persons.create\"", program, StringComparison.Ordinal);
        Assert.Contains("HasPermissionAsync(accountId, \"persons.read\"", program, StringComparison.Ordinal);
        Assert.DoesNotContain("parties.AddNaturalAsync", program, StringComparison.Ordinal);
        Assert.DoesNotContain("parties.AddLegalAsync", program, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v2/persons/quick/natural", program, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v2/persons/quick/legal", program, StringComparison.Ordinal);
        Assert.Contains("#/components/schemas/QuickRegisterNaturalPartyRequest", openApi, StringComparison.Ordinal);
        Assert.Contains("#/components/schemas/QuickRegisterLegalPartyRequest", openApi, StringComparison.Ordinal);
    }

    [Fact]
    public void Party_detail_endpoint_maps_through_the_versioned_public_contract()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "Host", "Api", "Program.cs"));
        var mapper = File.ReadAllText(Path.Combine(root, "src", "Modules", "Persons", "Application", "PersonsRegistrationMapper.cs"));

        Assert.Contains("Results.Ok(PersonsRegistrationMapper.Map(details))", program, StringComparison.Ordinal);
        Assert.Contains("public static PartyResponse Map(PartyDetails details)", mapper, StringComparison.Ordinal);
        Assert.Contains("PartyType.LegalPerson => \"LegalEntity\"", mapper, StringComparison.Ordinal);
    }

    [Fact]
    public void Database_backed_persons_routes_are_only_registered_when_persistence_dependencies_exist()
    {
        var root = FindRepositoryRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "Host", "Api", "Program.cs"));

        Assert.Contains("var personsPersistenceAvailable =", program, StringComparison.Ordinal);
        Assert.Contains("typeof(NpgsqlDataSource)", program, StringComparison.Ordinal);
        Assert.Contains("typeof(IRolePermissionStore)", program, StringComparison.Ordinal);
        Assert.Contains("typeof(IPartyRepository)", program, StringComparison.Ordinal);

        var guardStart = program.IndexOf("if (personsPersistenceAvailable)", StringComparison.Ordinal);
        var naturalCreate = program.IndexOf("app.MapPost(\"/api/v2/persons\"", StringComparison.Ordinal);
        var legalCreate = program.IndexOf("app.MapPost(\"/api/v2/persons/legal\"", StringComparison.Ordinal);
        var partyRead = program.IndexOf("app.MapGet(\"/api/v2/persons/{partyId}\"", StringComparison.Ordinal);
        var promotionRoutes = program.IndexOf("// Promotion routes", StringComparison.Ordinal);

        Assert.True(guardStart >= 0 && naturalCreate > guardStart,
            "The natural-person route must be inside the persistence-availability guard.");
        Assert.True(legalCreate > naturalCreate && legalCreate < promotionRoutes,
            "The legal-person route must be inside the persistence-availability guard.");
        Assert.True(partyRead > legalCreate && partyRead < promotionRoutes,
            "The party-detail route must be inside the persistence-availability guard.");

        Assert.Contains("[FromServices] NpgsqlDataSource dataSource", program, StringComparison.Ordinal);
        Assert.Contains("[FromServices] IRolePermissionStore permissions", program, StringComparison.Ordinal);
        Assert.Contains("[FromServices] IPartyRepository parties", program, StringComparison.Ordinal);
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
