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
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MDGsm.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("MDGsm.sln was not found above the test output directory.");
    }
}
