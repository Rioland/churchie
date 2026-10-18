using Churchie.Core.Bible;
using Churchie.Core.Services;

namespace Churchie.Core.Tests;

public class ScriptureTests
{
    private static readonly Lazy<BibleLibrary> Library = new(() =>
    {
        var lib = new BibleLibrary();
        lib.Add(BibleLoader.LoadFile(Path.Combine(AppContext.BaseDirectory, "kjv.json.gz")));
        return lib;
    });

    [Theory]
    [InlineData("John 3:16", 43, 3, 16, null)]
    [InlineData("jn 3 16", 43, 3, 16, null)]
    [InlineData("1 Cor 13:4-7", 46, 13, 4, 7)]
    [InlineData("1cor13:4", 46, 13, 4, null)]
    [InlineData("I Samuel 3", 9, 3, null, null)]
    [InlineData("ii kings 2:11", 12, 2, 11, null)]
    [InlineData("Psalm 23", 19, 23, null, null)]
    [InlineData("Rev. 21:4", 66, 21, 4, null)]
    [InlineData("Gen", 1, 1, null, null)]
    [InlineData("Jude 3", 65, 3, null, null)]
    [InlineData("Judg 6:12", 7, 6, 12, null)]
    [InlineData("Phil 4:13", 50, 4, 13, null)]
    [InlineData("Phm 1", 57, 1, null, null)]
    [InlineData("Song of Solomon 2:1", 22, 2, 1, null)]
    [InlineData("Isaiah 53:5", 23, 53, 5, null)]
    [InlineData("3 jn 1:2", 64, 1, 2, null)]
    [InlineData("John 3:18-16", 43, 3, 16, 18)]
    public void Parses_common_references(string input, int book, int chapter, int? start, int? end)
    {
        Assert.True(ScriptureReference.TryParse(input, null, out var r, out var error), error);
        Assert.Equal(new ScriptureReference(book, chapter, start, end), r);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Hezekiah 1:1")]
    [InlineData("12345")]
    public void Rejects_nonsense(string input)
    {
        Assert.False(ScriptureReference.TryParse(input, null, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Kjv_has_all_books()
    {
        var kjv = Library.Value.Find("KJV");
        Assert.NotNull(kjv);
        Assert.Equal(66, kjv!.Books.Count);
        Assert.Equal("1 Samuel", kjv.GetBook(9)!.Name);
        Assert.Equal("Revelation", kjv.GetBook(66)!.Name);
    }

    [Fact]
    public void Looks_up_a_verse()
    {
        var passage = Library.Value.Lookup("KJV", "John 3:16", out var error);

        Assert.Null(error);
        Assert.NotNull(passage);
        Assert.Equal("John 3:16", passage!.Title);
        Assert.StartsWith("For God so loved the world", passage.Verses.Single().Text);
    }

    [Fact]
    public void Whole_chapter_returns_every_verse()
    {
        var passage = Library.Value.Lookup("KJV", "Psalm 23", out _);

        Assert.Equal(6, passage!.Verses.Count);
        Assert.Equal("Psalms 23", passage.Title);
    }

    [Fact]
    public void Range_past_the_end_is_trimmed()
    {
        var passage = Library.Value.Lookup("KJV", "Jude 1:20-99", out _);

        Assert.Equal(new[] { 20, 21, 22, 23, 24, 25 }, passage!.Verses.Select(v => v.Number).ToArray());
    }

    [Theory]
    [InlineData("John 22")]
    [InlineData("Jude 2:1")]
    [InlineData("John 3:99")]
    public void Explains_invalid_numbers(string input)
    {
        var passage = Library.Value.Lookup("KJV", input, out var error);

        Assert.Null(passage);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void Passage_slides_group_verses_and_carry_a_footer()
    {
        var passage = Library.Value.Lookup("KJV", "Psalm 23:1-5", out _)!;
        var slides = SlideBuilder.FromPassage(passage, versesPerSlide: 2);

        Assert.Equal(3, slides.Count);
        Assert.Equal("23:1-2", slides[0].Label);
        Assert.Equal("Psalms 23:5 (KJV)", slides[2].Footer);
        Assert.StartsWith("1 ", slides[0].Text);
        Assert.Contains("is my shepherd", slides[0].Text);
    }

    [Fact]
    public void Loads_zefania_xml()
    {
        const string xml = """
            <?xml version="1.0" encoding="utf-8"?>
            <XMLBIBLE biblename="Bibeli Mimọ">
              <INFORMATION><identifier>YOR</identifier></INFORMATION>
              <BIBLEBOOK bnumber="43" bname="Johanu">
                <CHAPTER cnumber="3">
                  <VERS vnumber="16">Nitori Ọlọrun fẹ araiye <NOTE>a note</NOTE>tobẹ̃.</VERS>
                </CHAPTER>
              </BIBLEBOOK>
            </XMLBIBLE>
            """;
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        var bible = BibleLoader.LoadZefania(stream, "TEST");

        Assert.Equal("YOR", bible.Abbreviation);
        var book = bible.GetBook(43)!;
        Assert.Equal("Johanu", book.Name);
        Assert.Equal(3, book.ChapterCount);
        Assert.Equal("Nitori Ọlọrun fẹ araiye tobẹ̃.", book.Chapters[2][15]);

        // Book names in the translation's own language work too.
        Assert.True(ScriptureReference.TryParse("Johanu 3:16", bible, out var r, out _));
        Assert.Equal(43, r!.BookNumber);
    }
}
