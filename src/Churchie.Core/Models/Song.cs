namespace Churchie.Core.Models;

/// <summary>
/// A song in the library. Lyrics are stored as an ordered list of sections
/// (Verse 1, Chorus, Bridge...). Each section usually becomes one slide.
/// </summary>
public sealed class Song
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Copyright { get; set; } = "";
    public string CcliNumber { get; set; } = "";
    public List<SongSection> Sections { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>All lyric text joined together — handy for searching. Not saved to disk.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string AllText => string.Join("\n", Sections.Select(s => s.Text));

    /// <summary>Makes an independent copy (used when a song is put in a schedule).</summary>
    public Song Clone() => new()
    {
        Id = Id,
        Title = Title,
        Author = Author,
        Copyright = Copyright,
        CcliNumber = CcliNumber,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        Sections = Sections.Select(s => new SongSection { Label = s.Label, Text = s.Text }).ToList(),
    };

    public override string ToString() => Title;
}

/// <summary>One labelled block of lyrics, e.g. "Chorus".</summary>
public sealed class SongSection
{
    public string Label { get; set; } = "";
    public string Text { get; set; } = "";
}
