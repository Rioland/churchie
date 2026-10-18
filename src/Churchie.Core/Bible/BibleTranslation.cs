namespace Churchie.Core.Bible;

/// <summary>A whole Bible translation (KJV, a Yoruba Bible, ...).</summary>
public sealed class BibleTranslation
{
    public required string Abbreviation { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<BibleBook> Books { get; init; }

    /// <summary>Finds a book by its standard number (1 = Genesis ... 66 = Revelation).</summary>
    public BibleBook? GetBook(int number) => Books.FirstOrDefault(b => b.Number == number);

    public override string ToString() => $"{Abbreviation} — {Name}";
}

public sealed class BibleBook
{
    /// <summary>Standard book number: 1 = Genesis ... 66 = Revelation.</summary>
    public required int Number { get; init; }

    /// <summary>Display name in the translation's own language.</summary>
    public required string Name { get; init; }

    /// <summary>Chapters[chapter - 1][verse - 1] = verse text.</summary>
    public required IReadOnlyList<IReadOnlyList<string>> Chapters { get; init; }

    public int ChapterCount => Chapters.Count;

    public int VerseCount(int chapter) =>
        chapter >= 1 && chapter <= Chapters.Count ? Chapters[chapter - 1].Count : 0;

    public override string ToString() => Name;
}

/// <summary>A single verse with its number.</summary>
public readonly record struct Verse(int Number, string Text);

/// <summary>The verses found for a reference, plus everything needed to label them.</summary>
public sealed record Passage(
    BibleTranslation Bible,
    BibleBook Book,
    ScriptureReference Reference,
    IReadOnlyList<Verse> Verses)
{
    /// <summary>e.g. "John 3:16-18"</summary>
    public string Title => Reference.Format(Book.Name);
}
