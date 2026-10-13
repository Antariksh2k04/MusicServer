namespace MusicServer.Domain.Entities;

public sealed class UploadItem
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public Guid ClientItemId { get; set; }
    public required string FileName { get; set; }
    public long DeclaredBytes { get; set; }
    public long ReceivedBytes { get; set; }
    public byte[]? Sha256 { get; set; }
    public string? TempPath { get; set; }
    public Guid? TrackId { get; set; }
    public string? ErrorCode { get; set; }
    public required string State { get; set; }
    public int Attempt { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public long ReservedBytes { get; set; }
    public int ReservedTracks { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
