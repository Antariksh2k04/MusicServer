namespace MusicServer.Streaming;

public static class BoundedStreamCopy
{
    public const int BufferSize = 64 * 1024;

    // Source is positioned at the selected range; never copy bytes beyond it.
    public static async Task CopyAsync(Stream source, Stream destination, long length, CancellationToken cancellation)
    {
        var buffer = new byte[BufferSize];
        var remaining = length;
        while (remaining > 0)
        {
            cancellation.ThrowIfCancellationRequested();
            var read = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellation);
            if (read == 0) throw new IOException("Upstream response ended before the selected range completed.");
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellation);
            remaining -= read;
        }
    }
}
