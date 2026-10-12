namespace MusicServer.Domain.Entities;

public sealed class QuotaCounter
{
    public required string Metric { get; set; }
    public DateTime PeriodStart { get; set; }
    public long Used { get; set; }
    public long Reserved { get; set; }
    public long Limit { get; set; }
    public DateTime UpdatedAt { get; set; }
}
