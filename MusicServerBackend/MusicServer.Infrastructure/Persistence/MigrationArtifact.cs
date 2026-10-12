using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MusicServer.Infrastructure.Persistence;

public sealed record MigrationArtifact(int Version, string MigrationId, string Sql, byte[] Checksum)
{
    public static MigrationArtifact Initial()
    {
        var assembly = typeof(MigrationArtifact).Assembly;
        string Read(string suffix)
        {
            var name = assembly.GetManifestResourceNames().Single(name => name.EndsWith(suffix, StringComparison.Ordinal));
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        return Verify(Read("0001-initial-music.sql"), Read("0001-initial-music.json"));
    }

    public static MigrationArtifact Verify(string sql, string manifest)
    {
        using var document = JsonDocument.Parse(manifest);
        var root = document.RootElement;
        var version = root.GetProperty("version").GetInt32();
        var id = root.GetProperty("migrationId").GetString()!;
        var checksum = Convert.FromHexString(root.GetProperty("sha256").GetString()!);
        // Artifacts use UTF-8 without BOM and LF endings on every platform.
        sql = sql.TrimStart('\uFEFF').Replace("\r\n", "\n", StringComparison.Ordinal);
        if (version != 1 || id != "20261012061305_InitialMusicSchema" || checksum.Length != 32
            || !CryptographicOperations.FixedTimeEquals(checksum, SHA256.HashData(Encoding.UTF8.GetBytes(sql))))
            throw new InvalidOperationException("Migration artifact identity or checksum is invalid.");
        return new(version, id, sql, checksum);
    }
}
