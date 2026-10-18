using System.Globalization;
using System.Text;

namespace Churchie.Core.Bible;

/// <summary>
/// The 66 books in standard order, plus common abbreviations.
/// Used to understand what people type ("1 Cor 13", "Jn 3:16", "Ps 23").
/// </summary>
public static class BookNames
{
    public static readonly IReadOnlyList<string> English = new[]
    {
        "Genesis", "Exodus", "Leviticus", "Numbers", "Deuteronomy", "Joshua", "Judges", "Ruth",
        "1 Samuel", "2 Samuel", "1 Kings", "2 Kings", "1 Chronicles", "2 Chronicles", "Ezra",
        "Nehemiah", "Esther", "Job", "Psalms", "Proverbs", "Ecclesiastes", "Song of Solomon",
        "Isaiah", "Jeremiah", "Lamentations", "Ezekiel", "Daniel", "Hosea", "Joel", "Amos",
        "Obadiah", "Jonah", "Micah", "Nahum", "Habakkuk", "Zephaniah", "Haggai", "Zechariah",
        "Malachi", "Matthew", "Mark", "Luke", "John", "Acts", "Romans", "1 Corinthians",
        "2 Corinthians", "Galatians", "Ephesians", "Philippians", "Colossians", "1 Thessalonians",
        "2 Thessalonians", "1 Timothy", "2 Timothy", "Titus", "Philemon", "Hebrews", "James",
        "1 Peter", "2 Peter", "1 John", "2 John", "3 John", "Jude", "Revelation",
    };

    /// <summary>Abbreviations that are NOT simply the start of the book's name.</summary>
    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["jn"] = "john", ["jhn"] = "john",
        ["mt"] = "matthew", ["mk"] = "mark", ["mrk"] = "mark", ["lk"] = "luke",
        ["jdg"] = "judges", ["jdgs"] = "judges",
        ["dt"] = "deuteronomy", ["nm"] = "numbers",
        ["ps"] = "psalms", ["psa"] = "psalms", ["pss"] = "psalms", ["psalm"] = "psalms",
        ["sos"] = "songofsolomon", ["sng"] = "songofsolomon", ["songofsongs"] = "songofsolomon",
        ["canticles"] = "songofsolomon",
        ["ezk"] = "ezekiel", ["jl"] = "joel", ["zp"] = "zephaniah", ["zc"] = "zechariah",
        ["phm"] = "philemon", ["phlm"] = "philemon",
        ["jas"] = "james", ["jm"] = "james",
        ["rv"] = "revelation", ["revelations"] = "revelation", ["revelationofjohn"] = "revelation",
    };

    private sealed record Entry(int Number, int Prefix, string Key);

    // e.g. "1 Samuel" -> (Number 9, Prefix 1, Key "samuel")
    private static readonly IReadOnlyList<Entry> Entries = English
        .Select((name, i) =>
        {
            var prefix = char.IsDigit(name[0]) ? name[0] - '0' : 0;
            var rest = prefix > 0 ? name[2..] : name;
            return new Entry(i + 1, prefix, Normalize(rest));
        })
        .ToList();

    public static string EnglishName(int number) =>
        number >= 1 && number <= English.Count ? English[number - 1] : $"Book {number}";

    /// <summary>
    /// Works out which book was meant. <paramref name="prefix"/> is the leading
    /// number (1, 2, 3) or 0. Returns the book number, or null if nothing matches.
    /// </summary>
    public static int? Resolve(int prefix, string bookText, BibleTranslation? bible = null)
    {
        var key = Normalize(bookText);
        if (key.Length == 0) return null;

        var aliasKey = Aliases.TryGetValue(key, out var alias) ? alias : key;

        // 1. Exact match on the English name wins (so "Jude" never becomes "Judges").
        var exact = Entries.FirstOrDefault(e => e.Prefix == prefix && e.Key == aliasKey);
        if (exact is not null) return exact.Number;

        // 2. Otherwise the first book (in Bible order) whose name starts with what was typed.
        var english = Entries.FirstOrDefault(e => e.Prefix == prefix && e.Key.StartsWith(aliasKey, StringComparison.Ordinal));
        if (english is not null) return english.Number;

        // 3. Finally try the translation's own book names (e.g. Yoruba, Hausa, Igbo).
        if (bible is not null)
        {
            var typed = (prefix > 0 ? prefix.ToString(CultureInfo.InvariantCulture) : "") + key;
            var local = bible.Books.FirstOrDefault(b => Normalize(b.Name, keepDigits: true).StartsWith(typed, StringComparison.Ordinal));
            if (local is not null) return local.Number;
        }

        return null;
    }

    /// <summary>Lower-case, no spaces/punctuation, no accents: "Ìwé Òwe" -> "iweowe".</summary>
    public static string Normalize(string text, bool keepDigits = false)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetter(c) || (keepDigits && char.IsDigit(c)))
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
