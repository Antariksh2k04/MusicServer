using Microsoft.EntityFrameworkCore;
using MusicServer.Infrastructure.Persistence;
using MySql.Data.MySqlClient;
using System.Runtime.CompilerServices;
using Xunit;

namespace MusicServer.Tests.Unit.Infrastructure;

public sealed class MigrationInspectionTests
{
    private static string[] SchemaTables()
    {
        using var context = new MusicServerDbContext(new DbContextOptionsBuilder<MusicServerDbContext>()
            .UseMySQL("Server=127.0.0.1;Port=1;Database=offline_schema;User ID=offline;Password=").Options);
        return context.Model.GetEntityTypes().Select(entity => entity.GetTableName()!).Append("__EFMigrationsHistory").ToArray();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void WindowsOrMacIdentifierRules_AcceptLowercaseEfHistory(int mode)
    {
        var inventory = SchemaTables().Select(table => table.ToLowerInvariant()).ToArray();
        DatabaseMigrator.VerifyTables(inventory, mode);
    }

    [Fact]
    public void UnixIdentifierRules_RequireExactEfHistoryCase()
    {
        DatabaseMigrator.VerifyTables(SchemaTables(), 0);
        var lowercased = SchemaTables().Select(table => table.ToLowerInvariant()).ToArray();
        var error = Assert.Throws<MigrationCheckException>(() => DatabaseMigrator.VerifyTables(lowercased, 0));
        Assert.Contains("schemaTablesMismatch", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void MissingExtraOrRepeatedTables_AreRejectedInEveryMode(int mode)
    {
        var tables = SchemaTables();
        Assert.Throws<MigrationCheckException>(() => DatabaseMigrator.VerifyTables(tables.Skip(1).ToArray(), mode));
        Assert.Throws<MigrationCheckException>(() => DatabaseMigrator.VerifyTables(tables.Append("other_application_table").ToArray(), mode));
        Assert.Throws<MigrationCheckException>(() => DatabaseMigrator.VerifyTables(tables.Skip(1).Append(tables[1]).ToArray(), mode));
    }

    [Fact]
    public void UnknownServerIdentifierMode_IsRejected()
    {
        var error = Assert.Throws<MigrationCheckException>(() => DatabaseMigrator.VerifyTables(SchemaTables(), 3));
        Assert.Contains("identifierModeUnsupported", error.Message);
    }

    [Theory]
    [InlineData(MigrationCheckFailure.HistoryMismatch, "migrationHistoryMismatch")]
    [InlineData(MigrationCheckFailure.ChecksumMismatch, "migrationChecksumMismatch")]
    [InlineData(MigrationCheckFailure.TablesMismatch, "schemaTablesMismatch")]
    public void InspectionDiagnostics_IdentifyFailedGate(MigrationCheckFailure failure, string code)
    {
        Assert.Contains(code, MigrationDiagnostics.Describe(new MigrationCheckException(failure)));
    }

    [Fact]
    public void DiagnosticOutput_DoesNotExposeProviderOrUnexpectedExceptionDetails()
    {
        const string secret = "test-only-private-connection-value";
        // Connector/NET has no public exception constructor. A zero-initialized exception supplies
        // the numeric error code without invoking the driver or contacting a database.
        var providerException = (MySqlException)RuntimeHelpers.GetUninitializedObject(typeof(MySqlException));
        providerException.Data["provider-detail"] = secret;
        var mysqlError = MigrationDiagnostics.Describe(providerException);
        Assert.Contains("MySQL error", mysqlError);
        Assert.DoesNotContain(secret, mysqlError);
        Assert.DoesNotContain(secret, MigrationDiagnostics.Describe(new InvalidOperationException(secret)));
    }
}
