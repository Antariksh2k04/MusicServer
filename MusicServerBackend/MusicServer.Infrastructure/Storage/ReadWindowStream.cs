namespace MusicServer.Storage;

// Owns the upstream stream and exposes only the selected window, with no seek/write escape.
internal sealed class ReadWindowStream(Stream source, long length) : Stream
{
    private long remaining = length;
    private readonly long windowLength = length;
    public override bool CanRead => source.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => windowLength;
    public override long Position { get => windowLength - remaining; set => throw new NotSupportedException(); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (offset > buffer.Length - count) throw new ArgumentException("Invalid read bounds.");
        if (remaining == 0 || count == 0) return 0;
        return Consume(source.Read(buffer, offset, (int)Math.Min(count, remaining)));
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (remaining == 0 || buffer.Length == 0) return 0;
        return Consume(await source.ReadAsync(buffer[..(int)Math.Min(buffer.Length, remaining)], cancellationToken));
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private int Consume(int count)
    {
        if (count == 0) throw new IOException("The stored object ended before the selected range completed.");
        remaining -= count;
        return count;
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        if (disposing) source.Dispose();
        base.Dispose(disposing);
    }
    public override async ValueTask DisposeAsync()
    {
        await source.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
