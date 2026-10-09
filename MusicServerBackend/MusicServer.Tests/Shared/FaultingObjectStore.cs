using MusicServer.Storage;

namespace MusicServer.Tests;

public enum StorageFault { None, BeforePut, AfterPut, Head, Range, WrongRange, FirstRead, FirstReadMissing, FirstReadDenied, MidRead, Delete }

// Test-only failures at the provider boundary; never registered by application code.
public sealed class FaultingObjectStore(IObjectStore inner) : IObjectStore, IDisposable
{
    public StorageFault Fault { get; set; }
    public int PutAttempts { get; private set; }
    public int HeadAttempts { get; private set; }
    public int RangeAttempts { get; private set; }
    public int DeleteAttempts { get; private set; }

    public async Task<StoredObject> PutAsync(Guid key, Stream source, long length, string sha256, CancellationToken cancellation)
    {
        PutAttempts++;
        Fail(StorageFault.BeforePut);
        var result = await inner.PutAsync(key, source, length, sha256, cancellation);
        Fail(StorageFault.AfterPut); // Original bytes exist, but acknowledgement was lost.
        return result;
    }
    public Task<StoredObject?> HeadAsync(Guid key, CancellationToken cancellation)
    {
        HeadAttempts++;
        Fail(StorageFault.Head);
        return inner.HeadAsync(key, cancellation);
    }
    public async Task<ObjectRead> OpenRangeAsync(Guid key, long start, long length, CancellationToken cancellation)
    {
        RangeAttempts++;
        Fail(StorageFault.Range);
        var read = await inner.OpenRangeAsync(key, start, length, cancellation);
        if (Fault == StorageFault.WrongRange) return read with { Start = start + 1 };
        return Fault is StorageFault.FirstRead or StorageFault.FirstReadMissing or StorageFault.FirstReadDenied or StorageFault.MidRead
            ? read with { Content = new FailingReadStream(read.Content, Fault) } : read;
    }
    public Task DeleteAsync(Guid key, CancellationToken cancellation)
    {
        DeleteAttempts++;
        Fail(StorageFault.Delete);
        return inner.DeleteAsync(key, cancellation);
    }
    private void Fail(StorageFault point)
    {
        if (Fault == point) throw new IOException("Injected storage failure.");
    }
    public void Dispose() => (inner as IDisposable)?.Dispose();

    private sealed class FailingReadStream(Stream source, StorageFault fault) : Stream
    {
        private bool read;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => source.Length;
        public override long Position { get => source.Position; set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (fault == StorageFault.FirstRead) throw new IOException("Injected first-read failure.");
            if (fault == StorageFault.FirstReadMissing) throw new FileNotFoundException("Injected first-read missing object.");
            if (fault == StorageFault.FirstReadDenied) throw new UnauthorizedAccessException("Injected first-read access denial.");
            if (read) throw new IOException("Injected mid-response failure.");
            read = true;
            return await source.ReadAsync(buffer[..Math.Min(buffer.Length, 8)], cancellationToken);
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) source.Dispose(); base.Dispose(disposing); }
        public override ValueTask DisposeAsync() => source.DisposeAsync();
    }
}
