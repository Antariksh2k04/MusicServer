namespace MusicServer.Domain.Entities;

public sealed class Owner
{
    public int Id { get; set; }
    public required string GoogleIssuer { get; set; }
    public required string GoogleSubject { get; set; }
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; }
}
