using System.Buffers.Binary;

namespace Churchie.Core.Audio;

/// <summary>Reads the basic facts about a .wav file made by <see cref="WavWriter"/>.</summary>
public static class WavInfo
{
    /// <summary>
    /// How long the recording is, or null if the file isn't a .wav we understand.
    /// Works even for a recording that was cut off before its header was corrected,
    /// by measuring the file instead of trusting the header.
    /// </summary>
    public static TimeSpan? ReadDuration(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return ReadDuration(stream);
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public static TimeSpan? ReadDuration(Stream stream)
    {
        Span<byte> h = stackalloc byte[WavWriter.HeaderSize];
        if (stream.ReadAtLeast(h, h.Length, throwOnEndOfStream: false) < h.Length) return null;
        if (!h[..4].SequenceEqual("RIFF"u8) || !h[8..12].SequenceEqual("WAVE"u8)) return null;

        var byteRate = BinaryPrimitives.ReadUInt32LittleEndian(h[28..]);
        if (byteRate == 0) return null;

        var dataBytes = Math.Max(0, stream.Length - WavWriter.HeaderSize);
        return TimeSpan.FromSeconds((double)dataBytes / byteRate);
    }
}
