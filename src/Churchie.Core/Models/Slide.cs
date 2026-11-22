namespace Churchie.Core.Models;

public enum SlideKind
{
    Song,
    Scripture,
    Text,
    Image,
    Video,
}

/// <summary>
/// One screen of content that can be shown on the projector.
/// A <c>record</c> is a class whose equality is based on its values, and whose
/// properties are read-only once created — perfect for "a snapshot of a slide".
/// </summary>
/// <param name="Label">Short name shown on thumbnails, e.g. "Chorus" or "John 3:16".</param>
/// <param name="Text">The words shown on screen.</param>
/// <param name="Footer">Small text at the bottom, e.g. the scripture reference.</param>
/// <param name="MediaPath">
/// When set, this picture, animated GIF or video is shown instead of text
/// (used for media slides and the logo).
/// </param>
/// <param name="ImageFit">How that picture or video is sized to the screen.</param>
/// <param name="Loop">For a video: start again from the beginning when it ends. Otherwise it stops on its last frame.</param>
/// <param name="Muted">For a video: play it without sound (used for a video logo).</param>
public sealed record Slide(
    SlideKind Kind,
    string Label,
    string Text,
    string? Footer = null,
    string? MediaPath = null,
    ImageFit ImageFit = ImageFit.Fit,
    bool Loop = false,
    bool Muted = false)
{
    /// <summary>A slide with nothing on it — shows only the background.</summary>
    public static Slide Empty { get; } = new(SlideKind.Text, "", "");
}
