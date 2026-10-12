using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MusicServer.Infrastructure.Persistence;

// Offline tooling uses a deliberately unusable connection, never an operator credential.
// Production/runtime registration is a separate task; this factory never migrates a database.
public sealed class MusicServerDbContextFactory : IDesignTimeDbContextFactory<MusicServerDbContext>
{
    public MusicServerDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__MusicServerMigration");
        connection = string.IsNullOrWhiteSpace(connection)
            ? "Server=127.0.0.1;Port=1;Database=music_server_db;User ID=offline_tooling;Password=;Connection Timeout=1"
            : connection;
        return new(new DbContextOptionsBuilder<MusicServerDbContext>().UseMySQL(connection).Options);
    }
}
