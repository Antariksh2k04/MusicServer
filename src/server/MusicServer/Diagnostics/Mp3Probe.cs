namespace MusicServer.Diagnostics;

public static class Mp3Probe
{
    // Structural screening only, not a decoder or production tag/duration extractor.
    public static bool HasAudioFrames(string path)
    {
        using var file = File.OpenRead(path);
        Span<byte> header = stackalloc byte[10];
        if (file.Read(header) < 10) return false;
        long start = 0;
        if (header[..3].SequenceEqual("ID3"u8))
        {
            if (header[6..10].ContainsAnyInRange((byte)128, byte.MaxValue)) return false;
            start = 10L + (header[6] << 21 | header[7] << 14 | header[8] << 7 | header[9]);
            if (header[3] == 4 && (header[5] & 0x10) != 0) start += 10;
        }
        for (var i = 0; i < 3; i++)
        {
            if (start + 4 > file.Length) return false;
            file.Position = start;
            if (file.Read(header[..4]) != 4) return false;
            var length = FrameLength(header[..4]);
            if (length == 0 || start + length > file.Length) return false;
            start += length;
        }
        return true;
    }

    private static int FrameLength(ReadOnlySpan<byte> bytes)
    {
        if (bytes[0] != 255 || (bytes[1] & 0xe0) != 0xe0 || (bytes[1] & 6) != 2) return 0;
        var version = (bytes[1] >> 3) & 3;
        var bitrate = bytes[2] >> 4;
        var sample = (bytes[2] >> 2) & 3;
        if (version == 1 || bitrate is 0 or 15 || sample == 3) return 0;
        int[] mpeg1 = [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320];
        int[] mpeg2 = [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160];
        int[] rates = [44100, 48000, 32000];
        var hz = rates[sample] / (version == 3 ? 1 : version == 2 ? 2 : 4);
        return (version == 3 ? 144000 * mpeg1[bitrate] : 72000 * mpeg2[bitrate]) / hz + ((bytes[2] >> 1) & 1);
    }
}
