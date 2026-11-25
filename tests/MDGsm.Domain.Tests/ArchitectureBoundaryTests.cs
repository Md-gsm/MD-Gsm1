using System.Xml.Linq;
using Xunit;

namespace MDGsm.Domain.Tests;

public sealed class ArchitectureBoundaryTests
{
    [Fact]
    public void Building_blocks_follow_the_approved_dependency_direction()
    {
        var root = FindSolutionRoot();
        AssertReferences(root, "src/BuildingBlocks/MDGsm.Domain/MDGsm.Domain.csproj");
        AssertReferences(
            root,
            "src/BuildingBlocks/MDGsm.Application/MDGsm.Application.csproj",
            "..\\MDGsm.Domain\\MDGsm.Domain.csproj");
        AssertReferences(
            root,
            "src/BuildingBlocks/MDGsm.Infrastructure/MDGsm.Infrastructure.csproj",
            "..\\MDGsm.Application\\MDGsm.Application.csproj",
            "..\\..\\Modules\\IdentityAccess\\Application\\MDGsm.IdentityAccess.Application.csproj",
            "..\\..\\Modules\\Persons\\Application\\MDGsm.Persons.Application.csproj",
            "..\\..\\Modules\\Persons\\Infrastructure\\MDGsm.Persons.Infrastructure.csproj");
        AssertReferences(root, "src/BuildingBlocks/MDGsm.Client/MDGsm.Client.csproj");
    }

    [Fact]
    public void Host_projects_depend_only_on_building_blocks_and_published_contracts()
    {
        var root = FindSolutionRoot();
        AssertReferences(
            root,
            "src/Host/Api/MDGsm.Api.csproj",
            "..\\..\\BuildingBlocks\\MDGsm.Application\\MDGsm.Application.csproj",
            "..\\..\\BuildingBlocks\\MDGsm.Infrastructure\\MDGsm.Infrastructure.csproj",
            "..\\..\\Modules\\Persons\\Application\\MDGsm.Persons.Application.csproj",
            "..\\..\\Modules\\Persons\\Contracts\\MDGsm.Persons.Contracts.csproj",
            "..\\..\\Modules\\IdentityAccess\\Contracts\\MDGsm.IdentityAccess.Contracts.csproj",
            "..\\..\\Modules\\RepairOperations\\Contracts\\MDGsm.RepairOperations.Contracts.csproj",
            "..\\..\\Modules\\Finance\\Contracts\\MDGsm.Finance.Contracts.csproj",
            "..\\..\\Modules\\Finance\\MDGsm.Finance.csproj");
        AssertReferences(root, "src/Modules/Accounting/Domain/MDGsm.Accounting.Domain.csproj");
        AssertReferences(
            root,
            "src/Modules/Accounting/Application/MDGsm.Accounting.Application.csproj",
            "..\\Domain\\MDGsm.Accounting.Domain.csproj",
            "..\\Contracts\\MDGsm.Accounting.Contracts.csproj");
        AssertReferences(
            root,
            "src/Host/Desktop/MDGsm.Desktop.csproj",
            "..\\..\\BuildingBlocks\\MDGsm.Client\\MDGsm.Client.csproj",
            "..\\Client\\MDGsm.Host.Client.csproj");
        AssertReferences(
            root,
            "src/Host/Client/MDGsm.Host.Client.csproj",
            "..\\..\\BuildingBlocks\\MDGsm.Client\\MDGsm.Client.csproj",
            "..\\..\\Modules\\IdentityAccess\\Contracts\\MDGsm.IdentityAccess.Contracts.csproj",
            "..\\..\\Modules\\Persons\\Contracts\\MDGsm.Persons.Contracts.csproj");
    }

    [Fact]
    public void All_module_contract_projects_are_isolated_and_in_the_solution()
    {
        var root = FindSolutionRoot();
        var solution = File.ReadAllText(Path.Combine(root, "MDGsm.sln"));
        Assert.Contains("src\\Host\\Client\\MDGsm.Host.Client.csproj", solution);
        foreach (var module in new[] { "Persons", "IdentityAccess", "RepairOperations", "Finance", "Accounting" })
        {
            var relative = $"src/Modules/{module}/Contracts/MDGsm.{module}.Contracts.csproj";
            var project = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(project), $"Missing Contracts project: {relative}");
            Assert.Contains(relative.Replace('/', '\\'), solution);

            var xml = XDocument.Load(project);
            Assert.Empty(xml.Descendants("ProjectReference"));

            var versionMarker = Path.Combine(Path.GetDirectoryName(project)!, "ModuleContractVersion.cs");
            Assert.True(File.Exists(versionMarker), $"Missing contract version marker: {versionMarker}");
            var expectedVersion = module == "Finance" ? "1.1" : "1.0";
            var expectedMarker = "Current = " + (char)34 + expectedVersion + (char)34;
            Assert.Contains(expectedMarker, File.ReadAllText(versionMarker));
        }
    }

    [Fact]
    public void Persons_internal_projects_follow_ADR0016_and_keep_G1_disabled()
    {
        var root = FindSolutionRoot();
        var manifest = File.ReadAllText(Path.Combine(root, "docs", "persons", "persons-module-manifest.json"));
        var adr = File.ReadAllText(Path.Combine(root, "docs", "decisions", "0016-persons-module-boundary-and-data-ownership.md"));

        Assert.Contains("\"personsInternalCodePlacement\": \"MODULE_OWNED_PROJECTS_PER_ADR_0016\"", manifest);
        Assert.Contains("\"g1Authorized\": false", manifest);
        Assert.Contains("ADR-0016", adr, StringComparison.OrdinalIgnoreCase);

        AssertReferences(root, "src/Modules/Persons/Domain/MDGsm.Persons.Domain.csproj");
        AssertReferences(root, "src/Modules/Persons/Application/MDGsm.Persons.Application.csproj",
            "..\\Domain\\MDGsm.Persons.Domain.csproj",
            "..\\Contracts\\MDGsm.Persons.Contracts.csproj");
        AssertReferences(root, "src/Modules/Persons/Infrastructure/MDGsm.Persons.Infrastructure.csproj",
            "..\\Application\\MDGsm.Persons.Application.csproj",
            "..\\Domain\\MDGsm.Persons.Domain.csproj",
            "..\\Contracts\\MDGsm.Persons.Contracts.csproj");

        foreach (var legacyDirectory in new[]
        {
            "src/BuildingBlocks/MDGsm.Domain/Persons",
            "src/BuildingBlocks/MDGsm.Application/Persons",
            "src/BuildingBlocks/MDGsm.Infrastructure/Persons"
        })
        {
            var directory = Path.Combine(root, legacyDirectory.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(directory))
                Assert.Empty(Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories));
        }

        foreach (var moduleDirectory in new[] { "Domain", "Application", "Infrastructure" })
        {
            var directory = Path.Combine(root, "src", "Modules", "Persons", moduleDirectory);
            Assert.NotEmpty(Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories));
        }
    }

    [Fact]
    public void Flat_legacy_project_roots_are_not_left_under_src()
    {
        var root = FindSolutionRoot();
        foreach (var oldName in new[]
        {
            "MDGsm.Domain", "MDGsm.Application", "MDGsm.Infrastructure",
            "MDGsm.Api", "MDGsm.Desktop", "MDGsm.Client"
        })
        {
            Assert.False(Directory.Exists(Path.Combine(root, "src", oldName)), $"Legacy source root remains: {oldName}");
        }
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MDGsm.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate MDGsm.sln from test output directory.");
    }

    private static void AssertReferences(string root, string projectRelativePath, params string[] expectedReferences)
    {
        var path = Path.Combine(root, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
        var document = XDocument.Load(path);
        var actual = document.Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include") ?? string.Empty)
            .Select(value => value.Replace('/', '\\'))
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var expected = expectedReferences.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
        Assert.Equal(expected, actual);
    }
}
