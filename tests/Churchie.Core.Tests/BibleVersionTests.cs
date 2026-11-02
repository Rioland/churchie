using Churchie.Core.Bible;
using Churchie.Core.Services;

namespace Churchie.Core.Tests;

/// <summary>The bundled Yoruba and World English Bibles, other file formats, and the Bible list order.</summary>
public class BibleVersionTests
{
    private static readonly Lazy<BibleLibrary> Library = new(() =>
    {
        var lib = new BibleLibrary();
        foreach (var file in new[] { "kjv.json.gz", "yo-oycb.json.gz", "en-web.json.gz" })
            lib.Add(BibleLoader.LoadFile(Path.Combine(AppContext.BaseDirectory, file)));
        return lib;
    });

    [Fact]
    public void Yoruba_bible_has_all_books_names_and_licence()
    {
        var yoruba = Library.Value.Find("OYCB");

        Assert.NotNull(yoruba);
        Assert.Equal(66, yoruba!.Books.Count);
        Assert.Equal("yo", yoruba.Language);
        Assert.Equal("Johanu", yoruba.GetBook(43)!.Name);
        Assert.Equal("Saamu", yoruba.GetBook(19)!.Name);
        Assert.Contains("Biblica", yoruba.Copyright);
        Assert.Contains("Attribution-ShareAlike", yoruba.Copyright);
    }

    [Theory]
    [InlineData("Johanu 3:16")]   // Yoruba book name
    [InlineData("jn 3 16")]       // English abbreviations still work
    [InlineData("John 3:16")]
    public void Yoruba_lookup_works_with_yoruba_or_english_names(string reference)
    {
        var passage = Library.Value.Lookup("OYCB", reference, out var error);

        Assert.Null(error);
        Assert.Equal("Johanu 3:16", passage!.Title);
        Assert.Contains("Ọlọ́run", passage.Verses.Single().Text);
    }

    [Fact]
    public void Yoruba_psalm_23_by_yoruba_name()
    {
        var passage = Library.Value.Lookup("OYCB", "Saamu 23", out _);

        Assert.Equal(6, passage!.Verses.Count);
        Assert.StartsWith("Olúwa ni olùṣọ́", passage.Verses[0].Text);
    }

    [Fact]
    public void Verses_a_translation_leaves_out_are_skipped()
    {
        // Acts 8:37 is left out of the World English Bible (and many modern translations).
        var passage = Library.Value.Lookup("WEB", "Acts 8:36-38", out _);
        Assert.Equal(new[] { 36, 38 }, passage!.Verses.Select(v => v.Number).ToArray());

        var single = Library.Value.Lookup("WEB", "Acts 8:37", out var error);
        Assert.Null(single);
        Assert.Contains("not included", error);
    }

    [Fact]
    public void Kjv_comes_first_then_english_then_other_languages()
    {
        var order = Library.Value.InDisplayOrder().Select(b => b.Abbreviation).ToArray();

        Assert.Equal(new[] { "KJV", "WEB", "OYCB" }, order);
    }

    [Fact]
    public void Loads_opensong_xml()
    {
        const string xml = """
            <bible>
              <b n="Genesis"><c n="1"><v n="1">In the beginning.</v><v n="2">And the earth.</v></c></b>
              <b n="1 Samuel"><c n="3"><v n="10">Speak; for thy servant heareth.</v></c></b>
            </bible>
            """;
        var bible = BibleLoader.LoadXml(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)), "TEST");

        Assert.Equal("And the earth.", bible.GetBook(1)!.Chapters[0][1]);
        Assert.Equal("1 Samuel", bible.GetBook(9)!.Name); // matched by name, not position
        Assert.Equal("Speak; for thy servant heareth.", bible.GetBook(9)!.Chapters[2][9]);
    }

    [Fact]
    public void Loads_osis_with_container_and_milestone_verses_and_skips_notes()
    {
        const string xml = """
            <osis xmlns="http://www.bibletechnologies.net/2003/OSIS/namespace">
              <osisText osisIDWork="TST" xml:lang="en">
                <header><work osisWork="TST"><title>Test Bible</title><rights>Public domain</rights></work></header>
                <div type="book" osisID="John">
                  <chapter osisID="John.3">
                    <title>Jesus and Nicodemus</title>
                    <verse osisID="John.3.16">For God so loved<note>a footnote</note> the world.</verse>
                    <verse sID="v17" osisID="John.3.17"/>For God sent not his Son.<verse eID="v17"/>
                  </chapter>
                </div>
              </osisText>
            </osis>
            """;
        var bible = BibleLoader.LoadXml(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml)), "X");

        Assert.Equal("TST", bible.Abbreviation);
        Assert.Equal("Test Bible", bible.Name);
        Assert.Equal("Public domain", bible.Copyright);
        var john3 = bible.GetBook(43)!.Chapters[2];
        Assert.Equal("For God so loved the world.", john3[15]);
        Assert.Equal("For God sent not his Son.", john3[16]);
    }

    [Fact]
    public void Unknown_xml_is_rejected_with_a_helpful_message()
    {
        var ex = Assert.Throws<InvalidDataException>(() =>
            BibleLoader.LoadXml(new MemoryStream("<song/>"u8.ToArray()), "X"));
        Assert.Contains("Unknown Bible format", ex.Message);
    }

    [Fact]
    public void Scripture_slides_from_yoruba_carry_the_version_in_the_footer()
    {
        var passage = Library.Value.Lookup("OYCB", "Johanu 3:16", out _)!;
        var slide = SlideBuilder.FromPassage(passage).Single();

        Assert.Equal("Johanu 3:16 (OYCB)", slide.Footer);
    }
}
