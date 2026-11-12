using System.Globalization;

namespace Churchie.Core.Audio;

/// <summary>One saved recording, as shown in the list on the Record tab.</summary>
public sealed record RecordingFile(string Path, string Name, DateTime Modified, long Size, TimeSpan? Duration);

/// <summary>Names, lists and describes the recordings in the recordings folder.</summary>
public static class RecordingFiles
{
    public const string Extension = ".wav";

    /// <summary>
    /// A file name for a recording that starts now, e.g. "Recording 2026-11-15 09.30.05.wav".
    /// Year first, so the files sort by date; no ":" because Windows doesn't allow it in names.
    /// </summary>
    public static string NewFileName(DateTime now) =>
        "Recording " + now.ToString("yyyy-MM-dd HH.mm.ss", CultureInfo.InvariantCulture) + Extension;

    /// <summary>A path in <paramref name="folder"/> that doesn't exist yet, so nothing is ever overwritten.</summary>
    public static string NewFilePath(string folder, DateTime now)
    {
        var path = System.IO.Path.Combine(folder, NewFileName(now));
        for (var n = 2; File.Exists(path); n++)
            path = System.IO.Path.Combine(folder, System.IO.Path.GetFileNameWithoutExtension(NewFileName(now)) + $" ({n})" + Extension);
        return path;
    }

    /// <summary>The recordings in a folder, newest first.</summary>
    public static List<RecordingFile> List(string folder)
    {
        if (!Directory.Exists(folder)) return new List<RecordingFile>();

        return new DirectoryInfo(folder)
            .EnumerateFiles("*" + Extension)
            .OrderByDescending(f => f.LastWriteTime)
            .Select(f => new RecordingFile(f.FullName, System.IO.Path.GetFileNameWithoutExtension(f.Name), f.LastWriteTime, f.Length, WavInfo.ReadDuration(f.FullName)))
            .ToList();
    }

    /// <summary>"4:05" for short recordings, "1:02:03" once they pass an hour.</summary>
    public static string FormatDuration(TimeSpan time)
    {
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        var hours = (int)time.TotalHours;
        return hours > 0
            ? $"{hours}:{time.Minutes:00}:{time.Seconds:00}"
            : $"{time.Minutes}:{time.Seconds:00}";
    }

    /// <summary>"850 KB", "12.4 MB" or "1.20 GB".</summary>
    public static string FormatSize(long bytes)
    {
        const double mb = 1024 * 1024;
        if (bytes < mb) return (bytes / 1024.0).ToString("0", CultureInfo.InvariantCulture) + " KB";
        if (bytes < 1024 * mb) return (bytes / mb).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
        return (bytes / (1024 * mb)).ToString("0.00", CultureInfo.InvariantCulture) + " GB";
    }
}
