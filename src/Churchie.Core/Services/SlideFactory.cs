using Churchie.Core.Bible;
using Churchie.Core.Models;

namespace Churchie.Core.Services;

/// <summary>Builds the slides for any schedule item, using the current settings.</summary>
public sealed class SlideFactory
{
    private readonly BibleLibrary _bibles;
    private readonly Func<Guid, Song?> _findSong;

    /// <param name="findSong">Looks a song up in the library so edits show up in the schedule.</param>
    public SlideFactory(BibleLibrary bibles, Func<Guid, Song?> findSong)
    {
        _bibles = bibles;
        _findSong = findSong;
    }

    public IReadOnlyList<Slide> Build(ScheduleItem item, AppSettings settings)
    {
        switch (item.Kind)
        {
            case ScheduleItemKind.Song when item.Song is not null:
                // Prefer the library copy (it may have been edited), fall back to the saved copy.
                var song = _findSong(item.Song.Id) ?? item.Song;
                return SlideBuilder.FromSong(song, settings.MaxLinesPerSlide);

            case ScheduleItemKind.Scripture:
                var passage = _bibles.Lookup(item.BibleAbbreviation, item.Reference, out var error);
                return passage is null
                    ? new[] { new Slide(SlideKind.Text, "Error", error ?? "Passage not found") }
                    : SlideBuilder.FromPassage(passage, settings.VersesPerSlide, settings.ShowVerseNumbers);

            case ScheduleItemKind.Text:
                return SlideBuilder.FromText(item.Text);

            case ScheduleItemKind.Media when !string.IsNullOrEmpty(item.MediaPath):
                return new[] { SlideBuilder.FromImage(item.MediaPath) };

            default:
                return Array.Empty<Slide>();
        }
    }

    /// <summary>Which theme an item should be shown with.</summary>
    public static SlideTheme ThemeFor(ScheduleItemKind kind, AppSettings settings) =>
        kind == ScheduleItemKind.Scripture ? settings.ScriptureTheme : settings.SongTheme;
}
