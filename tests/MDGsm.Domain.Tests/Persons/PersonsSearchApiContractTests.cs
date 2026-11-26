using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PersonsSearchApiContractTests
{
    [Fact]
    public void Search_http_is_explicitly_disabled_and_requires_persistent_controls()
    {
        var root = FindRepositoryRoot();
        var apiSettings = File.ReadAllText(Path.Combine(root, "src", "Host", "Api", "appsettings.json"));
        var program = File.ReadAllText(Path.Combine(root, "src", "Host", "Api", "Program.cs"));

        Assert.Contains("\"SearchApiEnabled\": false", apiSettings, StringComparison.Ordinal);
        Assert.Contains("Persons:SearchApiEnabled", program, StringComparison.Ordinal);
        Assert.Contains("IPersonsSearchControl", program, StringComparison.Ordinal);
        Assert.Contains("persons.search", program, StringComparison.Ordinal);
        Assert.Contains("RoleCatalog.SuperAdmin", program, StringComparison.Ordinal);
        Assert.Contains("AdminUnlockAsync", program, StringComparison.Ordinal);
        Assert.Contains("RecordSecurityViolationAsync", program, StringComparison.Ordinal);
        Assert.Contains("Persons:HistoryApiEnabled", program, StringComparison.Ordinal);
        Assert.Contains("persons.sensitive.read", program, StringComparison.Ordinal);
        Assert.DoesNotContain("persons.contact.update", program, StringComparison.Ordinal);
        Assert.DoesNotContain("persons.address.update", program, StringComparison.Ordinal);
        Assert.Contains("RoleCatalog.SuperAdmin", program, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MDGsm.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("MDGsm.sln was not found above the test output directory.");
    }
}
