using System.Security.Cryptography;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MusicServer.Storage;
using MusicServer.Streaming;
using Xunit;

namespace MusicServer.Tests;

public sealed class ObjectStoreTests
{
    [Fact]
    public async Task Put_StoresOriginalBytesAndMetadataSurviveAdapterRestart()
    {
        using var sandbox = new Sandbox();
        var bytes = Enumerable.Range(0, 200_000).Select(x => (byte)x).ToArray();
        var key = Guid.NewGuid();
        using (var store = sandbox.Open())
        {
            var saved = await Put(store, key, bytes);
            Assert.Equal(bytes.Length, saved.SizeBytes);
            Assert.Equal(Hash(bytes), saved.Sha256);
        }
        using var reopened = sandbox.Open();
        var metadata = await reopened.HeadAsync(key, default);
        Assert.Equal(Hash(bytes), metadata!.Sha256);
        await using var range = await reopened.OpenRangeAsync(key, 123, 170_000, default);
        using var output = new MemoryStream();
        await range.Content.CopyToAsync(output);
        Assert.Equal(bytes[123..170_123], output.ToArray());
        Assert.False(range.Content.CanSeek);
        Assert.Throws<NotSupportedException>(() => range.Content.Seek(0, SeekOrigin.Begin));
        Assert.Equal(0, await range.Content.ReadAsync(new byte[16]));
    }

    [Fact]
    public async Task MatchingRetryAtCapacity_IsIdempotentAndConflictingBytesNeverOverwrite()
    {
        using var sandbox = new Sandbox();
        using var store = sandbox.Open(bytes: 4, count: 1);
        var key = Guid.NewGuid();
        var saved = await Put(store, key, [1, 2, 3, 4]);
        Assert.Equal(saved, await Put(store, key, [1, 2, 3, 4]));
        await Assert.ThrowsAsync<StorageConflictException>(() => Put(store, key, [4, 3, 2, 1]));
        await Assert.ThrowsAsync<StorageLimitException>(() => Put(store, Guid.NewGuid(), [1]));
        Assert.Equal(saved, await store.HeadAsync(key, default));
    }

    [Theory]
    [InlineData(3, false)]
    [InlineData(5, false)]
    [InlineData(4, true)]
    public async Task IncorrectLengthOrHash_DoesNotPublishOrLeakReservation(long length, bool wrongHash)
    {
        using var sandbox = new Sandbox();
        using var store = sandbox.Open(bytes: 5, count: 1);
        var key = Guid.NewGuid();
        byte[] bytes = [1, 2, 3, 4];
        await Assert.ThrowsAsync<StorageIntegrityException>(() => store.PutAsync(key, new MemoryStream(bytes), length,
            wrongHash ? Hash([4, 3, 2, 1]) : Hash(bytes), default));
        Assert.Null(await store.HeadAsync(key, default));
        Assert.Empty(Directory.GetDirectories(Path.Combine(sandbox.Root, "pending")));
        await Put(store, Guid.NewGuid(), bytes);
    }

    [Fact]
    public async Task Put_UsesBoundedReadsAndCancellationRemovesPartialObject()
    {
        using var sandbox = new Sandbox();
        using var store = sandbox.Open();
        var bytes = new byte[200_000];
        using var cancellation = new CancellationTokenSource();
        using var source = new InspectingInput(bytes, cancellation);
        var key = Guid.NewGuid();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.PutAsync(key, source, bytes.Length, Hash(bytes), cancellation.Token));
        Assert.InRange(source.LargestRequest, 1, BoundedStreamCopy.BufferSize);
        Assert.Null(await store.HeadAsync(key, default));
        Assert.Empty(Directory.GetDirectories(Path.Combine(sandbox.Root, "pending")));
        await Put(store, key, bytes);
    }

    [Fact]
    public async Task SourceFailure_DoesNotCommitPartialContent()
    {
        using var sandbox = new Sandbox();
        using var store = sandbox.Open();
        byte[] bytes = new byte[200_000];
        using var source = new InspectingInput(bytes, fail: true);
        var key = Guid.NewGuid();
        await Assert.ThrowsAsync<IOException>(() => store.PutAsync(key, source, bytes.Length, Hash(bytes), default));
        Assert.Null(await store.HeadAsync(key, default));
        await Put(store, key, bytes);
    }

    [Fact]
    public async Task ConcurrentPuts_CannotOvercommitByteCapacity()
    {
        using var sandbox = new Sandbox();
        using var store = sandbox.Open(bytes: 4);
        async Task<bool> Attempt(Guid key)
        {
            try { await Put(store, key, [1, 2, 3]); return true; }
            catch (StorageLimitException) { return false; }
        }
        var keys = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var results = await Task.WhenAll(keys.Select(Attempt));
        Assert.Single(results, x => x);
        var existing = new List<Guid>();
        foreach (var key in keys) if (await store.HeadAsync(key, default) is not null) existing.Add(key);
        Assert.Single(existing);
    }

    [Fact]
    public async Task RestartCountsStoredAndIncompleteBytesAndDeleteTargetsOnlyTheGivenKey()
    {
        using var sandbox = new Sandbox();
        var key = Guid.NewGuid();
        using (var store = sandbox.Open()) await Put(store, key, [1, 2, 3]);
        var orphan = Guid.NewGuid();
        var orphanDirectory = Path.Combine(sandbox.Root, "pending", orphan.ToString("N"));
        Directory.CreateDirectory(orphanDirectory);
        File.WriteAllBytes(Path.Combine(orphanDirectory, "audio.mp3"), [8, 9]);
        using var reopened = sandbox.Open(bytes: 5);
        await Assert.ThrowsAsync<StorageLimitException>(() => Put(reopened, Guid.NewGuid(), [1]));
        Assert.Null(await reopened.HeadAsync(orphan, default));
        await reopened.DeleteAsync(orphan, default);
        Assert.NotNull(await reopened.HeadAsync(key, default));
        Assert.False(Directory.Exists(orphanDirectory));
        await Put(reopened, Guid.NewGuid(), [6, 7]);
        await reopened.DeleteAsync(orphan, default); // Missing target is idempotent.
    }

    [Fact]
    public async Task IncompleteKey_IsNotOverwrittenUntilExactKeyCleanup()
    {
        using var sandbox = new Sandbox();
        Directory.CreateDirectory(Path.Combine(sandbox.Root, "pending"));
        var key = Guid.NewGuid();
        var partial = Path.Combine(sandbox.Root, "pending", key.ToString("N"));
        Directory.CreateDirectory(partial);
        File.WriteAllBytes(Path.Combine(partial, "audio.mp3"), [1]);
        using var store = sandbox.Open();
        await Assert.ThrowsAsync<StorageConflictException>(() => Put(store, key, [1, 2]));
        await store.DeleteAsync(key, default);
        await Put(store, key, [1, 2]);
    }

    [Fact]
    public async Task DeleteFailure_PreservesCapacityUntilTargetCleanupSucceeds()
    {
        using var sandbox = new Sandbox();
        using var store = sandbox.Open(bytes: 2, count: 1);
        var key = Guid.NewGuid();
        await Put(store, key, [1, 2]);
        // An unexpected file prevents removal; never recursively delete unrecorded files.
        var unexpected = Path.Combine(sandbox.Root, "objects", key.ToString("N"), "unrecorded.txt");
        File.WriteAllText(unexpected, "owned test sentinel");
        await Assert.ThrowsAsync<IOException>(() => store.DeleteAsync(key, default));
        Assert.True(File.Exists(unexpected));
        await Assert.ThrowsAsync<StorageLimitException>(() => Put(store, Guid.NewGuid(), [1]));
        File.Delete(unexpected);
        await store.DeleteAsync(key, default);
        await Put(store, Guid.NewGuid(), [3, 4]);
    }

    [Fact]
    public async Task OperationAndFileLimits_AreEnforced()
    {
        using var sandbox = new Sandbox();
        using var store = sandbox.Open(operations: 1, file: 2);
        await Assert.ThrowsAsync<StorageIntegrityException>(() => Put(store, Guid.NewGuid(), [1, 2, 3]));
        await Put(store, Guid.NewGuid(), [1, 2]);
        await Assert.ThrowsAsync<StorageLimitException>(() => store.HeadAsync(Guid.NewGuid(), default));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, -1)]
    [InlineData(4, 1)]
    [InlineData(2, 2)]
    public async Task OutOfBoundsRange_IsRejected(long start, long length)
    {
        using var sandbox = new Sandbox();
        using var store = sandbox.Open();
        var key = Guid.NewGuid();
        await Put(store, key, [1, 2, 3]);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.OpenRangeAsync(key, start, length, default));
    }

    [Fact]
    public async Task RangeCancellationAndDisposal_ReleaseTheUnderlyingRead()
    {
        using var sandbox = new Sandbox();
        using var store = sandbox.Open();
        var key = Guid.NewGuid();
        await Put(store, key, [1, 2, 3]);
        await using (var read = await store.OpenRangeAsync(key, 1, 2, default))
        {
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read.Content.ReadAsync(new byte[2], cancelled.Token).AsTask());
        }
        await store.DeleteAsync(key, default);
        Assert.Null(await store.HeadAsync(key, default));
    }

    [Fact]
    public async Task LostPutAcknowledgement_ReconcilesTheExactKeyAndRetriesWithoutAnotherObject()
    {
        using var sandbox = new Sandbox();
        using var store = sandbox.Open(bytes: 2, count: 1);
        using var faulting = new FaultingObjectStore(store) { Fault = StorageFault.AfterPut };
        var key = Guid.NewGuid();
        await Assert.ThrowsAsync<IOException>(() => Put(faulting, key, [1, 2]));
        var saved = await store.HeadAsync(key, default);
        Assert.NotNull(saved);
        faulting.Fault = StorageFault.None;
        Assert.Equal(saved, await Put(faulting, key, [1, 2]));
        Assert.Single(Directory.GetDirectories(Path.Combine(sandbox.Root, "objects")));
    }

    [Fact]
    public async Task InjectedUnavailableReadAndDelete_DoNotDestroyStoredBytes()
    {
        using var sandbox = new Sandbox();
        using var store = sandbox.Open();
        using var faulting = new FaultingObjectStore(store);
        var key = Guid.NewGuid();
        await Put(store, key, [1, 2, 3, 4, 5, 6, 7, 8, 9]);
        faulting.Fault = StorageFault.Delete;
        await Assert.ThrowsAsync<IOException>(() => faulting.DeleteAsync(key, default));
        faulting.Fault = StorageFault.Range;
        await Assert.ThrowsAsync<IOException>(() => faulting.OpenRangeAsync(key, 0, 9, default));
        faulting.Fault = StorageFault.MidRead;
        await using (var read = await faulting.OpenRangeAsync(key, 0, 9, default))
        {
            using var output = new MemoryStream();
            await Assert.ThrowsAsync<IOException>(() => read.Content.CopyToAsync(output));
            Assert.Equal(8, output.Length);
        }
        faulting.Fault = StorageFault.None;
        Assert.NotNull(await faulting.HeadAsync(key, default));
    }

    [Fact]
    public async Task InvalidKeysAndDisposedInstances_CannotOperateOnStorage()
    {
        using var sandbox = new Sandbox();
        using var store = sandbox.Open();
        await Assert.ThrowsAsync<ArgumentException>(() => store.HeadAsync(Guid.Empty, default));
        store.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => store.HeadAsync(Guid.NewGuid(), default));
    }

    [Theory]
    [InlineData("json")]
    [InlineData("key")]
    [InlineData("length")]
    public async Task CorruptStoredMetadataOrPayload_IsRejectedByExactKeyLookup(string corruption)
    {
        using var sandbox = new Sandbox();
        using var store = sandbox.Open();
        var key = Guid.NewGuid();
        var saved = await Put(store, key, [1, 2, 3]);
        var directory = Path.Combine(sandbox.Root, "objects", key.ToString("N"));
        if (corruption == "length") File.WriteAllBytes(Path.Combine(directory, "audio.mp3"), [1]);
        else File.WriteAllText(Path.Combine(directory, "metadata.json"), corruption == "json" ? "invalid"
            : System.Text.Json.JsonSerializer.Serialize(saved with { Key = Guid.NewGuid() }));
        await Assert.ThrowsAsync<StorageIntegrityException>(() => store.HeadAsync(key, default));
        await Assert.ThrowsAsync<StorageIntegrityException>(() => store.OpenRangeAsync(key, 0, 1, default));
        Assert.Null(await store.HeadAsync(Guid.NewGuid(), default));
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Development", false)]
    public void NonDevelopmentOrUnconfiguredAdapter_IsRejectedBeforeCreatingFiles(string environment, bool enabled)
    {
        using var sandbox = new Sandbox();
        Assert.Throws<InvalidOperationException>(() => new LocalFileObjectStore(new EnvironmentStub(environment),
            new LocalObjectStoreOptions { Enabled = enabled, Directory = sandbox.Root }));
        Assert.False(Directory.Exists(sandbox.Root));
    }

    [Fact]
    public void ASecondAdapter_CannotWriteIntoALiveNamespace()
    {
        using var sandbox = new Sandbox();
        using var first = sandbox.Open();
        Assert.Throws<IOException>(() => sandbox.Open());
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static Task<StoredObject> Put(IObjectStore store, Guid key, byte[] bytes)
        => store.PutAsync(key, new MemoryStream(bytes), bytes.Length, Hash(bytes), default);

    private sealed class InspectingInput(byte[] bytes, CancellationTokenSource? cancellation = null, bool fail = false) : MemoryStream(bytes)
    {
        private int calls;
        public int LargestRequest { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            LargestRequest = Math.Max(LargestRequest, buffer.Length);
            if (++calls == 2)
            {
                if (fail) throw new IOException("Injected input disconnect.");
                cancellation?.Cancel();
            }
            return await base.ReadAsync(buffer[..Math.Min(buffer.Length, 8192)], cancellationToken);
        }
    }
    private sealed class EnvironmentStub(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "storage-test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class Sandbox : IDisposable
    {
        private readonly string parent;
        public string Root { get; }
        public Sandbox()
        {
            var repository = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(repository.FullName, "global.json")))
                repository = repository.Parent ?? throw new InvalidOperationException("Repository root not found.");
            parent = Path.GetFullPath(Path.Combine(repository.FullName, ".local", "storage-tests"));
            Root = Path.Combine(parent, Guid.NewGuid().ToString("N"));
        }
        public LocalFileObjectStore Open(long bytes = 250 * 1024 * 1024, int count = 100, int operations = 1000, long file = 50 * 1024 * 1024)
            => new(new EnvironmentStub("Development"), new LocalObjectStoreOptions
            { Enabled = true, Directory = Root, ByteLimit = bytes, ObjectLimit = count, OperationLimit = operations, FileLimit = file });
        public void Dispose()
        {
            var target = Path.GetFullPath(Root);
            if (!target.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !Guid.TryParseExact(Path.GetFileName(target), "N", out _)) throw new InvalidOperationException("Unsafe test cleanup path.");
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
        }
    }
}
