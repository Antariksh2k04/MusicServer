using System.Globalization;

namespace MusicServer.Streaming;

public enum RangeKind { Full, Partial, Unsatisfiable, Multiple }
public readonly record struct ByteRange(RangeKind Kind, long Start, long Length)
{
    public static ByteRange Resolve(string? header, string? ifRange, string etag, long total)
    {
        if (string.IsNullOrEmpty(header) || (!string.IsNullOrEmpty(ifRange) && ifRange != etag))
            return new(RangeKind.Full, 0, total);
        if (!header.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase)) return Invalid();
        var value = header[6..].Trim();
        if (value.Contains(',')) return new(RangeKind.Multiple, 0, 0);
        var parts = value.Split('-');
        if (parts.Length != 2 || total <= 0) return Invalid();
        if (parts[0].Length == 0)
        {
            if (!Number(parts[1], out var suffix) || suffix == 0) return Invalid();
            var length = Math.Min(suffix, total);
            return new(RangeKind.Partial, total - length, length);
        }
        if (!Number(parts[0], out var start) || start >= total) return Invalid();
        var end = total - 1;
        if (parts[1].Length != 0 && (!Number(parts[1], out end) || end < start)) return Invalid();
        end = Math.Min(end, total - 1);
        return new(RangeKind.Partial, start, end - start + 1);
    }

    private static bool Number(string value, out long number) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    private static ByteRange Invalid() => new(RangeKind.Unsatisfiable, 0, 0);
}
