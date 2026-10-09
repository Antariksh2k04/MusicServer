namespace MusicServer.Storage;

// Internal keys and checksums never belong in public library DTOs or media URLs.
public sealed record StoredObject(Guid Key, long SizeBytes, string Sha256)
{
    public string ContentType => "audio/mpeg";
    public string Etag => $"\"{Sha256}-{SizeBytes}\"";
}

public sealed record ObjectRead(StoredObject Metadata, long Start, long Length, Stream Content) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public interface IObjectStore
{
    // A successful PUT validates length/checksum and publishes immutable original bytes.
    // Matching key/length/checksum retries are idempotent; conflicting keys never overwrite.
    Task<StoredObject> PutAsync(Guid key, Stream source, long length, string sha256, CancellationToken cancellation);
    Task<StoredObject?> HeadAsync(Guid key, CancellationToken cancellation);
    Task<ObjectRead> OpenRangeAsync(Guid key, long start, long length, CancellationToken cancellation);
    Task DeleteAsync(Guid key, CancellationToken cancellation);
}

public sealed class StorageLimitException() : IOException("The configured local storage limit was reached.");
public sealed class StorageConflictException() : IOException("The storage key conflicts with an existing or incomplete write.");
public sealed class StorageIntegrityException() : IOException("The storage content does not match its declared length or checksum.");
