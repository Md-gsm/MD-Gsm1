using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class MigrationResourceTests
{
    [Fact]
    public void Migration_files_match_implemented_registry_rows_and_reserved_slots_have_no_sql()
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

        var parsedFiles = names.Select(name => Regex.Match(name!,
            @"^(?<version>\d{4})_[a-z0-9_]+\.sql$", RegexOptions.IgnoreCase)).ToArray();
        Assert.All(parsedFiles, match => Assert.True(match.Success, $"Invalid migration filename: {match.Value}"));

        var versions = parsedFiles.Select(match => int.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture))
            .OrderBy(value => value).ToArray();
        Assert.Equal(versions.Length, versions.Distinct().Count());
        Assert.Equal(versions.OrderBy(value => value), versions);

        var registryPath = Path.Combine(root, "docs", "data", "MIGRATION_SEQUENCE_REGISTRY_FA.md");
        var registry = File.ReadAllText(registryPath);
        var rows = Regex.Matches(registry,
                @"^\|\s*(?<version>\d{4})\s*\|\s*(?<path>[^|]+)\|\s*(?<owner>[^|]+)\|\s*(?<status>[^|]+)\|",
                RegexOptions.Multiline)
            .Cast<Match>()
            .Select(match => new
            {
                Version = int.Parse(match.Groups["version"].Value, CultureInfo.InvariantCulture),
                PathCell = match.Groups["path"].Value.Trim(),
                Status = match.Groups["status"].Value.Trim()
            })
            .ToArray();

        Assert.NotEmpty(rows);
        Assert.Equal(rows.Length, rows.Select(row => row.Version).Distinct().Count());

        var tick = ((char)96).ToString();
        var registryFiles = rows
            .Where(row => row.PathCell.StartsWith(tick + "database/migrations/", StringComparison.Ordinal))
            .Select(row => new
            {
                row.Version,
                Path = Regex.Match(row.PathCell, "^" + tick + "(?<path>database/migrations/[^" + tick + "]+[.]sql)" + tick).Groups["path"].Value,
                row.Status
            })
            .ToArray();

        Assert.All(registryFiles, row => Assert.False(string.IsNullOrWhiteSpace(row.Path),
            $"Registry row {row.Version:D4} has an invalid migration path."));

        var reserved = registryFiles.Where(row => row.Status.StartsWith("RESERVED", StringComparison.Ordinal)).ToArray();
        var reservedVersions = reserved.Select(row => row.Version).ToHashSet();
        if (versions.Length > 0)
        {
            foreach (var missingVersion in Enumerable.Range(1, versions[^1]).Except(versions))
            {
                Assert.Contains(missingVersion, reservedVersions);
            }
        }
        foreach (var row in reserved)
        {
            Assert.Equal(row.Version, int.Parse(Path.GetFileName(row.Path)![..4], CultureInfo.InvariantCulture));
            Assert.False(File.Exists(Path.Combine(root, row.Path)),
                $"Reserved migration {row.Version:D4} must not have a SQL file before implementation is authorized.");
        }

        var implementedPaths = registryFiles
            .Where(row => !row.Status.StartsWith("RESERVED", StringComparison.Ordinal))
            .Select(row => row.Path)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(files, implementedPaths);
        Assert.Equal(implementedPaths.Length, implementedPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MDGsm.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("MDGsm.sln was not found above the test output directory.");
    }
}
