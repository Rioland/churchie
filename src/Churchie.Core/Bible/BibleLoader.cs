using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace Churchie.Core.Bible;

/// <summary>
/// Reads Bible files. Two formats are supported:
///  • Churchie JSON (.json or .json.gz) — the format of the bundled KJV.
///  • Zefania XML (.xml) — a free format with many translations available,
///    including Yoruba, Igbo and Hausa Bibles.
/// </summary>
public static class BibleLoader
{
    public static BibleTranslation LoadFile(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();
        using var file = File.OpenRead(path);
        Stream stream = name.EndsWith(".gz") ? new GZipStream(file, CompressionMode.Decompress) : file;
        using (stream)
        {
            var fallbackAbbrev = Path.GetFileName(path).Split('.')[0].ToUpperInvariant();
            return name.Contains(".xml")
                ? LoadZefania(stream, fallbackAbbrev)
                : LoadJson(stream);
        }
    }

    // ---------- Churchie JSON ----------

    private sealed class JsonBible
    {
        [JsonPropertyName("abbreviation")] public string? Abbreviation { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("books")] public List<JsonBook> Books { get; set; } = new();
    }

    private sealed class JsonBook
    {
        [JsonPropertyName("number")] public int? Number { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("chapters")] public List<List<string>> Chapters { get; set; } = new();
    }

    public static BibleTranslation LoadJson(Stream stream)
    {
        var data = JsonSerializer.Deserialize<JsonBible>(stream)
                   ?? throw new InvalidDataException("Empty Bible file");

        var books = data.Books.Select((b, i) =>
        {
            var number = b.Number ?? i + 1;
            return new BibleBook
            {
                Number = number,
                Name = string.IsNullOrWhiteSpace(b.Name) ? BookNames.EnglishName(number) : b.Name!,
                Chapters = b.Chapters,
            };
        }).ToList();

        return new BibleTranslation
        {
            Abbreviation = data.Abbreviation ?? "BIBLE",
            Name = data.Name ?? data.Abbreviation ?? "Bible",
            Books = books,
        };
    }

    // ---------- Zefania XML ----------

    public static BibleTranslation LoadZefania(Stream stream, string fallbackAbbreviation)
    {
        var doc = XDocument.Load(stream);
        var root = doc.Root ?? throw new InvalidDataException("Empty XML file");

        var info = root.Element("INFORMATION");
        var name = (string?)root.Attribute("biblename")
                   ?? info?.Element("title")?.Value
                   ?? fallbackAbbreviation;
        var abbrev = info?.Element("identifier")?.Value;
        if (string.IsNullOrWhiteSpace(abbrev)) abbrev = fallbackAbbreviation;

        var books = new List<BibleBook>();
        foreach (var bookEl in root.Elements("BIBLEBOOK"))
        {
            var number = (int?)bookEl.Attribute("bnumber") ?? books.Count + 1;
            var bookName = (string?)bookEl.Attribute("bname");
            if (string.IsNullOrWhiteSpace(bookName)) bookName = BookNames.EnglishName(number);

            var chapters = new SortedDictionary<int, SortedDictionary<int, string>>();
            foreach (var chEl in bookEl.Elements("CHAPTER"))
            {
                var c = (int?)chEl.Attribute("cnumber") ?? chapters.Count + 1;
                var verses = new SortedDictionary<int, string>();
                foreach (var vEl in chEl.Elements("VERS"))
                {
                    var v = (int?)vEl.Attribute("vnumber") ?? verses.Count + 1;
                    verses[v] = VerseText(vEl);
                }
                chapters[c] = verses;
            }

            books.Add(new BibleBook
            {
                Number = number,
                Name = bookName.Trim(),
                Chapters = Densify(chapters),
            });
        }

        return new BibleTranslation { Abbreviation = abbrev.Trim(), Name = name.Trim(), Books = books };
    }

    /// <summary>Verse text without footnotes or cross references.</summary>
    private static string VerseText(XElement verse)
    {
        var copy = new XElement(verse);
        copy.Descendants().Where(e => e.Name.LocalName is "NOTE" or "XREF" or "gr").ToList().ForEach(e => e.Remove());
        return string.Join(" ", copy.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Turns numbered dictionaries into lists, filling any gaps with empty text.</summary>
    private static IReadOnlyList<IReadOnlyList<string>> Densify(SortedDictionary<int, SortedDictionary<int, string>> chapters)
    {
        var maxChapter = chapters.Count == 0 ? 0 : chapters.Keys.Max();
        var result = new List<IReadOnlyList<string>>(maxChapter);
        for (var c = 1; c <= maxChapter; c++)
        {
            if (!chapters.TryGetValue(c, out var verses)) { result.Add(Array.Empty<string>()); continue; }
            var maxVerse = verses.Count == 0 ? 0 : verses.Keys.Max();
            var list = new string[maxVerse];
            for (var v = 1; v <= maxVerse; v++) list[v - 1] = verses.TryGetValue(v, out var t) ? t : "";
            result.Add(list);
        }
        return result;
    }
}
