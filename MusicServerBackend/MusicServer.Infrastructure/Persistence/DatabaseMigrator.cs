using System.Security.Cryptography;
using MySql.Data.MySqlClient;

namespace MusicServer.Infrastructure.Persistence;

// Explicit operator tooling only. Never registered or invoked during API startup.
// This release contains one migration. Later releases must extend ordered-history verification.
public sealed class DatabaseMigrator
{
    private static readonly string[] Tables = ["__EFMigrationsHistory", "owners", "sessions", "refresh_tokens",
        "upload_batches", "upload_items", "storage_objects", "tracks", "import_claims", "quota_counters", "schema_migrations"];

    public static string RequireConnection(string? connection)
    {
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Set ConnectionStrings__MusicServerMigration before database operations.");
        var settings = new MySqlConnectionStringBuilder(connection);
        if (string.IsNullOrWhiteSpace(settings.Database))
            throw new InvalidOperationException("Migration connection must select an existing dedicated MusicServer database.");
        if (settings.OldGuids)
            throw new InvalidOperationException("OldGuids must be disabled for CHAR(36) identifiers.");
        return settings.ConnectionString;
    }

    public async Task<string> RunAsync(string? connectionString, bool apply, CancellationToken cancellationToken = default)
    {
        var artifact = MigrationArtifact.Initial(); // Verify before connecting or issuing SQL.
        await using var connection = new MySqlConnection(RequireConnection(connectionString));
        await connection.OpenAsync(cancellationToken);
        var lockName = "music-schema-" + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(connection.Database)))[..40];
        using var acquire = new MySqlCommand("SELECT GET_LOCK(@name, 0)", connection);
        acquire.Parameters.AddWithValue("@name", lockName);
        if (Convert.ToInt32(await acquire.ExecuteScalarAsync(cancellationToken)) != 1)
            throw new InvalidOperationException("Another MusicServer migration operation is running.");
        try
        {
            using var serverVersion = new MySqlCommand("SELECT VERSION()", connection);
            var versionText = Convert.ToString(await serverVersion.ExecuteScalarAsync(cancellationToken))!;
            if (versionText.Contains("MariaDB", StringComparison.OrdinalIgnoreCase)
                || !Version.TryParse(versionText.Split('-')[0], out var version) || version < new Version(8, 0, 16))
                throw new InvalidOperationException("Migration requires MySQL 8.0.16 or newer with enforced CHECK constraints.");
            using var engine = new MySqlCommand("SELECT @@default_storage_engine", connection);
            if (!string.Equals(Convert.ToString(await engine.ExecuteScalarAsync(cancellationToken)), "InnoDB", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Migration requires the InnoDB default storage engine.");
            var tables = new List<string>();
            using var identifierMode = new MySqlCommand("SELECT @@lower_case_table_names", connection);
            var lowerCaseTableNames = Convert.ToInt32(await identifierMode.ExecuteScalarAsync(cancellationToken));
            using (var inventory = new MySqlCommand("SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE()", connection))
            await using (var reader = await inventory.ExecuteReaderAsync(cancellationToken))
                while (await reader.ReadAsync(cancellationToken)) tables.Add(reader.GetString(0));

            if (tables.Count == 0)
            {
                if (!apply) return "Empty database; initial migration is pending.";
                // DDL implicitly commits. Any failure leaves evidence and the next invocation refuses repair.
                await new MySqlScript(connection, artifact.Sql).ExecuteAsync(cancellationToken);
                using var journal = new MySqlCommand("INSERT INTO schema_migrations (version, checksum, applied_at) VALUES (@version, @checksum, UTC_TIMESTAMP(6))", connection);
                journal.Parameters.AddWithValue("@version", artifact.Version);
                journal.Parameters.AddWithValue("@checksum", artifact.Checksum);
                await journal.ExecuteNonQueryAsync(cancellationToken);
                return "Initial migration applied; EF history and SQL checksum recorded.";
            }

            VerifyTables(tables, lowerCaseTableNames);
            using (var history = new MySqlCommand("SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId", connection))
            await using (var reader = await history.ExecuteReaderAsync(cancellationToken))
                if (!await reader.ReadAsync(cancellationToken) || reader.GetString(0) != artifact.MigrationId || await reader.ReadAsync(cancellationToken))
                    throw new MigrationCheckException(MigrationCheckFailure.HistoryMismatch);
            using (var journal = new MySqlCommand("SELECT version, checksum FROM schema_migrations ORDER BY version", connection))
            await using (var reader = await journal.ExecuteReaderAsync(cancellationToken))
                if (!await reader.ReadAsync(cancellationToken) || reader.GetInt32(0) != artifact.Version
                    || !CryptographicOperations.FixedTimeEquals((byte[])reader.GetValue(1), artifact.Checksum)
                    || await reader.ReadAsync(cancellationToken)) throw new MigrationCheckException(MigrationCheckFailure.ChecksumMismatch);
            return "Initial migration already applied; history and checksum match. No changes made.";
        }
        finally
        {
            // Closing the session also releases its advisory lock if this best-effort release fails.
            try
            {
                using var release = new MySqlCommand("SELECT RELEASE_LOCK(@name)", connection);
                release.Parameters.AddWithValue("@name", lockName);
                await release.ExecuteScalarAsync(CancellationToken.None);
            }
            catch (Exception exception) when (exception is MySqlException or InvalidOperationException) { }
        }
    }

    public static void VerifyTables(IReadOnlyCollection<string> tables, int lowerCaseTableNames)
    {
        // Modes 1 and 2 use case-insensitive lookup; mode 0 preserves case-sensitive Unix lookup.
        // Do not apply identifier rules to identity proofs, migration IDs, or checksums.
        var comparer = lowerCaseTableNames switch
        {
            0 => StringComparer.Ordinal,
            1 or 2 => StringComparer.OrdinalIgnoreCase,
            _ => throw new MigrationCheckException(MigrationCheckFailure.IdentifierModeUnsupported)
        };
        if (tables.Count != Tables.Length || !new HashSet<string>(tables, comparer).SetEquals(Tables))
            throw new MigrationCheckException(MigrationCheckFailure.TablesMismatch);
    }
}
