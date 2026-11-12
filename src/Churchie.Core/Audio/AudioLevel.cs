namespace Churchie.Core.Audio;

/// <summary>Helpers for the recording level meter.</summary>
public static class AudioLevel
{
    /// <summary>The quietest level the meter shows. Anything below this is drawn as empty.</summary>
    public const double FloorDecibels = -60;

    /// <summary>At or above this the sound is distorting ("clipping") and the meter turns red.</summary>
    public const double ClipDecibels = -1;

    /// <summary>The loudest sample in a block of sound, from 0 (silence) to 1 (full scale).</summary>
    public static float Peak(ReadOnlySpan<float> samples)
    {
        var peak = 0f;
        foreach (var sample in samples)
        {
            var size = Math.Abs(sample);
            if (size > peak) peak = size;
        }
        return Math.Min(peak, 1f);
    }

    /// <summary>
    /// Converts a peak (0..1) to decibels, the scale sound engineers use: 0 dB is the
    /// loudest possible, -6 dB is half as loud, and silence is minus infinity.
    /// </summary>
    public static double ToDecibels(double peak) =>
        peak <= 0 ? double.NegativeInfinity : 20 * Math.Log10(peak);

    /// <summary>
    /// Where the meter bar should be, from 0 (empty) to 1 (full). Ears hear loudness
    /// in decibels, so the bar follows decibels: speech then sits around the middle
    /// instead of barely moving at the bottom.
    /// </summary>
    public static double ToMeter(double peak)
    {
        var decibels = ToDecibels(peak);
        if (decibels <= FloorDecibels) return 0;
        return Math.Clamp((decibels - FloorDecibels) / -FloorDecibels, 0, 1);
    }

    public static bool IsClipping(double peak) => ToDecibels(peak) >= ClipDecibels;
}
