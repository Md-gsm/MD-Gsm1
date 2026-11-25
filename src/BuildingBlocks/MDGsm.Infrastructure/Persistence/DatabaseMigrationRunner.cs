using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Npgsql;
using MDGsm.Persons.Infrastructure;

namespace MDGsm.Infrastructure.Persistence;

public sealed class DatabaseMigrationRunner
{
    private const string ResourcePrefix = "MDGsm.Migrations.";
    private const string DisposableMarker = "MDGSM_DISPOSABLE_TEST_DB";
    private static readonly Regex MigrationFileName = new(
        @"^(?<version>[0-9]{4})_(?<name>[a-z0-9_]+)\.sql$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<MigrationRunResult> ApplyPendingAsync(
        string connectionString,
        bool requireDisposableMarker = true,
        CancellationToken cancellationToken = default,
        string? migrationLoginRole = null,
        string? schemaOwnerRole = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (string.IsNullOrWhiteSpace(migrationLoginRole) != string.IsNullOrWhiteSpace(schemaOwnerRole))
        {
            throw new ArgumentException("Migration login and schema-owner roles must be supplied together.");
        }
        var migrations = LoadMigrations();
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Timeout = 15,
            CommandTimeout = 120,
            ApplicationName = "MD-Gsm.DbMigrator"
        };

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        if (requireDisposableMarker)
        {
            await using var markerCommand = new NpgsqlCommand(
                "SELECT COALESCE(shobj_description(oid, 'pg_database'), '') " +
                "FROM pg_database WHERE datname = current_database();", connection);
            var marker = (string?)await markerCommand.ExecuteScalarAsync(cancellationToken) ?? string.Empty;
            if (!marker.Contains(DisposableMarker, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Database '{connection.Database}' is not marked {DisposableMarker}; refusing to migrate it.");
            }
        }

        if (migrationLoginRole is not null && schemaOwnerRole is not null)
        {
            await AssumeRegisteredSchemaOwnerAsync(
                connection, migrationLoginRole, schemaOwnerRole, cancellationToken);
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var lockCommand = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(1296319793, 1);", connection, transaction))
        {
            await lockCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var historyCommand = new NpgsqlCommand(
            """
            CREATE TABLE IF NOT EXISTS mdgsm_migration_history (
                migration_version integer PRIMARY KEY,
                migration_name varchar(160) NOT NULL UNIQUE,
                sha256 char(64) NOT NULL,
                applied_at_utc timestamptz NOT NULL DEFAULT now()
            );
            """, connection, transaction))
        {
            await historyCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var applied = new Dictionary<int, (string Name, string Sha256)>();
        await using (var readHistory = new NpgsqlCommand(
            "SELECT migration_version, migration_name, sha256 FROM mdgsm_migration_history ORDER BY migration_version;",
            connection, transaction))
        await using (var reader = await readHistory.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                applied.Add(reader.GetInt32(0), (reader.GetString(1), reader.GetString(2).Trim()));
            }
        }

        var byVersion = migrations.ToDictionary(migration => migration.Version);
        foreach (var row in applied)
        {
            if (!byVersion.TryGetValue(row.Key, out var migration))
            {
                throw new InvalidOperationException(
                    $"Database contains migration version {row.Key}, but this application build does not include it.");
            }

            if (!string.Equals(migration.Name, row.Value.Name, StringComparison.Ordinal) ||
                !string.Equals(migration.Sha256, row.Value.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Applied migration version {row.Key} has a name or checksum mismatch; refusing to continue.");
            }
        }

        var highestAppliedVersion = applied.Keys.DefaultIfEmpty(0).Max();
        var newlyApplied = new List<int>();
        foreach (var migration in migrations)
        {
            if (applied.ContainsKey(migration.Version))
            {
                continue;
            }

            if (migration.Version < highestAppliedVersion)
            {
                throw new InvalidOperationException(
                    $"Migration version {migration.Version} is missing below an already-applied version; refusing out-of-order migration.");
            }

            await using (var applyCommand = new NpgsqlCommand(migration.Sql, connection, transaction))
            {
                applyCommand.CommandTimeout = 120;
                await applyCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var recordCommand = new NpgsqlCommand(
                """
                INSERT INTO mdgsm_migration_history
                    (migration_version, migration_name, sha256)
                VALUES (@version, @name, @sha256);
                """, connection, transaction))
            {
                recordCommand.Parameters.AddWithValue("version", migration.Version);
                recordCommand.Parameters.AddWithValue("name", migration.Name);
                recordCommand.Parameters.AddWithValue("sha256", migration.Sha256);
                await recordCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            newlyApplied.Add(migration.Version);
        }

        await transaction.CommitAsync(cancellationToken);
        return new MigrationRunResult(connection.Database, newlyApplied, applied.Count);
    }

    private static async Task AssumeRegisteredSchemaOwnerAsync(
        NpgsqlConnection connection,
        string expectedLoginRole,
        string schemaOwnerRole,
        CancellationToken cancellationToken)
    {
        ValidateRoleName(expectedLoginRole, nameof(expectedLoginRole));
        ValidateRoleName(schemaOwnerRole, nameof(schemaOwnerRole));

        await using (var validate = new NpgsqlCommand("""
            SELECT
                session_user = @login,
                login_role.rolcanlogin AND NOT login_role.rolsuper
                    AND NOT login_role.rolcreatedb AND NOT login_role.rolcreaterole
                    AND NOT login_role.rolreplication AND NOT login_role.rolbypassrls
                    AND NOT login_role.rolinherit,
                NOT owner_role.rolcanlogin AND NOT owner_role.rolsuper
                    AND NOT owner_role.rolcreatedb AND NOT owner_role.rolcreaterole
                    AND NOT owner_role.rolreplication AND NOT owner_role.rolbypassrls,
                (SELECT count(*) FROM pg_auth_members m WHERE m.member=login_role.oid),
                (SELECT count(*) FROM pg_auth_members m
                 WHERE m.member=login_role.oid AND m.roleid=owner_role.oid
                   AND NOT m.admin_option AND NOT m.inherit_option AND m.set_option)
            FROM pg_roles login_role
            CROSS JOIN pg_roles owner_role
            WHERE login_role.rolname=@login AND owner_role.rolname=@owner;
            """, connection))
        {
            validate.Parameters.AddWithValue("login", expectedLoginRole);
            validate.Parameters.AddWithValue("owner", schemaOwnerRole);
            await using var reader = await validate.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) ||
                !reader.GetBoolean(0) || !reader.GetBoolean(1) || !reader.GetBoolean(2) ||
                reader.GetInt64(3) != 1 || reader.GetInt64(4) != 1)
            {
                throw new InvalidOperationException(
                    "Migration identity does not match the registered non-inheriting LOGIN-to-NOLOGIN schema-owner boundary.");
            }
        }

        await using (var assume = new NpgsqlCommand($"SET ROLE {QuoteRole(schemaOwnerRole)};", connection))
        {
            await assume.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var verify = new NpgsqlCommand("SELECT session_user, current_user;", connection);
        await using var identity = await verify.ExecuteReaderAsync(cancellationToken);
        if (!await identity.ReadAsync(cancellationToken) ||
            !string.Equals(identity.GetString(0), expectedLoginRole, StringComparison.Ordinal) ||
            !string.Equals(identity.GetString(1), schemaOwnerRole, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("PostgreSQL did not enter the registered schema-owner role as expected.");
        }
    }

    private static void ValidateRoleName(string roleName, string parameterName)
    {
        if (!Regex.IsMatch(roleName, @"^[a-z_][a-z0-9_]{0,62}$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("PostgreSQL role name is not a permitted canonical identifier.", parameterName);
        }
    }

    private static string QuoteRole(string roleName) => "\"" + roleName + "\"";

    private static IReadOnlyList<MigrationArtifact> LoadMigrations()
    {
        var assemblies = new[]
        {
            typeof(DatabaseMigrationRunner).Assembly,
            typeof(PersonsInfrastructureAssemblyMarker).Assembly
        }.Distinct();
        var migrations = new List<MigrationArtifact>();

        foreach (var assembly in assemblies)
        {
            foreach (var resourceName in assembly.GetManifestResourceNames())
            {
                if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var fileName = resourceName[ResourcePrefix.Length..];
                var match = MigrationFileName.Match(fileName);
                if (!match.Success)
                {
                    throw new InvalidOperationException($"Embedded migration resource has an invalid name: {fileName}");
                }

                using var stream = assembly.GetManifestResourceStream(resourceName)
                    ?? throw new InvalidOperationException($"Embedded migration resource could not be opened: {fileName}");
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var sql = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(sql))
                {
                    throw new InvalidOperationException($"Embedded migration is empty: {fileName}");
                }

                var version = int.Parse(match.Groups["version"].Value, System.Globalization.CultureInfo.InvariantCulture);
                var sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))).ToLowerInvariant();
                migrations.Add(new MigrationArtifact(version, Path.GetFileNameWithoutExtension(fileName), sql, sha256));
            }
        }

        if (migrations.Count == 0)
        {
            throw new InvalidOperationException("No embedded SQL migrations were found in this application build.");
        }

        var ordered = migrations.OrderBy(migration => migration.Version).ToArray();
        if (ordered.Select(migration => migration.Version).Distinct().Count() != ordered.Length)
        {
            throw new InvalidOperationException("Duplicate migration version detected in embedded resources.");
        }

        return ordered;
    }

    private sealed record MigrationArtifact(int Version, string Name, string Sql, string Sha256);
}

public sealed record MigrationRunResult(
    string DatabaseName,
    IReadOnlyList<int> NewlyAppliedVersions,
    int PreviouslyAppliedCount);
