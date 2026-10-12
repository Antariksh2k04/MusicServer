namespace MusicServer.Domain.Entities;

public sealed class OwnerSession
{
    public Guid Id { get; set; }
    public int OwnerId { get; set; }
    public required string ClientKind { get; set; }
    public required byte[] AccessHash { get; set; }
    public DateTime AccessExpiresAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
