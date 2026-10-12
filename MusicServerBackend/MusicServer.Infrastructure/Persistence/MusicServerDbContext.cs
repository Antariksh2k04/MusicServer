using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MusicServer.Domain.Entities;
using MySql.EntityFrameworkCore.Extensions;

namespace MusicServer.Infrastructure.Persistence;

public sealed class MusicServerDbContext(DbContextOptions<MusicServerDbContext> options) : DbContext(options)
{
    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<OwnerSession> Sessions => Set<OwnerSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UploadBatch> UploadBatches => Set<UploadBatch>();
    public DbSet<UploadItem> UploadItems => Set<UploadItem>();
    public DbSet<StorageObject> StorageObjects => Set<StorageObject>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<ImportClaim> ImportClaims => Set<ImportClaim>();
    public DbSet<QuotaCounter> QuotaCounters => Set<QuotaCounter>();
    public DbSet<SchemaMigration> SchemaMigrations => Set<SchemaMigration>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Owner>(entity =>
        {
            entity.ToTable("owners", table => table.HasCheckConstraint("ck_owner_singleton", "`id` = 1"));
            entity.HasKey(owner => owner.Id);
            entity.Property(owner => owner.Id).ValueGeneratedNever();
            Exact(entity.Property(owner => owner.GoogleIssuer), 255);
            Exact(entity.Property(owner => owner.GoogleSubject), 255);
            entity.Property(owner => owner.DisplayName).HasColumnType("text");
            entity.HasIndex(owner => new { owner.GoogleIssuer, owner.GoogleSubject }).IsUnique();
        });
        model.Entity<OwnerSession>(entity =>
        {
            entity.ToTable("sessions", table => table.HasCheckConstraint("ck_session_kind", "`client_kind` IN ('browser','android')"));
            entity.HasKey(session => session.Id);
            Exact(entity.Property(session => session.ClientKind), 16);
            entity.HasIndex(session => session.AccessHash).IsUnique();
            entity.HasIndex(session => new { session.ExpiresAt, session.RevokedAt });
            entity.HasOne<Owner>().WithMany().HasForeignKey(session => session.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(token => token.Id);
            entity.HasIndex(token => token.TokenHash).IsUnique();
            entity.HasOne<OwnerSession>().WithMany().HasForeignKey(token => token.SessionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RefreshToken>().WithMany().HasForeignKey(token => token.ReplacementId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<UploadBatch>(entity =>
        {
            entity.ToTable("upload_batches");
            entity.HasKey(batch => batch.Id);
            entity.HasIndex(batch => new { batch.OwnerId, batch.ClientBatchId }).IsUnique();
            entity.HasOne<Owner>().WithMany().HasForeignKey(batch => batch.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<UploadItem>(entity =>
        {
            entity.ToTable("upload_items", table =>
            {
                table.HasCheckConstraint("ck_item_accounting", "`declared_bytes` > 0 AND `declared_bytes` <= 52428800 AND `received_bytes` >= 0 AND `received_bytes` <= `declared_bytes` AND `reserved_bytes` >= 0 AND `reserved_tracks` >= 0 AND `attempt` > 0");
                table.HasCheckConstraint("ck_item_state", "`state` IN ('selected','receiving','staged','processing','waiting_duplicate','imported','duplicate','failed')");
            });
            entity.HasKey(item => item.Id);
            entity.Property(item => item.FileName).HasColumnType("text");
            entity.Property(item => item.TempPath).HasColumnType("text");
            Exact(entity.Property(item => item.State), 32);
            Exact(entity.Property(item => item.ErrorCode), 64);
            entity.HasIndex(item => new { item.BatchId, item.ClientItemId }).IsUnique();
            entity.HasIndex(item => new { item.State, item.LeaseExpiresAt });
            entity.HasOne<UploadBatch>().WithMany().HasForeignKey(item => item.BatchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Track>().WithMany().HasForeignKey(item => item.TrackId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<StorageObject>(entity =>
        {
            entity.ToTable("storage_objects", table =>
            {
                table.HasCheckConstraint("ck_object_accounting", "`size_bytes` > 0 AND `attempts` >= 0");
                table.HasCheckConstraint("ck_object_state", "`state` IN ('planned','uploading','stored','delete_pending','deleted')");
            });
            entity.HasKey(storage => storage.Id);
            Exact(entity.Property(storage => storage.ObjectKey), 128);
            Exact(entity.Property(storage => storage.ContentType), 64);
            Exact(entity.Property(storage => storage.State), 32);
            Exact(entity.Property(storage => storage.ErrorCode), 64);
            entity.Property(storage => storage.ProviderEtag).HasColumnType("text");
            entity.HasIndex(storage => storage.ObjectKey).IsUnique();
            entity.HasIndex(storage => new { storage.State, storage.NextAttemptAt });
            entity.HasOne<UploadItem>().WithMany().HasForeignKey(storage => storage.ItemId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<Track>(entity =>
        {
            entity.ToTable("tracks", table => table.HasCheckConstraint("ck_track_metadata", "(`duration_ms` IS NULL OR `duration_ms` >= 0) AND (`track_number` IS NULL OR `track_number` >= 0) AND (`disc_number` IS NULL OR `disc_number` >= 0)"));
            entity.HasKey(track => track.Id);
            entity.Property(track => track.OriginalFileName).HasColumnType("text");
            entity.Property(track => track.Title).HasColumnType("text");
            entity.Property(track => track.Artist).HasColumnType("text");
            entity.Property(track => track.Album).HasColumnType("text");
            entity.Property(track => track.AlbumArtist).HasColumnType("text");
            entity.HasIndex(track => track.Sha256).IsUnique();
            entity.HasIndex(track => track.AudioObjectId).IsUnique();
            entity.HasIndex(track => new { track.OwnerId, track.CreatedAt });
            entity.HasOne<Owner>().WithMany().HasForeignKey(track => track.OwnerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StorageObject>().WithMany().HasForeignKey(track => track.AudioObjectId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ImportClaim>(entity =>
        {
            entity.ToTable("import_claims");
            entity.HasKey(claim => claim.Sha256);
            entity.HasIndex(claim => claim.ItemId).IsUnique();
            entity.HasOne<UploadItem>().WithMany().HasForeignKey(claim => claim.ItemId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<QuotaCounter>(entity =>
        {
            entity.ToTable("quota_counters", table => table.HasCheckConstraint("ck_quota_accounting", "`used` >= 0 AND `reserved` >= 0 AND `limit` >= 0 AND `used` <= `limit` AND `reserved` <= `limit` - `used`"));
            Exact(entity.Property(counter => counter.Metric), 64);
            entity.HasKey(counter => new { counter.Metric, counter.PeriodStart });
        });
        model.Entity<SchemaMigration>(entity =>
        {
            entity.ToTable("schema_migrations");
            entity.HasKey(migration => migration.Version);
            entity.Property(migration => migration.Version).ValueGeneratedNever();
        });

        foreach (var entity in model.Model.GetEntityTypes())
        {
            model.Entity(entity.ClrType).ForMySQLHasCharset("utf8mb4");
            model.Entity(entity.ClrType).ForMySQLHasCollation("utf8mb4_0900_as_ci");
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(SnakeCase(property.Name));
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                var configured = model.Entity(entity.ClrType).Property(property.Name);
                if (type == typeof(Guid))
                    configured.ForMySQLHasCharset("ascii").ForMySQLHasCollation("ascii_bin");
                else if (type == typeof(byte[])) configured.HasColumnType("binary(32)").HasMaxLength(32).IsFixedLength();
                else if (type == typeof(DateTime)) configured.HasColumnType("datetime(6)").HasConversion(UtcConverter);
            }
        }
        MySQLModelBuilderExtensions.UseCollation(model, "utf8mb4_0900_as_ci");
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateDigests();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateDigests();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ValidateDigests()
    {
        foreach (var entry in ChangeTracker.Entries().Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        foreach (var property in entry.Properties.Where(property => property.Metadata.ClrType == typeof(byte[])))
            if (property.CurrentValue is byte[] bytes && bytes.Length != 32)
                throw new InvalidOperationException("Database digests must contain exactly 32 bytes.");
    }

    private static readonly ValueConverter<DateTime, DateTime> UtcConverter = new(
        value => RequireUtc(value), value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private static DateTime RequireUtc(DateTime value) => value.Kind == DateTimeKind.Utc
        ? value : throw new InvalidOperationException("Database timestamps must be UTC.");
    private static void Exact(PropertyBuilder property, int length) => property.HasMaxLength(length).ForMySQLHasCollation("utf8mb4_0900_bin");
    private static string SnakeCase(string name) => string.Concat(name.Select((letter, index) =>
        char.IsUpper(letter) && index > 0 ? "_" + char.ToLowerInvariant(letter) : char.ToLowerInvariant(letter).ToString()));
}
