using System.Buffers.Binary;
using System.Text;
using Churchie.Core.Audio;

namespace Churchie.Core.Tests;

public class AudioTests
{
    [Fact]
    public void Wav_file_has_a_correct_header_and_samples()
    {
        var stream = new MemoryStream();
        using (var wav = new WavWriter(new NonClosingStream(stream), sampleRate: 44100, channels: 1))
        {
            wav.Write(new[] { 0f, 1f, -1f, 0.5f });
            Assert.Equal(WavWriter.HeaderSize + 8, wav.FileSize);
        }

        var bytes = stream.ToArray();
        Assert.Equal(44 + 8, bytes.Length);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal(36u + 8u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)));   // file size minus 8
        Assert.Equal("WAVEfmt ", Encoding.ASCII.GetString(bytes, 8, 8));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(20)));          // PCM
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(22)));          // mono
        Assert.Equal(44100u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(24)));
        Assert.Equal(88200u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(28)));     // bytes per second
        Assert.Equal(16, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(34)));         // bits
        Assert.Equal("data", Encoding.ASCII.GetString(bytes, 36, 4));
        Assert.Equal(8u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(40)));

        Assert.Equal(0, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(44)));
        Assert.Equal(short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(46)));
        Assert.Equal(-short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(48)));
        Assert.Equal(16384, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(50)));
    }

    [Fact]
    public void Sound_louder_than_full_scale_is_clipped_not_wrapped()
    {
        var stream = new MemoryStream();
        using (var wav = new WavWriter(new NonClosingStream(stream), 8000))
            wav.Write(new[] { 3f, -3f });

        var bytes = stream.ToArray();
        Assert.Equal(short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(44)));
        Assert.Equal(-short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(46)));
    }

    [Fact]
    public void Duration_counts_the_sound_written()
    {
        using var wav = new WavWriter(new MemoryStream(), sampleRate: 8000);
        wav.Write(new float[8000]);
        wav.Write(new float[4000]);

        Assert.Equal(TimeSpan.FromSeconds(1.5), wav.Duration);
    }

    [Fact]
    public void Updating_the_header_keeps_later_sound_in_the_right_place()
    {
        var stream = new MemoryStream();
        using (var wav = new WavWriter(new NonClosingStream(stream), 8000))
        {
            wav.Write(new[] { 0.5f });
            wav.UpdateHeader();          // as happens every few seconds while recording
            wav.Write(new[] { -0.5f });
        }

        var bytes = stream.ToArray();
        Assert.Equal(48, bytes.Length);
        Assert.Equal(16384, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(44)));
        Assert.Equal(-16384, BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(46)));
        Assert.Equal(4u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(40)));
    }

    [Fact]
    public void A_recording_cut_off_by_a_crash_still_reports_its_length()
    {
        // Header says "0 bytes of sound" (never corrected), but two seconds of sound follow it.
        var stream = new MemoryStream();
        var wav = new WavWriter(stream, 8000);
        wav.Write(new float[16000]);
        stream.Flush(); // no UpdateHeader, no Dispose: like a power cut

        stream.Position = 0;
        Assert.Equal(TimeSpan.FromSeconds(2), WavInfo.ReadDuration(stream));
    }

    [Fact]
    public void Files_that_are_not_wav_have_no_duration()
    {
        Assert.Null(WavInfo.ReadDuration(new MemoryStream(Encoding.ASCII.GetBytes("this is not a sound file, just some text in a file"))));
        Assert.Null(WavInfo.ReadDuration(new MemoryStream()));
    }

    [Fact]
    public void Peak_is_the_loudest_sample_either_direction()
    {
        Assert.Equal(0.75f, AudioLevel.Peak(new[] { 0.1f, -0.75f, 0.5f }));
        Assert.Equal(0f, AudioLevel.Peak(ReadOnlySpan<float>.Empty));
        Assert.Equal(1f, AudioLevel.Peak(new[] { 2.5f })); // never more than full scale
    }

    [Fact]
    public void Meter_follows_decibels()
    {
        Assert.Equal(0, AudioLevel.ToMeter(0));          // silence
        Assert.Equal(0, AudioLevel.ToMeter(0.0005));     // below -60 dB
        Assert.Equal(1, AudioLevel.ToMeter(1));          // full scale
        Assert.Equal(0.5, AudioLevel.ToMeter(Math.Pow(10, -30 / 20.0)), 3); // -30 dB is the middle
        Assert.True(AudioLevel.IsClipping(1));
        Assert.False(AudioLevel.IsClipping(0.5));
    }

    [Fact]
    public void Recording_names_sort_by_date_and_work_on_windows()
    {
        var name = RecordingFiles.NewFileName(new DateTime(2026, 11, 15, 9, 30, 5));

        Assert.Equal("Recording 2026-11-15 09.30.05.wav", name);
        Assert.DoesNotContain(':', name);
    }

    [Fact]
    public void A_new_recording_never_overwrites_an_old_one()
    {
        var folder = Directory.CreateTempSubdirectory("churchie-rec").FullName;
        try
        {
            var now = new DateTime(2026, 11, 15, 9, 30, 5);
            var first = RecordingFiles.NewFilePath(folder, now);
            File.WriteAllText(first, "x");
            var second = RecordingFiles.NewFilePath(folder, now);

            Assert.NotEqual(first, second);
            Assert.EndsWith("Recording 2026-11-15 09.30.05 (2).wav", second);
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void Listing_shows_recordings_newest_first_with_their_length()
    {
        var folder = Directory.CreateTempSubdirectory("churchie-rec").FullName;
        try
        {
            var older = Path.Combine(folder, "Older.wav");
            var newer = Path.Combine(folder, "Newer.wav");
            using (var wav = new WavWriter(older, 8000)) wav.Write(new float[8000]);
            using (var wav = new WavWriter(newer, 8000)) wav.Write(new float[24000]);
            File.SetLastWriteTime(older, new DateTime(2026, 11, 1));
            File.SetLastWriteTime(newer, new DateTime(2026, 11, 8));
            File.WriteAllText(Path.Combine(folder, "notes.txt"), "not a recording");

            var list = RecordingFiles.List(folder);

            Assert.Equal(new[] { "Newer", "Older" }, list.Select(r => r.Name));
            Assert.Equal(TimeSpan.FromSeconds(3), list[0].Duration);
            Assert.Empty(RecordingFiles.List(Path.Combine(folder, "missing")));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(65, "1:05")]
    [InlineData(3723, "1:02:03")]
    [InlineData(-5, "0:00")]
    public void Durations_read_like_a_clock(int seconds, string expected) =>
        Assert.Equal(expected, RecordingFiles.FormatDuration(TimeSpan.FromSeconds(seconds)));

    [Theory]
    [InlineData(512 * 1024, "512 KB")]
    [InlineData(13 * 1024 * 1024, "13.0 MB")]
    [InlineData(1288490189, "1.20 GB")]
    public void Sizes_use_sensible_units(long bytes, string expected) =>
        Assert.Equal(expected, RecordingFiles.FormatSize(bytes));

    /// <summary>Lets a test look at a MemoryStream after the writer has closed "its" stream.</summary>
    private sealed class NonClosingStream(MemoryStream inner) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
    }
}
