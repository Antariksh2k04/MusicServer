namespace MusicServer.Domain.Entities;

public sealed class StorageObject
{
    public Guid Id { get; set; }
    public required string ObjectKey { get; set; }
    public long SizeBytes { get; set; }
    public required string ContentType { get; set; }
    public string? ProviderEtag { get; set; }
    public Guid? ItemId { get; set; }
    public required string State { get; set; }
    public int Attempts { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? ErrorCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
