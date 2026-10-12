using System.Collections.Concurrent;
using System.Security.Cryptography;
using MusicServer.Storage;

namespace MusicServer.Diagnostics;

public sealed record FixtureTrack(Guid Id, string Title, string Artist, string Album, long? DurationMs, long SizeBytes, string Etag);
public sealed class FixtureFailure(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

// Isolated experiment state. This is never registered as the production library.
public sealed class FixtureCatalog : IDisposable
{
    public const long FileLimit = 50 * 1024 * 1024;
    private readonly ConcurrentDictionary<Guid, FixtureTrack> tracks = new();
    private readonly Dictionary<string, Guid> hashes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, Guid> objectKeys = new();
    private readonly ConcurrentDictionary<Guid, byte> ownedObjects = new();
    private readonly SemaphoreSlim writer = new(1, 1);
    private readonly string directory;
    private readonly long byteLimit;
    private readonly IObjectStore storage;
    private long used;

    public FixtureCatalog(DiagnosticOptions options, IObjectStore storage)
    {
        this.storage = storage;
        directory = Path.Combine(Path.GetFullPath(options.StorageDirectory), "session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        byteLimit = options.StorageByteLimit;
    }

    public FixtureTrack[] List() => tracks.Values.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id).ToArray();
    public FixtureTrack? Find(Guid id) => tracks.GetValueOrDefault(id);
    public Task<StoredObject?> HeadAsync(Guid id, CancellationToken cancellation)
        => storage.HeadAsync(objectKeys[id], cancellation);
    public Task<ObjectRead> OpenRangeAsync(Guid id, long start, long length, CancellationToken cancellation)
        => storage.OpenRangeAsync(objectKeys[id], start, length, cancellation);
    private string FilePath(Guid id) => Path.Combine(directory, id.ToString("N") + ".mp3");

    public async Task<(FixtureTrack Track, bool Duplicate)> UploadAsync(Stream input, string filename, long declared, CancellationToken cancellation)
    {
        if (declared <= 0) throw new FixtureFailure(400, "invalidMp3");
        if (declared > FileLimit) throw new FixtureFailure(413, "fileTooLarge");
        filename = filename.Replace('\\', '/').Split('/')[^1];
        if (string.IsNullOrWhiteSpace(filename) || filename.Length > 255) throw new FixtureFailure(400, "invalidFileName");
        await writer.WaitAsync(cancellation);
        var id = Guid.NewGuid();
        var file = FilePath(id);
        var committed = false;
        Guid? objectKey = null;
        try
        {
            if (declared > byteLimit - used) throw new FixtureFailure(409, "quotaExceeded");
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long received = 0;
            await using (var output = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, true))
            {
                var buffer = new byte[64 * 1024];
                int count;
                while ((count = await input.ReadAsync(buffer, cancellation)) != 0)
                {
                    received += count;
                    if (received > FileLimit) throw new FixtureFailure(413, "fileTooLarge");
                    if (received > declared) throw new FixtureFailure(400, "transferIncomplete");
                    hash.AppendData(buffer, 0, count);
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellation);
                }
                if (received != declared) throw new FixtureFailure(400, "transferIncomplete");
                await output.FlushAsync(cancellation);
            }
            var digest = Convert.ToHexString(hash.GetHashAndReset());
            if (hashes.TryGetValue(digest, out var existing)) return (tracks[existing], true);
            if (tracks.Count >= 100) throw new FixtureFailure(409, "quotaExceeded");
            if (!Mp3Probe.HasAudioFrames(file)) throw new FixtureFailure(400, "invalidMp3");
            objectKey = Guid.NewGuid();
            ownedObjects[objectKey.Value] = 0;
            StoredObject stored;
            await using (var inputFile = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, true))
                stored = await storage.PutAsync(objectKey.Value, inputFile, received, digest, cancellation);
            cancellation.ThrowIfCancellationRequested();
            var title = Path.GetFileNameWithoutExtension(filename);
            var track = new FixtureTrack(id, string.IsNullOrWhiteSpace(title) ? "Untitled track" : title,
                "Unknown artist", "Unknown album", null, received, stored.Etag);
            objectKeys[id] = objectKey.Value;
            tracks[id] = track;
            hashes[digest] = id;
            used += received;
            committed = true;
            return (track, false);
        }
        catch (StorageLimitException) { throw new FixtureFailure(409, "quotaExceeded"); }
        finally
        {
            try
            {
                if (!committed && objectKey is { } attempted) await TryDeleteObjectAsync(attempted);
                if (File.Exists(file)) File.Delete(file);
            }
            finally { writer.Release(); }
        }
    }

    private async Task TryDeleteObjectAsync(Guid key)
    {
        try
        {
            await storage.DeleteAsync(key, CancellationToken.None);
            ownedObjects.TryRemove(key, out _);
        }
        catch (IOException) { /* Retain exact-key ownership for a shutdown cleanup attempt. */ }
        catch (UnauthorizedAccessException) { /* Never pretend uncertain deletion succeeded. */ }
    }

    public void Dispose()
    {
        foreach (var key in ownedObjects.Keys) TryDeleteObjectAsync(key).GetAwaiter().GetResult();
        try { Directory.Delete(directory, false); } catch (IOException) { /* Never recursively delete unrecorded files. */ }
        writer.Dispose();
    }
}
