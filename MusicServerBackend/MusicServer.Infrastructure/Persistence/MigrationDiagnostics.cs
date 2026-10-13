using MySql.Data.MySqlClient;

namespace MusicServer.Infrastructure.Persistence;

public enum MigrationCheckFailure
{
    TablesMismatch,
    HistoryMismatch,
    ChecksumMismatch,
    IdentifierModeUnsupported
}

// Messages are controlled constants. Never include provider text, credentials, or database contents.
public sealed class MigrationCheckException(MigrationCheckFailure failure) : InvalidOperationException(failure switch
{
    MigrationCheckFailure.TablesMismatch => "schemaTablesMismatch: Database tables do not match the initial schema under the server's identifier rules. No automatic repair was attempted.",
    MigrationCheckFailure.HistoryMismatch => "migrationHistoryMismatch: EF migration history is missing, unexpected, or incomplete. No automatic repair was attempted.",
    MigrationCheckFailure.ChecksumMismatch => "migrationChecksumMismatch: SQL checksum journal is missing, different, or incomplete. No automatic repair was attempted.",
    _ => "identifierModeUnsupported: The server's lower_case_table_names value is unsupported. No automatic repair was attempted."
});

public static class MigrationDiagnostics
{
    public static string Describe(Exception exception) => exception switch
    {
        MigrationCheckException check => "Database operation failed. " + check.Message + " Follow docs/database-migrations.md.",
        MySqlException mysql => $"Database operation failed (MySQL error {mysql.Number}). Check the connection, permissions, and migration recovery instructions. Provider details and credentials were not logged.",
        _ => "Database operation failed. Check the migration configuration and recovery instructions in docs/database-migrations.md. Credentials were not logged."
    };
}
