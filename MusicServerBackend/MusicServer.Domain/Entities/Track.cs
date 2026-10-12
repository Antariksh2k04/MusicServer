namespace MusicServer.Domain.Entities;

public sealed class Track
{
    public Guid Id { get; set; }
    public int OwnerId { get; set; }
    public required byte[] Sha256 { get; set; }
    public required string OriginalFileName { get; set; }
    public required string Title { get; set; }
    public required string Artist { get; set; }
    public required string Album { get; set; }
    public string? AlbumArtist { get; set; }
    public int? TrackNumber { get; set; }
    public int? DiscNumber { get; set; }
    public long? DurationMs { get; set; }
    public Guid AudioObjectId { get; set; }
    public DateTime CreatedAt { get; set; }
}
