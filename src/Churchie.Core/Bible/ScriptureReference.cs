using System.Text.RegularExpressions;

namespace Churchie.Core.Bible;

/// <summary>
/// A parsed reference such as John 3:16-18.
/// StartVerse/EndVerse are null when the whole chapter is meant.
/// </summary>
public sealed record ScriptureReference(int BookNumber, int Chapter, int? StartVerse = null, int? EndVerse = null)
{
    public string Format(string bookName)
    {
        if (StartVerse is null) return $"{bookName} {Chapter}";
        if (EndVerse is null || EndVerse == StartVerse) return $"{bookName} {Chapter}:{StartVerse}";
        return $"{bookName} {Chapter}:{StartVerse}-{EndVerse}";
    }

    // Accepts: "John 3:16", "jn 3 16", "1 Cor 13:4-7", "I Samuel 3", "Psalm 23", "Rev. 21:4", "Gen"
    private static readonly Regex Pattern = new(
        @"^\s*(?:(?<num>[123])\s*|(?<roman>iii|ii|i|first|second|third)\s+)?" +
        @"(?<book>[^\d\s][^\d]*?)\.?\s*" +
        @"(?:(?<ch>\d+)(?:\s*[:.\s]\s*(?<vs>\d+)(?:\s*[-–—]\s*(?<ve>\d+))?)?)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Turns what the operator typed into a reference. The Bible is optional and is
    /// used to understand book names in other languages and to check the numbers.
    /// </summary>
    public static bool TryParse(string? input, BibleTranslation? bible, out ScriptureReference? reference, out string? error)
    {
        reference = null;
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Type a reference, e.g. John 3:16";
            return false;
        }

        var m = Pattern.Match(input);
        if (!m.Success)
        {
            error = $"Couldn't understand \"{input.Trim()}\". Try something like John 3:16-18";
            return false;
        }

        var prefix = 0;
        if (m.Groups["num"].Success) prefix = m.Groups["num"].Value[0] - '0';
        else if (m.Groups["roman"].Success)
        {
            prefix = m.Groups["roman"].Value.ToLowerInvariant() switch
            {
                "i" or "first" => 1,
                "ii" or "second" => 2,
                _ => 3,
            };
        }

        var bookNumber = BookNames.Resolve(prefix, m.Groups["book"].Value, bible);
        if (bookNumber is null)
        {
            error = $"Unknown book \"{(prefix > 0 ? prefix + " " : "")}{m.Groups["book"].Value.Trim()}\"";
            return false;
        }

        var chapter = m.Groups["ch"].Success ? int.Parse(m.Groups["ch"].Value) : 1;
        int? start = m.Groups["vs"].Success ? int.Parse(m.Groups["vs"].Value) : null;
        int? end = m.Groups["ve"].Success ? int.Parse(m.Groups["ve"].Value) : null;

        if (start is not null && end is not null && end < start)
            (start, end) = (end, start);

        if (bible is not null)
        {
            var book = bible.GetBook(bookNumber.Value);
            if (book is null)
            {
                error = $"{BookNames.EnglishName(bookNumber.Value)} is not in {bible.Abbreviation}";
                return false;
            }
            if (chapter < 1 || chapter > book.ChapterCount)
            {
                error = $"{book.Name} has {book.ChapterCount} chapter{(book.ChapterCount == 1 ? "" : "s")}";
                return false;
            }
            var verseCount = book.VerseCount(chapter);
            if (start is not null && (start < 1 || start > verseCount))
            {
                error = $"{book.Name} {chapter} has {verseCount} verses";
                return false;
            }
            if (end is not null && end > verseCount) end = verseCount;
        }

        reference = new ScriptureReference(bookNumber.Value, chapter, start, end);
        return true;
    }
}
