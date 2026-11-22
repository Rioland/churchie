using System.Globalization;

namespace Churchie.App.Playback;

/// <summary>
/// An optional log of what the video engine is doing, for tracking down playback problems.
/// It is off unless the CHURCHIE_VIDEO_LOG environment variable is set (to anything);
/// then Churchie writes video-log.txt (our own notes) and vlc-log.txt (VLC's messages)
/// into its data folder.
/// </summary>
public static class VideoLog
{
    private static readonly object Gate = new();

    /// <summary>The folder the logs go in, or null when logging is off.</summary>
    public static string? Folder { get; private set; }

    public static bool IsOn => Folder is not null;

    /// <summary>Called once at startup with Churchie's data folder.</summary>
    public static void Start(string dataFolder)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CHURCHIE_VIDEO_LOG"))) return;
        Folder = dataFolder;
        try { File.WriteAllText(Path.Combine(dataFolder, "video-log.txt"), ""); }
        catch { Folder = null; }
        Write("Video log started");
    }

    /// <summary>Adds a line with the time. Safe to call from any thread; does nothing when logging is off.</summary>
    public static void Write(string message)
    {
        if (Folder is null) return;
        var line = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + message + Environment.NewLine;
        lock (Gate)
        {
            try { File.AppendAllText(Path.Combine(Folder, "video-log.txt"), line); }
            catch { /* a log must never break playback */ }
        }
    }
}
