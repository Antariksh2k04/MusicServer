namespace MusicServer.Domain.Entities;

public sealed class UploadBatch
{
    public Guid Id { get; set; }
    public int OwnerId { get; set; }
    public Guid ClientBatchId { get; set; }
    public required byte[] ManifestHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
