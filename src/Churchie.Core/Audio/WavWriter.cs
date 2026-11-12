using System.Buffers.Binary;
using System.Text;

namespace Churchie.Core.Audio;

/// <summary>
/// Writes sound to a .wav file (16-bit PCM, the format every player and editor opens).
///
/// A .wav file starts with a 44-byte header that says how long the sound is. We can't
/// know that until the recording ends, so the header is written first with a length of
/// zero and corrected later by <see cref="UpdateHeader"/>. Calling UpdateHeader every
/// few seconds means that even if the computer loses power in the middle of a sermon,
/// the file on disk is still playable up to that point.
/// </summary>
public sealed class WavWriter : IDisposable
{
    public const int HeaderSize = 44;
    private const int BytesPerSample = 2; // 16-bit

    private readonly Stream _stream;
    private byte[] _buffer = new byte[8192];
    private long _dataBytes;
    private bool _disposed;

    public int SampleRate { get; }
    public int Channels { get; }

    /// <summary>How much sound has been written so far.</summary>
    public TimeSpan Duration => TimeSpan.FromSeconds((double)_dataBytes / (SampleRate * Channels * BytesPerSample));

    /// <summary>Size of the file so far, in bytes.</summary>
    public long FileSize => HeaderSize + _dataBytes;

    public WavWriter(string path, int sampleRate, int channels = 1)
        : this(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize: 65536), sampleRate, channels)
    {
    }

    /// <param name="stream">Must allow seeking, so the header can be corrected later.</param>
    public WavWriter(Stream stream, int sampleRate, int channels = 1)
    {
        if (!stream.CanSeek) throw new ArgumentException("The stream must support seeking.", nameof(stream));
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));

        _stream = stream;
        SampleRate = sampleRate;
        Channels = channels;
        WriteHeader();
    }

    /// <summary>
    /// Adds sound to the file. Microphones give samples as numbers from -1.0 to +1.0;
    /// a 16-bit .wav stores them as whole numbers from -32768 to +32767.
    /// </summary>
    public void Write(ReadOnlySpan<float> samples)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var byteCount = samples.Length * BytesPerSample;
        if (_buffer.Length < byteCount) _buffer = new byte[byteCount];

        for (var i = 0; i < samples.Length; i++)
        {
            // Anything louder than full scale is clipped rather than wrapping round to a loud crackle.
            var clamped = Math.Clamp(samples[i], -1f, 1f);
            var value = (short)Math.Round(clamped * short.MaxValue);
            BinaryPrimitives.WriteInt16LittleEndian(_buffer.AsSpan(i * BytesPerSample), value);
        }

        _stream.Write(_buffer, 0, byteCount);
        _dataBytes += byteCount;
    }

    /// <summary>Corrects the lengths in the header and saves everything written so far to disk.</summary>
    public void UpdateHeader()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var end = _stream.Position;
        _stream.Position = 0;
        WriteHeader();
        _stream.Position = end;
        _stream.Flush();
    }

    private void WriteHeader()
    {
        Span<byte> h = stackalloc byte[HeaderSize];
        var byteRate = SampleRate * Channels * BytesPerSample;
        // .wav lengths are 32-bit numbers, so a file can't describe more than 4 GB (about 12 hours).
        var dataSize = (uint)Math.Min(_dataBytes, uint.MaxValue - HeaderSize);

        Encoding.ASCII.GetBytes("RIFF", h);
        BinaryPrimitives.WriteUInt32LittleEndian(h[4..], dataSize + HeaderSize - 8);
        Encoding.ASCII.GetBytes("WAVE", h[8..]);
        Encoding.ASCII.GetBytes("fmt ", h[12..]);
        BinaryPrimitives.WriteUInt32LittleEndian(h[16..], 16);                    // size of the format block
        BinaryPrimitives.WriteUInt16LittleEndian(h[20..], 1);                     // 1 = plain PCM
        BinaryPrimitives.WriteUInt16LittleEndian(h[22..], (ushort)Channels);
        BinaryPrimitives.WriteUInt32LittleEndian(h[24..], (uint)SampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(h[28..], (uint)byteRate);
        BinaryPrimitives.WriteUInt16LittleEndian(h[32..], (ushort)(Channels * BytesPerSample));
        BinaryPrimitives.WriteUInt16LittleEndian(h[34..], 8 * BytesPerSample);    // bits per sample
        Encoding.ASCII.GetBytes("data", h[36..]);
        BinaryPrimitives.WriteUInt32LittleEndian(h[40..], dataSize);

        _stream.Write(h);
    }

    public void Dispose()
    {
        if (_disposed) return;
        try { UpdateHeader(); }
        finally
        {
            _disposed = true;
            _stream.Dispose();
        }
    }
}
