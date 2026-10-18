namespace Churchie.Core.Models;

/// <summary>Everything the user can configure. Saved as settings.json.</summary>
public sealed class AppSettings
{
    public SlideTheme SongTheme { get; set; } = SlideTheme.DefaultSongs();
    public SlideTheme ScriptureTheme { get; set; } = SlideTheme.DefaultScripture();

    /// <summary>Which monitor the projector output opens on (0 = first screen).</summary>
    public int OutputScreenIndex { get; set; } = 1;

    /// <summary>Split song sections longer than this into several slides. 0 = never split.</summary>
    public int MaxLinesPerSlide { get; set; }

    public int VersesPerSlide { get; set; } = 1;
    public bool ShowVerseNumbers { get; set; } = true;
    public string DefaultBible { get; set; } = "KJV";

    /// <summary>Image shown when the operator presses "Logo".</summary>
    public string? LogoPath { get; set; }

    public List<string> MediaFiles { get; set; } = new();
}
