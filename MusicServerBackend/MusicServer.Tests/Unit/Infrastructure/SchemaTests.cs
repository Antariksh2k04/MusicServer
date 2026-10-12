using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MusicServer.Domain.Entities;
using MusicServer.Infrastructure.Persistence;
using Xunit;

namespace MusicServer.Tests.Unit.Infrastructure;

public sealed class SchemaTests
{
    private static MusicServerDbContext CreateContext() => new(new DbContextOptionsBuilder<MusicServerDbContext>()
        .UseMySQL("Server=127.0.0.1;Port=1;Database=offline_schema;User ID=offline;Password=;Connection Timeout=1").Options);

    [Fact]
    public void CheckedSql_EqualsProviderGeneratedMigration_WithoutDatabase()
    {
        using var context = CreateContext();
        var artifact = MigrationArtifact.Initial();
        var generated = context.GetService<IMigrator>().GenerateScript("0", artifact.MigrationId)
            .TrimStart('\uFEFF').Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Equal(artifact.Sql, generated);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void GeneratedSchema_PreservesExactIdentityAndDigestUniqueness()
    {
        var sql = MigrationArtifact.Initial().Sql;
        Assert.Contains("`google_subject` varchar(255) COLLATE utf8mb4_0900_bin", sql);
        Assert.Contains("`id` char(36) CHARACTER SET ascii COLLATE ascii_bin", sql);
        Assert.Contains("`access_hash` binary(32)", sql);
        Assert.Contains("CONSTRAINT `ck_owner_singleton` CHECK (`id` = 1)", sql);
        Assert.Contains("CREATE UNIQUE INDEX `IX_tracks_sha256`", sql);
        Assert.Contains("CREATE UNIQUE INDEX `IX_upload_batches_owner_id_client_batch_id`", sql);
        Assert.Contains("CREATE UNIQUE INDEX `IX_upload_items_batch_id_client_item_id`", sql);
        Assert.Contains("SET SESSION default_storage_engine = 'InnoDB'", sql);
        Assert.Contains("COLLATE utf8mb4_0900_as_ci", sql);
    }

    [Fact]
    public void GeneratedSchema_CreatesReferencedTablesBeforeAddingCyclicForeignKeys()
    {
        var sql = MigrationArtifact.Initial().Sql;
        var foreignKey = sql.IndexOf("ALTER TABLE `storage_objects` ADD CONSTRAINT", StringComparison.Ordinal);
        Assert.True(foreignKey > sql.IndexOf("CREATE TABLE `upload_items`", StringComparison.Ordinal));
        Assert.True(foreignKey > sql.IndexOf("CREATE TABLE `tracks`", StringComparison.Ordinal));
        Assert.DoesNotContain("ON DELETE CASCADE", sql);
    }

    [Fact]
    public void TamperedArtifact_IsRejected()
    {
        var artifact = MigrationArtifact.Initial();
        var manifest = JsonSerializer.Serialize(new { version = artifact.Version, migrationId = artifact.MigrationId,
            sha256 = Convert.ToHexString(artifact.Checksum) });
        Assert.Throws<InvalidOperationException>(() => MigrationArtifact.Verify(artifact.Sql + "SELECT 1;", manifest));
        Assert.Equal(artifact.Sql, MigrationArtifact.Verify(artifact.Sql.Replace("\n", "\r\n", StringComparison.Ordinal), manifest).Sql);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Server=localhost;User ID=operator")]
    [InlineData("Server=localhost;Database=music_server_db;Old Guids=true")]
    public void MigrationConnection_MustBeExplicitAndSelectDatabase(string? connection)
    {
        Assert.Throws<InvalidOperationException>(() => DatabaseMigrator.RequireConnection(connection));
    }

    [Fact]
    public async Task ShortDigest_IsRejectedBeforeOpeningDatabase()
    {
        using var context = CreateContext();
        context.ImportClaims.Add(new ImportClaim { Sha256 = [1], ItemId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow, LeaseExpiresAt = DateTime.UtcNow });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        Assert.Contains("exactly 32 bytes", error.Message);
    }

    [Fact]
    public void TimestampConverter_RejectsLocalTime_AndRestoresUtc()
    {
        using var context = CreateContext();
        var converter = context.Model.FindEntityType(typeof(Owner))!.FindProperty(nameof(Owner.CreatedAt))!.GetValueConverter()!;
        Assert.Throws<InvalidOperationException>(() => converter.ConvertToProvider(DateTime.Now));
        var timestamp = Assert.IsType<DateTime>(converter.ConvertFromProvider(new DateTime(2026, 10, 12)));
        Assert.Equal(DateTimeKind.Utc, timestamp.Kind);
    }
}
