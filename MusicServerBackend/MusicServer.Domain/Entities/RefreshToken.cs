namespace MusicServer.Domain.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public required byte[] TokenHash { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public Guid? ReplacementId { get; set; }
    public DateTime CreatedAt { get; set; }
}
