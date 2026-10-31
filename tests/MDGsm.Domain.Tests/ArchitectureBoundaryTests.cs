using System.Xml.Linq;
using Xunit;

namespace MDGsm.Domain.Tests;

public sealed class ArchitectureBoundaryTests
{
    [Fact]
    public void Layer_project_references_follow_the_approved_dependency_direction()
    {
        var root = FindSolutionRoot();
        AssertReferences(root, "src/MDGsm.Domain/MDGsm.Domain.csproj");
        AssertReferences(root, "src/MDGsm.Application/MDGsm.Application.csproj", "..\\MDGsm.Domain\\MDGsm.Domain.csproj");
        AssertReferences(root, "src/MDGsm.Infrastructure/MDGsm.Infrastructure.csproj", "..\\MDGsm.Application\\MDGsm.Application.csproj");
        AssertReferences(
            root,
            "src/MDGsm.Api/MDGsm.Api.csproj",
            "..\\MDGsm.Application\\MDGsm.Application.csproj",
            "..\\MDGsm.Infrastructure\\MDGsm.Infrastructure.csproj",
            "..\\Modules\\Persons\\Contracts\\MDGsm.Persons.Contracts.csproj",
            "..\\Modules\\IdentityAccess\\Contracts\\MDGsm.IdentityAccess.Contracts.csproj",
            "..\\Modules\\RepairOperations\\Contracts\\MDGsm.RepairOperations.Contracts.csproj",
            "..\\Modules\\Finance\\Contracts\\MDGsm.Finance.Contracts.csproj");
        AssertReferences(root, "src/MDGsm.Client/MDGsm.Client.csproj");
        AssertReferences(root, "src/MDGsm.Desktop/MDGsm.Desktop.csproj", "..\\MDGsm.Client\\MDGsm.Client.csproj");
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
