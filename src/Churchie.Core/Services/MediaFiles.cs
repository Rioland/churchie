namespace Churchie.Core.Services;

/// <summary>The sort of file a media path points to.</summary>
public enum MediaKind
{
    /// <summary>Not a file Churchie can show.</summary>
    None,

    /// <summary>A still picture — or an animated GIF/WebP, which the app plays like a short silent clip.</summary>
    Picture,

    /// <summary>A video file. The app plays it with sound when it is a slide, silently when it is a background.</summary>
    Video,
}

/// <summary>
/// Knows which files can be added on the Media tab, judged by the file extension
/// (the part after the last dot). Opening every file to look inside would be slow,
/// and the extension is what the file dialogs filter on anyway.
/// </summary>
public static class MediaFiles
{
    /// <summary>Picture formats. GIF and WebP files may hold several frames (an animation).</summary>
    public static IReadOnlyList<string> PictureExtensions { get; } =
        new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };

    /// <summary>Common video formats. VLC (which plays them) understands many more, but these cover phones and cameras.</summary>
    public static IReadOnlyList<string> VideoExtensions { get; } =
        new[] { ".mp4", ".m4v", ".mov", ".avi", ".mkv", ".webm", ".wmv", ".mpg", ".mpeg" };

    /// <summary>Patterns for a file dialog that shows only pictures, e.g. "*.png".</summary>
    public static IReadOnlyList<string> PicturePatterns { get; } = ToPatterns(PictureExtensions);

    /// <summary>Patterns for a file dialog that shows only videos.</summary>
    public static IReadOnlyList<string> VideoPatterns { get; } = ToPatterns(VideoExtensions);

    /// <summary>Patterns for a file dialog that shows pictures and videos together.</summary>
    public static IReadOnlyList<string> AllPatterns { get; } = PicturePatterns.Concat(VideoPatterns).ToArray();

    public static MediaKind KindOf(string? path)
    {
        var extension = ExtensionOf(path);
        if (PictureExtensions.Contains(extension)) return MediaKind.Picture;
        if (VideoExtensions.Contains(extension)) return MediaKind.Video;
        return MediaKind.None;
    }

    public static bool IsVideo(string? path) => KindOf(path) == MediaKind.Video;

    /// <summary>
    /// True for formats that can contain an animation (GIF and WebP). Whether this
    /// particular file really moves is only known once it is opened.
    /// </summary>
    public static bool MayBeAnimated(string? path) => ExtensionOf(path) is ".gif" or ".webp";

    /// <summary>The extension in lower case ("Photo.JPG" gives ".jpg"), or "" when there is none.</summary>
    private static string ExtensionOf(string? path) =>
        string.IsNullOrEmpty(path) ? "" : Path.GetExtension(path).ToLowerInvariant();

    private static string[] ToPatterns(IEnumerable<string> extensions) =>
        extensions.Select(e => "*" + e).ToArray();
}
