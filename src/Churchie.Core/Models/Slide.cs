namespace Churchie.Core.Models;

public enum SlideKind
{
    Song,
    Scripture,
    Text,
    Image,
}

/// <summary>
/// One screen of content that can be shown on the projector.
/// A <c>record</c> is a class whose equality is based on its values, and whose
/// properties are read-only once created — perfect for "a snapshot of a slide".
/// </summary>
/// <param name="Label">Short name shown on thumbnails, e.g. "Chorus" or "John 3:16".</param>
/// <param name="Text">The words shown on screen.</param>
/// <param name="Footer">Small text at the bottom, e.g. the scripture reference.</param>
/// <param name="ImagePath">When set, this image fills the screen (used for media slides and the logo).</param>
public sealed record Slide(
    SlideKind Kind,
    string Label,
    string Text,
    string? Footer = null,
    string? ImagePath = null)
{
    /// <summary>A slide with nothing on it — shows only the background.</summary>
    public static Slide Empty { get; } = new(SlideKind.Text, "", "");
}
