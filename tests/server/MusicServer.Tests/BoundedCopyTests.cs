using MusicServer.Streaming;
using Xunit;

namespace MusicServer.Tests;

public sealed class BoundedCopyTests
{
    [Fact]
    public async Task Copy_StopsAtSelectedLength()
    {
        using var source = new MemoryStream(Enumerable.Range(0, 100).Select(x => (byte)x).ToArray());
        using var destination = new MemoryStream();
        await BoundedStreamCopy.CopyAsync(source, destination, 7, CancellationToken.None);
        Assert.Equal(7, source.Position);
        Assert.Equal(7, destination.Length);
    }

    [Fact]
    public async Task Cancellation_StopsUpstreamAfterFirstBoundedWrite()
    {
        using var cancel = new CancellationTokenSource();
        using var source = new MemoryStream(new byte[BoundedStreamCopy.BufferSize * 3]);
        using var destination = new CancelOnWrite(cancel);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            BoundedStreamCopy.CopyAsync(source, destination, source.Length, cancel.Token));
        Assert.Equal(BoundedStreamCopy.BufferSize, source.Position);
    }

    [Fact]
    public async Task ShortUpstream_ReportsIncompleteRange()
    {
        using var source = new MemoryStream(new byte[2]);
        using var destination = new MemoryStream();
        await Assert.ThrowsAsync<IOException>(() => BoundedStreamCopy.CopyAsync(source, destination, 10, CancellationToken.None));
    }

    private sealed class CancelOnWrite(CancellationTokenSource cancel) : MemoryStream
    {
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await base.WriteAsync(buffer, cancellationToken);
            cancel.Cancel();
        }
    }
}
