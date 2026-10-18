namespace Churchie.Core.Models;

public enum ScheduleItemKind
{
    Song,
    Scripture,
    Text,
    Media,
}

/// <summary>
/// One entry in the service order. Only the fields for its <see cref="Kind"/> are used.
/// Songs are stored as a copy so a saved schedule still works on another computer.
/// </summary>
public sealed class ScheduleItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ScheduleItemKind Kind { get; set; }
    public string Title { get; set; } = "";

    // Kind == Song
    public Song? Song { get; set; }

    // Kind == Scripture
    public string? BibleAbbreviation { get; set; }
    public string? Reference { get; set; }

    // Kind == Text  (blank-line separated blocks become slides)
    public string? Text { get; set; }

    // Kind == Media
    public string? MediaPath { get; set; }

    public static ScheduleItem ForSong(Song song) =>
        new() { Kind = ScheduleItemKind.Song, Title = song.Title, Song = song.Clone() };

    public static ScheduleItem ForScripture(string bible, string reference) =>
        new() { Kind = ScheduleItemKind.Scripture, Title = $"{reference} ({bible})", BibleAbbreviation = bible, Reference = reference };

    public static ScheduleItem ForText(string title, string text) =>
        new() { Kind = ScheduleItemKind.Text, Title = title, Text = text };

    public static ScheduleItem ForMedia(string path) =>
        new() { Kind = ScheduleItemKind.Media, Title = Path.GetFileName(path), MediaPath = path };
}

/// <summary>A whole service order that can be saved to a .churchie file.</summary>
public sealed class Schedule
{
    public string Name { get; set; } = "Sunday Service";
    public DateTime Date { get; set; } = DateTime.Today;
    public List<ScheduleItem> Items { get; set; } = new();
}
