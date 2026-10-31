using MDGsm.Infrastructure.Persistence;

if (args.Any(argument => argument is "--help" or "-h"))
{
    Console.WriteLine("MD-Gsm database migrator");
    Console.WriteLine("Reads ConnectionStrings__MDGsmDatabase from the process environment.");
    Console.WriteLine("By default, the target database must contain the PostgreSQL comment marker MDGSM_DISPOSABLE_TEST_DB.");
    Console.WriteLine("Use --allow-unmarked-database only for an explicitly approved deployment operation.");
    return 0;
}

var unknownArguments = args.Where(argument => argument != "--allow-unmarked-database").ToArray();
if (unknownArguments.Length > 0)
{
    Console.Error.WriteLine($"Unsupported argument(s): {string.Join(", ", unknownArguments)}");
    return 2;
}

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__MDGsmDatabase");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("FAIL: ConnectionStrings__MDGsmDatabase is not configured in the process environment.");
    return 2;
}

var allowUnmarkedDatabase = args.Contains("--allow-unmarked-database", StringComparer.Ordinal);
try
{
    var result = await new DatabaseMigrationRunner().ApplyPendingAsync(
        connectionString,
        requireDisposableMarker: !allowUnmarkedDatabase);

    Console.WriteLine(
        $"PASS: migration run completed; database={result.DatabaseName}; " +
        $"newlyApplied={result.NewlyAppliedVersions.Count}; previouslyApplied={result.PreviouslyAppliedCount}; " +
        $"versions={string.Join(",", result.NewlyAppliedVersions)}");
    return 0;
}
catch (Exception exception) when (exception is not OperationCanceledException)
{
    Console.Error.WriteLine($"FAIL: database migration runner stopped ({exception.GetType().Name}). No credentials were logged.");
    return 1;
}
