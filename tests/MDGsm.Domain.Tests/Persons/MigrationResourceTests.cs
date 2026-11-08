using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class MigrationResourceTests
{
    [Fact]
    public void Migration_file_names_are_unique_across_module_folders()
    {
        // The migration resources are embedded with LogicalName = file name only, so two modules
        // must never ship a migration with the same file name.
        var root = FindRepositoryRoot();
        var files = Directory.GetFiles(Path.Combine(root, "database", "migrations"), "*.sql", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .ToArray();

        Assert.NotEmpty(files);
        Assert.Equal(files.Length, files.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        var versionMatches = files
            .Select(file => Regex.Match(
                file!,
                @"^(?<version>\d{4})_[a-z0-9_]+\.sql$",
                RegexOptions.IgnoreCase))
            .ToArray();

        Assert.All(versionMatches, match => Assert.True(match.Success, $"Invalid migration filename: {match.Value}"));

        var versions = versionMatches
            .Select(match => int.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture))
            .ToArray();
        Assert.Equal(versions.Length, versions.Distinct().Count());

        var registryPath = Path.Combine(root, "docs", "data", "MIGRATION_SEQUENCE_REGISTRY_FA.md");
        var registry = File.ReadAllText(registryPath);
        foreach (var file in Directory.GetFiles(Path.Combine(root, "database", "migrations"), "*.sql", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
            Assert.Contains(relativePath, registry);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MDGsm.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("MDGsm.sln was not found above the test output directory.");
    }
}
