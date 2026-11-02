namespace Churchie.Core.Bible;

/// <summary>All the Bible translations the app knows about.</summary>
public sealed class BibleLibrary
{
    private readonly List<BibleTranslation> _bibles = new();

    public IReadOnlyList<BibleTranslation> Bibles => _bibles;

    /// <summary>Problems found while loading files (shown to the user, never fatal).</summary>
    public List<string> LoadErrors { get; } = new();

    public void Add(BibleTranslation bible)
    {
        _bibles.RemoveAll(b => string.Equals(b.Abbreviation, bible.Abbreviation, StringComparison.OrdinalIgnoreCase));
        _bibles.Add(bible);
    }

    /// <summary>KJV first, then other English Bibles, then other languages — each group by name.</summary>
    public IReadOnlyList<BibleTranslation> InDisplayOrder() => _bibles
        .OrderBy(b => string.Equals(b.Abbreviation, "KJV", StringComparison.OrdinalIgnoreCase) ? 0
                      : b.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
        .ThenBy(b => b.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    public BibleTranslation? Find(string? abbreviation) =>
        _bibles.FirstOrDefault(b => string.Equals(b.Abbreviation, abbreviation, StringComparison.OrdinalIgnoreCase));

    /// <summary>Loads every .json, .json.gz and .xml Bible in a folder.</summary>
    public void LoadFolder(string folder)
    {
        if (!Directory.Exists(folder)) return;

        var files = Directory.EnumerateFiles(folder)
            .Where(f =>
            {
                var n = f.ToLowerInvariant();
                return n.EndsWith(".json") || n.EndsWith(".json.gz") || n.EndsWith(".xml") || n.EndsWith(".xml.gz");
            })
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            try { Add(BibleLoader.LoadFile(file)); }
            catch (Exception ex) { LoadErrors.Add($"{Path.GetFileName(file)}: {ex.Message}"); }
        }
    }

    /// <summary>
    /// Looks up a reference like "John 3:16-18". Returns null and sets
    /// <paramref name="error"/> when the reference is not valid.
    /// </summary>
    public Passage? Lookup(string? abbreviation, string? referenceText, out string? error)
    {
        var bible = Find(abbreviation) ?? _bibles.FirstOrDefault();
        if (bible is null)
        {
            error = "No Bible is loaded";
            return null;
        }

        if (!ScriptureReference.TryParse(referenceText, bible, out var reference, out error) || reference is null)
            return null;

        var book = bible.GetBook(reference.BookNumber)!;
        var chapterVerses = book.Chapters[reference.Chapter - 1];
        var first = reference.StartVerse ?? 1;
        var last = reference.EndVerse ?? (reference.StartVerse is null ? chapterVerses.Count : first);

        // Some modern translations leave out a few verses (e.g. Acts 8:37); skip those.
        var verses = new List<Verse>();
        for (var v = first; v <= last && v <= chapterVerses.Count; v++)
        {
            if (!string.IsNullOrWhiteSpace(chapterVerses[v - 1]))
                verses.Add(new Verse(v, chapterVerses[v - 1]));
        }

        if (verses.Count == 0)
        {
            error = $"{reference.Format(book.Name)} is not included in {bible.Abbreviation}";
            return null;
        }

        error = null;
        return new Passage(bible, book, reference, verses);
    }
}
