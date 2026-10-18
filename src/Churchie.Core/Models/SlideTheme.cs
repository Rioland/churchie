namespace Churchie.Core.Models;

/// <summary>
/// How slides look on the projector: background, font, colours and layout.
/// Colours are stored as hex strings ("#RRGGBB") so they save nicely to JSON.
/// </summary>
public sealed class SlideTheme
{
    public string Name { get; set; } = "Default";

    // Background
    public string BackgroundColor { get; set; } = "#101828";
    public string? BackgroundImage { get; set; }

    /// <summary>0 = image fully visible, 0.8 = image darkened a lot so text stands out.</summary>
    public double BackgroundDim { get; set; } = 0.35;

    // Text
    /// <summary>Empty means "use the app's default font".</summary>
    public string FontFamily { get; set; } = "";
    public double FontSize { get; set; } = 80;
    public string TextColor { get; set; } = "#FFFFFF";
    public bool Bold { get; set; } = true;
    public bool Uppercase { get; set; }
    public bool Shadow { get; set; } = true;

    /// <summary>"Left", "Center" or "Right".</summary>
    public string Alignment { get; set; } = "Center";

    /// <summary>"Top", "Center" or "Bottom".</summary>
    public string VerticalAlignment { get; set; } = "Center";

    // Footer (scripture reference, copyright line...)
    public double FooterFontSize { get; set; } = 40;
    public string FooterColor { get; set; } = "#E0E0E0";

    public SlideTheme Clone() => (SlideTheme)MemberwiseClone();

    public static SlideTheme DefaultSongs() => new() { Name = "Songs" };

    public static SlideTheme DefaultScripture() => new()
    {
        Name = "Scripture",
        BackgroundColor = "#1B1F3B",
        FontSize = 64,
        Bold = false,
        Alignment = "Left",
    };

    /// <summary>Plain black — used for the "Black" button.</summary>
    public static SlideTheme Black() => new()
    {
        Name = "Black",
        BackgroundColor = "#000000",
        BackgroundImage = null,
    };
}
