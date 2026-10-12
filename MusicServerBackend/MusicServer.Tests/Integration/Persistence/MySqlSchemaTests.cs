using Microsoft.EntityFrameworkCore;
using MusicServer.Domain.Entities;
using MusicServer.Infrastructure.Persistence;
using MySql.Data.MySqlClient;
using Xunit;

namespace MusicServer.Tests.Integration.Persistence;

public sealed class MySqlSchemaFactAttribute : FactAttribute
{
    public MySqlSchemaFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MUSICSERVER_MYSQL_TEST_CONNECTION")))
            Skip = "Explicit dedicated MySQL test connection has not been configured.";
    }
}

public sealed class MySqlSchemaTests
{
    [MySqlSchemaFact]
    public async Task InitialMigration_RerunsWithoutWrites_AndEnforcesOwnerAndImportConstraints()
    {
        var connection = DatabaseMigrator.RequireConnection(Environment.GetEnvironmentVariable("MUSICSERVER_MYSQL_TEST_CONNECTION"));
        // Deliberately refuse the owner's music_server_db and every other existing application database.
        Assert.Equal("music_server_schema_test", new MySqlConnectionStringBuilder(connection).Database);
        var migrator = new DatabaseMigrator();
        await migrator.RunAsync(connection, apply: true);
        Assert.Contains("already applied", await migrator.RunAsync(connection, apply: true));
        await using var context = new MusicServerDbContext(new DbContextOptionsBuilder<MusicServerDbContext>().UseMySQL(connection).Options);
        await using var transaction = await context.Database.BeginTransactionAsync();
        var now = DateTime.UtcNow;
        context.Owners.Add(new Owner { Id = 1, GoogleIssuer = "https://accounts.google.com", GoogleSubject = "schema-test", CreatedAt = now });
        await context.SaveChangesAsync();
        var secondOwner = new Owner { Id = 2, GoogleIssuer = "https://accounts.google.com", GoogleSubject = "other", CreatedAt = now };
        context.Owners.Add(secondOwner);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.Entry(secondOwner).State = EntityState.Detached;

        var clientBatch = Guid.NewGuid();
        var batch = new UploadBatch { Id = Guid.NewGuid(), OwnerId = 1, ClientBatchId = clientBatch,
            ManifestHash = new byte[32], CreatedAt = now, ExpiresAt = now.AddHours(1) };
        context.UploadBatches.Add(batch);
        await context.SaveChangesAsync();
        var repeatedBatch = new UploadBatch { Id = Guid.NewGuid(), OwnerId = 1, ClientBatchId = clientBatch,
            ManifestHash = new byte[32], CreatedAt = now, ExpiresAt = now.AddHours(1) };
        context.UploadBatches.Add(repeatedBatch);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.Entry(repeatedBatch).State = EntityState.Detached;

        var item = new UploadItem { Id = Guid.NewGuid(), BatchId = batch.Id, ClientItemId = Guid.NewGuid(),
            FileName = "schema-test.mp3", DeclaredBytes = 128, State = "selected", Attempt = 1, CreatedAt = now, UpdatedAt = now };
        context.UploadItems.Add(item);
        await context.SaveChangesAsync();
        var duplicateItem = new UploadItem { Id = Guid.NewGuid(), BatchId = batch.Id, ClientItemId = item.ClientItemId,
            FileName = item.FileName, DeclaredBytes = 128, State = "selected", Attempt = 1, CreatedAt = now, UpdatedAt = now };
        context.UploadItems.Add(duplicateItem);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.Entry(duplicateItem).State = EntityState.Detached;

        var audio = new StorageObject { Id = Guid.NewGuid(), ObjectKey = Guid.NewGuid().ToString("N"), SizeBytes = 128,
            ContentType = "audio/mpeg", ItemId = item.Id, State = "stored", CreatedAt = now, UpdatedAt = now };
        context.StorageObjects.Add(audio);
        await context.SaveChangesAsync();
        var track = new Track { Id = Guid.NewGuid(), OwnerId = 1, Sha256 = new byte[32], OriginalFileName = item.FileName,
            Title = "Café 音楽", Artist = "Unknown artist", Album = "Unknown album", AudioObjectId = audio.Id, CreatedAt = now };
        context.Tracks.Add(track);
        await context.SaveChangesAsync();
        var otherAudio = new StorageObject { Id = Guid.NewGuid(), ObjectKey = Guid.NewGuid().ToString("N"), SizeBytes = 128,
            ContentType = "audio/mpeg", State = "stored", CreatedAt = now, UpdatedAt = now };
        context.StorageObjects.Add(otherAudio);
        await context.SaveChangesAsync();
        var duplicateTrack = new Track { Id = Guid.NewGuid(), OwnerId = 1, Sha256 = new byte[32], OriginalFileName = item.FileName,
            Title = track.Title, Artist = track.Artist, Album = track.Album, AudioObjectId = otherAudio.Id, CreatedAt = now };
        context.Tracks.Add(duplicateTrack);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.Entry(duplicateTrack).State = EntityState.Detached;
        context.ChangeTracker.Clear();
        var readBack = await context.Tracks.SingleAsync();
        Assert.Equal(track.Title, readBack.Title);
        Assert.Equal(DateTimeKind.Utc, readBack.CreatedAt.Kind);
        await transaction.RollbackAsync();
        // Service-restart durability and interrupted-DDL recovery require a separate operator drill.
    }
}
