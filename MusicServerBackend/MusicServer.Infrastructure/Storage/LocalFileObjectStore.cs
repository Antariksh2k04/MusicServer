using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using MusicServer.Streaming;

namespace MusicServer.Storage;

public sealed class LocalObjectStoreOptions
{
    public bool Enabled { get; init; }
    public required string Directory { get; init; }
    public long ByteLimit { get; init; } = 250 * 1024 * 1024;
    public long FileLimit { get; init; } = 50 * 1024 * 1024;
    public int ObjectLimit { get; init; } = 100;
    public int OperationLimit { get; init; } = 10_000;
}

// Development-only provider. No HTTP/static-file route exposes this directory.
public sealed class LocalFileObjectStore : IObjectStore, IDisposable
{
    private readonly LocalObjectStoreOptions options;
    private readonly string directory;
    private readonly string objects;
    private readonly string pending;
    private readonly FileStream ownership;
    private readonly SemaphoreSlim writer = new(1, 1);
    private readonly Dictionary<Guid, long> committed = new();
    private readonly Dictionary<Guid, long> incomplete = new();
    private long usedBytes;
    private long operations;
    private bool disposed;

    public LocalFileObjectStore(IHostEnvironment environment, LocalObjectStoreOptions options)
    {
        if (!environment.IsDevelopment() || !options.Enabled)
            throw new InvalidOperationException("Local object storage requires explicit Development configuration.");
        if (options.ByteLimit < 1 || options.FileLimit is < 1 or > 50 * 1024 * 1024
            || options.ObjectLimit < 1 || options.OperationLimit < 1 || !Path.IsPathFullyQualified(options.Directory))
            throw new ArgumentException("Invalid local object storage configuration.");
        directory = Path.GetFullPath(options.Directory);
        if (directory.StartsWith("\\\\", StringComparison.Ordinal) || directory.StartsWith("//", StringComparison.Ordinal)
            || new DriveInfo(Path.GetPathRoot(directory)!).DriveType == DriveType.Network)
            throw new ArgumentException("Local object storage requires a local filesystem directory.");
        this.options = options;
        for (var parent = new DirectoryInfo(directory); parent is not null; parent = parent.Parent) RejectLink(parent.FullName);
        System.IO.Directory.CreateDirectory(directory);
        RejectLink(directory);
        RejectLink(Path.Combine(directory, ".owner.lock"));
        ownership = new FileStream(Path.Combine(directory, ".owner.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        objects = Path.Combine(directory, "objects");
        pending = Path.Combine(directory, "pending");
        try
        {
            RejectLink(objects);
            RejectLink(pending);
            System.IO.Directory.CreateDirectory(objects);
            System.IO.Directory.CreateDirectory(pending);
            Scan(objects, committed);
            Scan(pending, incomplete);
        }
        catch { ownership.Dispose(); throw; }
    }

    public async Task<StoredObject> PutAsync(Guid key, Stream source, long length, string sha256, CancellationToken cancellation)
    {
        ValidateKey(key);
        ArgumentNullException.ThrowIfNull(source);
        if (length <= 0 || length > options.FileLimit || !ValidHash(sha256)) throw new StorageIntegrityException();
        sha256 = sha256.ToUpperInvariant();
        Charge(cancellation);
        await writer.WaitAsync(cancellation);
        var staged = Path.Combine(pending, key.ToString("N"));
        var destination = Path.Combine(objects, key.ToString("N"));
        var reserved = false;
        var published = false;
        try
        {
            if (committed.ContainsKey(key))
            {
                var existing = await HeadCoreAsync(key, cancellation) ?? throw new StorageIntegrityException();
                if (existing.SizeBytes != length || existing.Sha256 != sha256) throw new StorageConflictException();
                return existing;
            }
            if (incomplete.ContainsKey(key) || System.IO.Directory.Exists(staged)) throw new StorageConflictException();
            if (length > options.ByteLimit - usedBytes || committed.Count + incomplete.Count >= options.ObjectLimit)
                throw new StorageLimitException();
            usedBytes += length;
            incomplete.Add(key, length);
            reserved = true;
            System.IO.Directory.CreateDirectory(staged);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long received = 0;
            await using (var output = new FileStream(Path.Combine(staged, "audio.mp3"), FileMode.CreateNew,
                FileAccess.Write, FileShare.None, BoundedStreamCopy.BufferSize, FileOptions.Asynchronous))
            {
                var buffer = new byte[BoundedStreamCopy.BufferSize];
                int read;
                while ((read = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, length - received + 1)), cancellation)) > 0)
                {
                    received += read;
                    if (received > length) throw new StorageIntegrityException();
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellation);
                }
                if (received != length || Convert.ToHexString(hash.GetHashAndReset()) != sha256) throw new StorageIntegrityException();
                await output.FlushAsync(cancellation);
                output.Flush(flushToDisk: true);
            }
            var metadata = new StoredObject(key, length, sha256);
            await using (var output = new FileStream(Path.Combine(staged, "metadata.json"), FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(output, metadata, cancellationToken: cancellation);
                await output.FlushAsync(cancellation);
                output.Flush(flushToDisk: true);
            }
            cancellation.ThrowIfCancellationRequested();
            await PublishAsync(staged, destination, cancellation);
            committed.Add(key, length);
            incomplete.Remove(key);
            published = true;
            return metadata;
        }
        finally
        {
            if (reserved && !published && TryRemove(staged))
            {
                incomplete.Remove(key);
                usedBytes -= length;
            }
            writer.Release();
        }
    }

    public Task<StoredObject?> HeadAsync(Guid key, CancellationToken cancellation)
    {
        ValidateKey(key);
        Charge(cancellation);
        return HeadCoreAsync(key, cancellation);
    }

    public async Task<ObjectRead> OpenRangeAsync(Guid key, long start, long length, CancellationToken cancellation)
    {
        ValidateKey(key);
        Charge(cancellation);
        var metadata = await HeadCoreAsync(key, cancellation) ?? throw new FileNotFoundException("The storage object is unavailable.");
        if (start < 0 || length < 0 || start > metadata.SizeBytes || length > metadata.SizeBytes - start)
            throw new ArgumentOutOfRangeException(nameof(length), "Invalid storage range.");
        var source = new FileStream(Path.Combine(objects, key.ToString("N"), "audio.mp3"), FileMode.Open,
            FileAccess.Read, FileShare.Read, BoundedStreamCopy.BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        try
        {
            if (source.Length != metadata.SizeBytes) throw new StorageIntegrityException();
            cancellation.ThrowIfCancellationRequested();
            source.Position = start;
            return new(metadata, start, length, new ReadWindowStream(source, length));
        }
        catch { await source.DisposeAsync(); throw; }
    }

    public async Task DeleteAsync(Guid key, CancellationToken cancellation)
    {
        ValidateKey(key);
        Charge(cancellation);
        await writer.WaitAsync(cancellation);
        try
        {
            RemoveAccounted(objects, committed, key);
            RemoveAccounted(pending, incomplete, key);
        }
        finally { writer.Release(); }
    }

    private async Task<StoredObject?> HeadCoreAsync(Guid key, CancellationToken cancellation)
    {
        var objectDirectory = Path.Combine(objects, key.ToString("N"));
        if (!System.IO.Directory.Exists(objectDirectory)) return null;
        RejectLink(objectDirectory);
        var path = Path.Combine(objectDirectory, "metadata.json");
        RejectLink(path);
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            4096, FileOptions.Asynchronous);
        if (input.Length > 4096) throw new StorageIntegrityException();
        StoredObject? metadata;
        try { metadata = await JsonSerializer.DeserializeAsync<StoredObject>(input, cancellationToken: cancellation); }
        catch (JsonException) { throw new StorageIntegrityException(); }
        var audio = Path.Combine(objectDirectory, "audio.mp3");
        RejectLink(audio);
        if (metadata is null || metadata.Key != key || metadata.SizeBytes <= 0 || !ValidHash(metadata.Sha256)
            || new FileInfo(audio).Length != metadata.SizeBytes) throw new StorageIntegrityException();
        return metadata;
    }

    private void Scan(string parent, Dictionary<Guid, long> entries)
    {
        RejectLink(parent);
        foreach (var child in System.IO.Directory.EnumerateDirectories(parent))
        {
            RejectLink(child);
            if (!Guid.TryParseExact(Path.GetFileName(child), "N", out var key) || key == Guid.Empty)
                throw new IOException("The local storage namespace contains an unrecognized directory.");
            var payload = Path.Combine(child, "audio.mp3");
            RejectLink(payload);
            var length = File.Exists(payload) ? new FileInfo(payload).Length : 0;
            entries.Add(key, length);
            usedBytes = checked(usedBytes + length);
        }
    }

    private void RemoveAccounted(string parent, Dictionary<Guid, long> entries, Guid key)
    {
        if (!entries.TryGetValue(key, out var bytes)) return;
        RemoveKnownDirectory(Path.Combine(parent, key.ToString("N")));
        entries.Remove(key);
        usedBytes -= bytes;
    }

    private static void RemoveKnownDirectory(string path)
    {
        if (!System.IO.Directory.Exists(path)) return;
        RejectLink(path);
        foreach (var name in new[] { "audio.mp3", "metadata.json" })
        {
            var file = Path.Combine(path, name);
            RejectLink(file);
            File.Delete(file);
        }
        System.IO.Directory.Delete(path, recursive: false);
    }

    private static bool TryRemove(string path)
    {
        try { RemoveKnownDirectory(path); return true; }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    private static async Task PublishAsync(string staged, string destination, CancellationToken cancellation)
    {
        // Windows can briefly retain a directory handle after its files close.
        // Retry only the same atomic rename, never the transfer or an existing destination.
        for (var attempt = 0; ; attempt++)
        {
            cancellation.ThrowIfCancellationRequested();
            try { System.IO.Directory.Move(staged, destination); return; }
            catch (IOException failure) when (OperatingSystem.IsWindows() && attempt < 3
                && (failure.HResult & 0xffff) is 5 or 32 or 33
                && System.IO.Directory.Exists(staged) && !System.IO.Directory.Exists(destination))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)), cancellation);
            }
        }
    }

    private void Charge(CancellationToken cancellation)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellation.ThrowIfCancellationRequested();
        if (Interlocked.Increment(ref operations) > options.OperationLimit) throw new StorageLimitException();
    }

    private static void ValidateKey(Guid key)
    {
        if (key == Guid.Empty) throw new ArgumentException("An opaque generated storage key is required.", nameof(key));
    }
    private static bool ValidHash(string? hash) => hash is { Length: 64 } && hash.All(Uri.IsHexDigit);
    private static void RejectLink(string path)
    {
        if ((File.Exists(path) || System.IO.Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Links are not allowed in the private local storage namespace.");
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        ownership.Dispose();
        writer.Dispose();
        // Keep payloads/metadata for exact-key reconciliation across adapter restarts.
    }
}
