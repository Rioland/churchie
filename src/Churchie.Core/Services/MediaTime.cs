namespace Churchie.Core.Services;

/// <summary>Writes video times the way media players show them.</summary>
public static class MediaTime
{
    /// <summary>"0:05", "12:30", or "1:02:03" for anything an hour or longer.</summary>
    public static string Format(TimeSpan time)
    {
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}";
    }
}
