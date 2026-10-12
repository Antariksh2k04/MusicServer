namespace MusicServer.Domain.Entities;

public sealed class SchemaMigration
{
    public int Version { get; set; }
    public required byte[] Checksum { get; set; }
    public DateTime AppliedAt { get; set; }
}
