namespace MusicServer.Storage;

// Internal keys and checksums never belong in public library DTOs or media URLs.
public sealed record StoredObject(Guid Key, long SizeBytes, string Sha256)
{
    public string ContentType => "audio/mpeg";
    public string Etag => $"\"{Sha256}-{SizeBytes}\"";
}

