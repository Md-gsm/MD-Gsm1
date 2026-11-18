using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class MigrationResourceTests
{
    [Fact]
    public void Migration_files_and_registry_match_one_to_one()
    {
        var root = FindRepositoryRoot();
        var migrationRoot = Path.Combine(root, "database", "migrations");
        var files = Directory.GetFiles(migrationRoot, "*.sql", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(files);
        var names = files.Select(Path.GetFileName).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        var parsed = names.Select(name => Regex.Match(name!,
            @"^(?<version>\d{4})_[a-z0-9_]+\.sql$", RegexOptions.IgnoreCase)).ToArray();
        Assert.All(parsed, match => Assert.True(match.Success, $"Invalid migration filename: {match.Value}"));

        var versions = parsed.Select(match => int.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture))
            .OrderBy(value => value).ToArray();
        Assert.Equal(versions.Length, versions.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, versions.Length), versions);

        var registryPath = Path.Combine(root, "docs", "data", "MIGRATION_SEQUENCE_REGISTRY_FA.md");
        var registry = File.ReadAllText(registryPath);
        var registryPaths = Regex.Matches(registry,
                @"^\|\s*\d{4}\s*\|\s*`(?<path>database/migrations/[^\`]+\.sql)`\s*\|",
                RegexOptions.Multiline)
            .Select(match => match.Groups["path"].Value)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(files, registryPaths);
        Assert.Equal(registryPaths.Length, registryPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MDGsm.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("MDGsm.sln was not found above the test output directory.");
    }
}
