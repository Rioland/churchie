using Churchie.Core.Models;

namespace Churchie.Core.Tests;

public class LiveFrameTests
{
    private static readonly Slide Verse1 = new(SlideKind.Song, "Verse 1", "Amazing grace");
    private static readonly Slide Verse2 = new(SlideKind.Song, "Verse 2", "'Twas grace");

    [Fact]
    public void Theme_change_alone_counts_as_same_content()
    {
        var before = new LiveFrame(Verse1, SlideTheme.DefaultSongs());
        var after = new LiveFrame(Verse1, new SlideTheme { FontSize = 120 });

        Assert.True(after.HasSameContentAs(before)); // restyle instantly, no fade
    }

    [Fact]
    public void Next_slide_is_different_content()
    {
        var before = new LiveFrame(Verse1, SlideTheme.DefaultSongs());
        var after = new LiveFrame(Verse2, SlideTheme.DefaultSongs());

        Assert.False(after.HasSameContentAs(before)); // fade
    }

    [Fact]
    public void Black_clear_and_first_frame_are_transitions()
    {
        var live = new LiveFrame(Verse1, SlideTheme.DefaultSongs());
        var cleared = live with { Slide = Verse1 with { Text = "" } };

        Assert.False(LiveFrame.Black().HasSameContentAs(live));
        Assert.False(cleared.HasSameContentAs(live));
        Assert.False(live.HasSameContentAs(null));
    }

    [Fact]
    public void A_repeated_chorus_with_identical_words_does_not_flash()
    {
        var chorus = new Slide(SlideKind.Song, "Chorus", "This is my story");
        var first = new LiveFrame(chorus, SlideTheme.DefaultSongs());
        var again = new LiveFrame(chorus with { }, SlideTheme.DefaultSongs());

        Assert.True(again.HasSameContentAs(first));
    }
}
