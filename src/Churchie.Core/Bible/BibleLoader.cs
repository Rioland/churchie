using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace Churchie.Core.Bible;

/// <summary>
/// Reads Bible files. Supported formats:
///  • Churchie JSON (.json or .json.gz) — the format of the Bibles that ship with the app.
///  • Zefania XML (.xml) — a free format with many translations available.
///  • OpenSong XML (.xml) — used by OpenSong / OpenLP.
///  • OSIS XML (.xml) — the standard format used by Bible societies and SWORD.
/// The XML flavour is detected from the file's root element.
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
                ? LoadXml(stream, fallbackAbbrev)
                : LoadJson(stream);
        }
    }

    /// <summary>Works out which XML format a file uses and reads it.</summary>
    public static BibleTranslation LoadXml(Stream stream, string fallbackAbbreviation)
    {
        var doc = XDocument.Load(stream);
        var root = doc.Root ?? throw new InvalidDataException("Empty XML file");

        return root.Name.LocalName.ToLowerInvariant() switch
        {
            "xmlbible" => LoadZefania(doc, fallbackAbbreviation),
            "bible" => LoadOpenSong(doc, fallbackAbbreviation),
            "osis" => LoadOsis(doc, fallbackAbbreviation),
            _ => throw new InvalidDataException(
                $"Unknown Bible format <{root.Name.LocalName}>. Supported: Zefania, OpenSong and OSIS XML, or Churchie JSON."),
        };
    }

    // ---------- Churchie JSON ----------

    private sealed class JsonBible
    {
        [JsonPropertyName("abbreviation")] public string? Abbreviation { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("language")] public string? Language { get; set; }
        [JsonPropertyName("copyright")] public string? Copyright { get; set; }
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
            Language = data.Language ?? "",
            Copyright = data.Copyright ?? "",
            Books = books,
        };
    }

    // ---------- Zefania XML ----------
    // <XMLBIBLE><BIBLEBOOK bnumber="43" bname="John"><CHAPTER cnumber="3"><VERS vnumber="16">…

    public static BibleTranslation LoadZefania(Stream stream, string fallbackAbbreviation) =>
        LoadZefania(XDocument.Load(stream), fallbackAbbreviation);

    private static BibleTranslation LoadZefania(XDocument doc, string fallbackAbbreviation)
    {
        var root = doc.Root!;
        var info = root.Element("INFORMATION");
        var name = (string?)root.Attribute("biblename") ?? info?.Element("title")?.Value ?? fallbackAbbreviation;
        var abbrev = info?.Element("identifier")?.Value;
        if (string.IsNullOrWhiteSpace(abbrev)) abbrev = fallbackAbbreviation;

        var collector = new VerseCollector();
        var bookNames = new Dictionary<int, string>();
        foreach (var bookEl in root.Elements("BIBLEBOOK"))
        {
            var number = (int?)bookEl.Attribute("bnumber") ?? bookNames.Count + 1;
            var bookName = (string?)bookEl.Attribute("bname");
            if (!string.IsNullOrWhiteSpace(bookName)) bookNames[number] = bookName.Trim();

            foreach (var chEl in bookEl.Elements("CHAPTER"))
            {
                var c = (int?)chEl.Attribute("cnumber") ?? 1;
                var nextVerse = 1;
                foreach (var vEl in chEl.Elements("VERS"))
                {
                    var v = (int?)vEl.Attribute("vnumber") ?? nextVerse;
                    collector.Set(number, c, v, VerseText(vEl, "NOTE", "XREF", "gr"));
                    nextVerse = v + 1;
                }
            }
        }

        return collector.Build(abbrev.Trim(), name.Trim(),
            language: info?.Element("language")?.Value ?? "",
            copyright: info?.Element("rights")?.Value ?? "",
            bookNames);
    }

    // ---------- OpenSong XML ----------
    // <bible><b n="Genesis"><c n="1"><v n="1">In the beginning…</v>

    private static BibleTranslation LoadOpenSong(XDocument doc, string fallbackAbbreviation)
    {
        var root = doc.Root!;
        var collector = new VerseCollector();
        var bookNames = new Dictionary<int, string>();
        var position = 0;

        foreach (var bookEl in root.Elements().Where(e => e.Name.LocalName == "b"))
        {
            position++;
            var name = ((string?)bookEl.Attribute("n"))?.Trim() ?? "";

            // Prefer the book's name ("1 Samuel" → 9); fall back to its position in the file.
            var number = ScriptureReference.TryParse(name, null, out var reference, out _) && reference is not null
                ? reference.BookNumber
                : position;
            if (name.Length > 0) bookNames[number] = name;

            foreach (var chEl in bookEl.Elements().Where(e => e.Name.LocalName == "c"))
            {
                var c = (int?)chEl.Attribute("n") ?? 1;
                var nextVerse = 1;
                foreach (var vEl in chEl.Elements().Where(e => e.Name.LocalName == "v"))
                {
                    var v = (int?)vEl.Attribute("n") ?? nextVerse;
                    collector.Set(number, c, v, VerseText(vEl));
                    nextVerse = v + 1;
                }
            }
        }

        return collector.Build(fallbackAbbreviation, fallbackAbbreviation, "", "", bookNames);
    }

    // ---------- OSIS XML ----------
    // <osis><osisText osisIDWork="WEB"><div type="book" osisID="John"><chapter osisID="John.3">
    //   <verse osisID="John.3.16">…</verse>               (container style), or
    //   <verse sID="x" osisID="John.3.16"/>…<verse eID="x"/>   (milestone style)

    private static readonly string[] OsisBookCodes =
    {
        "Gen", "Exod", "Lev", "Num", "Deut", "Josh", "Judg", "Ruth", "1Sam", "2Sam", "1Kgs", "2Kgs",
        "1Chr", "2Chr", "Ezra", "Neh", "Esth", "Job", "Ps", "Prov", "Eccl", "Song", "Isa", "Jer",
        "Lam", "Ezek", "Dan", "Hos", "Joel", "Amos", "Obad", "Jonah", "Mic", "Nah", "Hab", "Zeph",
        "Hag", "Zech", "Mal", "Matt", "Mark", "Luke", "John", "Acts", "Rom", "1Cor", "2Cor", "Gal",
        "Eph", "Phil", "Col", "1Thess", "2Thess", "1Tim", "2Tim", "Titus", "Phlm", "Heb", "Jas",
        "1Pet", "2Pet", "1John", "2John", "3John", "Jude", "Rev",
    };

    private static BibleTranslation LoadOsis(XDocument doc, string fallbackAbbreviation)
    {
        var root = doc.Root!;
        var osisText = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "osisText");
        var work = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "work");
        var abbrev = (string?)osisText?.Attribute("osisIDWork");
        if (string.IsNullOrWhiteSpace(abbrev)) abbrev = fallbackAbbreviation;
        var name = work?.Elements().FirstOrDefault(e => e.Name.LocalName == "title")?.Value;
        var rights = work?.Elements().FirstOrDefault(e => e.Name.LocalName == "rights")?.Value ?? "";
        var language = (string?)osisText?.Attribute(XNamespace.Xml + "lang") ?? "";

        var collector = new VerseCollector();
        (int Book, int Chapter, int Verse)? open = null; // the milestone verse we're inside, if any

        foreach (var node in root.DescendantNodes())
        {
            if (node is XElement element && element.Name.LocalName == "verse")
            {
                if (element.Attribute("eID") is not null) { open = null; continue; }

                var id = ParseOsisId((string?)element.Attribute("osisID"));
                if (id is null) continue;
                collector.Set(id.Value.Book, id.Value.Chapter, id.Value.Verse, "");

                // A milestone start, or an empty <verse/>: the text follows it.
                if (element.Attribute("sID") is not null || !element.Nodes().Any()) open = id;
                continue;
            }

            if (node is not XText text || IsInsideNoteOrHeading(text)) continue;

            // Text belongs to the container <verse> around it, or to the open milestone verse.
            var container = text.Ancestors().FirstOrDefault(a =>
                a.Name.LocalName == "verse" && a.Attribute("osisID") is not null && a.Attribute("sID") is null);
            var target = container is not null ? ParseOsisId((string?)container.Attribute("osisID")) : open;
            if (target is null) continue;

            collector.Append(target.Value.Book, target.Value.Chapter, target.Value.Verse, text.Value);
        }

        return collector.Build(abbrev.Trim(), string.IsNullOrWhiteSpace(name) ? abbrev.Trim() : name.Trim(),
            language, rights.Trim(), new Dictionary<int, string>());
    }

    /// <summary>"John.3.16" (or "John.3.16 John.3.17" for joined verses) → (43, 3, 16).</summary>
    private static (int Book, int Chapter, int Verse)? ParseOsisId(string? osisId)
    {
        if (string.IsNullOrWhiteSpace(osisId)) return null;
        var parts = osisId.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0].Split('.');
        if (parts.Length < 3) return null;

        var book = Array.FindIndex(OsisBookCodes, code => string.Equals(code, parts[0], StringComparison.OrdinalIgnoreCase));
        if (book < 0 || !int.TryParse(parts[1], out var chapter) || !int.TryParse(parts[2], out var verse)) return null;
        return (book + 1, chapter, verse);
    }

    /// <summary>Footnotes and section headings are not part of the verse text (canonical psalm titles are).</summary>
    private static bool IsInsideNoteOrHeading(XText text) =>
        text.Ancestors().Any(a =>
            a.Name.LocalName == "note" ||
            (a.Name.LocalName == "title" && (string?)a.Attribute("canonical") != "true"));

    // ---------- shared helpers ----------

    /// <summary>Verse text with the named child elements (footnotes etc.) removed and spaces tidied.</summary>
    private static string VerseText(XElement verse, params string[] skip)
    {
        var copy = new XElement(verse);
        copy.Descendants().Where(e => skip.Contains(e.Name.LocalName)).ToList().ForEach(e => e.Remove());
        return Tidy(copy.Value);
    }

    private static string Tidy(string text) =>
        string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Gathers verses in any order, then builds the book/chapter/verse lists.</summary>
    private sealed class VerseCollector
    {
        private readonly SortedDictionary<int, SortedDictionary<int, SortedDictionary<int, StringBuilder>>> _books = new();

        public void Set(int book, int chapter, int verse, string text)
        {
            var sb = Get(book, chapter, verse);
            sb.Clear();
            sb.Append(text);
        }

        public void Append(int book, int chapter, int verse, string text) => Get(book, chapter, verse).Append(text);

        private StringBuilder Get(int book, int chapter, int verse)
        {
            if (!_books.TryGetValue(book, out var chapters)) _books[book] = chapters = new();
            if (!chapters.TryGetValue(chapter, out var verses)) chapters[chapter] = verses = new();
            if (!verses.TryGetValue(verse, out var sb)) verses[verse] = sb = new StringBuilder();
            return sb;
        }

        public BibleTranslation Build(string abbreviation, string name, string language, string copyright, IReadOnlyDictionary<int, string> bookNames)
        {
            if (_books.Count == 0) throw new InvalidDataException("No verses were found in this file.");

            var books = _books.Select(pair => new BibleBook
            {
                Number = pair.Key,
                Name = bookNames.TryGetValue(pair.Key, out var n) ? n : BookNames.EnglishName(pair.Key),
                Chapters = Densify(pair.Value),
            }).ToList();

            return new BibleTranslation
            {
                Abbreviation = abbreviation,
                Name = name,
                Language = language.Trim(),
                Copyright = Tidy(copyright),
                Books = books,
            };
        }

        /// <summary>Turns numbered chapters/verses into lists, filling any gaps with empty text.</summary>
        private static IReadOnlyList<IReadOnlyList<string>> Densify(SortedDictionary<int, SortedDictionary<int, StringBuilder>> chapters)
        {
            var maxChapter = chapters.Keys.Max();
            var result = new List<IReadOnlyList<string>>(maxChapter);
            for (var c = 1; c <= maxChapter; c++)
            {
                if (!chapters.TryGetValue(c, out var verses) || verses.Count == 0) { result.Add(Array.Empty<string>()); continue; }
                var list = new string[verses.Keys.Max()];
                for (var v = 1; v <= list.Length; v++) list[v - 1] = verses.TryGetValue(v, out var sb) ? Tidy(sb.ToString()) : "";
                result.Add(list);
            }
            return result;
        }
    }
}
