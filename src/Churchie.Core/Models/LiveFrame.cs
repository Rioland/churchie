namespace Churchie.Core.Models;

/// <summary>
/// Everything on the projector at one moment: the slide (null = nothing but the
/// background) and the theme it's drawn with. Kept together so the screen can
/// change both in one step — and fade smoothly from one frame to the next.
/// </summary>
public sealed record LiveFrame(Slide? Slide, SlideTheme Theme)
{
    /// <summary>Plain black: nothing live, or the Black button.</summary>
    public static LiveFrame Black() => new(null, SlideTheme.Black());

    /// <summary>
    /// True when <paramref name="other"/> shows the same words/picture and only the
    /// look may differ (e.g. while dragging the text-size slider). Those changes
    /// should appear instantly; anything else gets a transition.
    /// </summary>
    public bool HasSameContentAs(LiveFrame? other) =>
        other is not null && Equals(Slide, other.Slide);
}
