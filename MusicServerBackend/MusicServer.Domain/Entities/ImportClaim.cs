namespace MusicServer.Domain.Entities;

public sealed class ImportClaim
{
    public required byte[] Sha256 { get; set; }
    public Guid ItemId { get; set; }
    public DateTime LeaseExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
