using Churchie.Core.Services;

namespace Churchie.Core.Tests;

public class SongParserTests
{
    [Fact]
    public void Bracket_headers_name_sections()
    {
        var sections = SongParser.Parse("[Verse 1]\nLine a\nLine b\n\n[Chorus]\nLine c");

        Assert.Equal(2, sections.Count);
        Assert.Equal("Verse 1", sections[0].Label);
        Assert.Equal("Line a\nLine b", sections[0].Text);
        Assert.Equal("Chorus", sections[1].Label);
        Assert.Equal("Line c", sections[1].Text);
    }

    [Fact]
    public void Word_headers_are_recognised()
    {
        var sections = SongParser.Parse("verse 2:\nOne\n\nCHORUS\nTwo\n\nPre-chorus\nThree\n\nBridge:\nFour");

        Assert.Equal(new[] { "Verse 2", "Chorus", "Pre-Chorus", "Bridge" }, sections.Select(s => s.Label).ToArray());
    }

    [Fact]
    public void Unlabelled_blocks_become_numbered_verses()
    {
        var sections = SongParser.Parse("First\nblock\n\nSecond block\n\n\n\nThird block");

        Assert.Equal(new[] { "Verse 1", "Verse 2", "Verse 3" }, sections.Select(s => s.Label).ToArray());
    }

    [Fact]
    public void Header_followed_by_blank_line_still_labels_next_block()
    {
        var sections = SongParser.Parse("[Chorus]\n\nHallelujah");

        Assert.Single(sections);
        Assert.Equal("Chorus", sections[0].Label);
    }

    [Fact]
    public void Windows_line_endings_work()
    {
        var sections = SongParser.Parse("[Verse 1]\r\nGlory\r\nHonour\r\n\r\n[Chorus]\r\nPraise");

        Assert.Equal(2, sections.Count);
        Assert.Equal("Glory\nHonour", sections[0].Text);
    }

    [Fact]
    public void Short_headers_need_a_number()
    {
        Assert.True(SongParser.TryGetHeader("V1", out var v1));
        Assert.Equal("Verse 1", v1);
        Assert.True(SongParser.TryGetHeader("C2", out var c));
        Assert.Equal("Chorus 2", c);
        Assert.False(SongParser.TryGetHeader("V", out _));
        Assert.False(SongParser.TryGetHeader("C", out _));
        Assert.False(SongParser.TryGetHeader("O", out _));
        Assert.False(SongParser.TryGetHeader("Victory is mine", out _));
    }

    [Fact]
    public void ToText_round_trips()
    {
        var original = SongParser.Parse("[Verse 1]\nGlory\nHonour\n\n[Chorus]\nPraise");
        var again = SongParser.Parse(SongParser.ToText(original));

        Assert.Equal(original.Select(s => (s.Label, s.Text)), again.Select(s => (s.Label, s.Text)));
    }
}
