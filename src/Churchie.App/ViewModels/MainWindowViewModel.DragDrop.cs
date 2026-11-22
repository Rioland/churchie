using Churchie.Core.Bible;
using Churchie.Core.Models;
using Churchie.Core.Services;

namespace Churchie.App.ViewModels;

// Drag and drop: what happens when something is dropped somewhere.
// The window (Views/MainWindow.DragDrop.cs) does the mouse work — noticing a drag,
// showing where it will land — and then calls one of these methods.
//
// What can be dragged where:
//   a schedule item            → another place in the schedule, the Preview panel, the Live panel
//   a song, picture/video, or the Bible passage on the Scripture tab
//                              → the schedule (at the spot it's dropped), Preview, or Live
//   a preview slide            → the Live panel (goes live at that slide)
//   files from Finder/Explorer → the schedule or the Media tab (pictures and videos),
//                                the Songs list (lyrics files)
public partial class MainWindowViewModel
{
    /// <summary>
    /// Turns something dragged from one of the lists into a schedule item, or null if it
    /// can't become one (a slide thumbnail, for example).
    /// </summary>
    private ScheduleItem? ItemFor(object dragged) => dragged switch
    {
        ScheduleEntryViewModel entry => entry.Item,
        Song song => ScheduleItem.ForSong(song),
        MediaItemViewModel media => ScheduleItem.ForMedia(media.FilePath),
        Verse => _passage is null ? null : ScheduleItem.ForScripture(_passage.Bible.Abbreviation, _passage.Title),
        _ => null,
    };

    public bool CanDropInSchedule(object dragged) => ItemFor(dragged) is not null;

    public bool CanDropOnPreview(object dragged) => ItemFor(dragged) is not null;

    public bool CanDropOnLive(object dragged) => dragged is SlideItemViewModel || ItemFor(dragged) is not null;

    /// <summary>Something dropped on the schedule.</summary>
    /// <param name="dropBefore">The gap it landed in: 0 = above the first item, Count = below the last.</param>
    public void DropInSchedule(object dragged, int dropBefore)
    {
        // A schedule item dragged to another place: move it.
        if (dragged is ScheduleEntryViewModel entry)
        {
            var from = ScheduleEntries.IndexOf(entry);
            if (ListReorder.MoveTarget(from, dropBefore, ScheduleEntries.Count) is not { } to) return;
            ScheduleEntries.Move(from, to);
            SelectedScheduleEntry = entry;
            ScheduleChanged();
            StatusMessage = $"Moved \"{entry.Title}\"";
            return;
        }

        // Anything else: add a new item at that spot.
        if (ItemFor(dragged) is { } item) AddToSchedule(item, dropBefore);
    }

    /// <summary>Something dropped on the Preview panel: show it there.</summary>
    public void DropOnPreview(object dragged)
    {
        if (dragged is ScheduleEntryViewModel entry)
        {
            // Selecting it keeps the schedule and the preview in step (and Next/Prev working).
            if (SelectedScheduleEntry == entry) ShowInPreview(entry.Item);
            else SelectedScheduleEntry = entry;
        }
        else if (ItemFor(dragged) is { } item)
        {
            ShowInPreview(item);
        }
    }

    /// <summary>Something dropped on the Live panel: send it live.</summary>
    public void DropOnLive(object dragged)
    {
        switch (dragged)
        {
            case SlideItemViewModel slide when PreviewSlides.Contains(slide):
                GoLiveAt(slide); // a preview slide: go live at exactly that slide
                break;
            case ScheduleEntryViewModel entry:
                GoLiveWith(entry);
                break;
            default:
                if (ItemFor(dragged) is { } item) SendLive(item, 0);
                break;
        }
    }

    /// <summary>Files from the computer dropped on the schedule: pictures, videos and lyrics files.</summary>
    /// <param name="dropBefore">The gap they landed in (see <see cref="DropInSchedule"/>).</param>
    public async Task DropFilesInScheduleAsync(IReadOnlyList<string> paths, int dropBefore)
    {
        var position = dropBefore;

        // Pictures and videos also join the Media tab, so they can be used again later.
        foreach (var file in AddMediaFiles(paths))
            AddToSchedule(ScheduleItem.ForMedia(file), position++);

        // Lyrics files become songs in the library and in the schedule.
        var lyrics = paths.Where(SongImporter.CanImport).ToList();
        if (lyrics.Count > 0)
        {
            var (songs, failed) = ImportSongFiles(lyrics);
            foreach (var song in songs)
                AddToSchedule(ScheduleItem.ForSong(song), position++);
            await ReportFailedImportsAsync(failed);
        }

        ReportSkippedFiles(paths, f => MediaFiles.KindOf(f) != MediaKind.None || SongImporter.CanImport(f));
    }

    /// <summary>Files from the computer dropped on the Media tab: pictures and videos are added.</summary>
    public void DropFilesInMedia(IReadOnlyList<string> paths)
    {
        var added = AddMediaFiles(paths);
        if (added.Count > 0) StatusMessage = $"Added {added.Count} file{(added.Count == 1 ? "" : "s")} to Media";
        ReportSkippedFiles(paths, f => MediaFiles.KindOf(f) != MediaKind.None);
    }

    /// <summary>Files from the computer dropped on the Songs list: lyrics files are imported.</summary>
    public async Task DropFilesInSongsAsync(IReadOnlyList<string> paths)
    {
        var lyrics = paths.Where(SongImporter.CanImport).ToList();
        if (lyrics.Count > 0)
        {
            var (songs, failed) = ImportSongFiles(lyrics);
            StatusMessage = $"Imported {songs.Count} song{(songs.Count == 1 ? "" : "s")}";
            await ReportFailedImportsAsync(failed);
        }
        ReportSkippedFiles(paths, SongImporter.CanImport);
    }

    /// <summary>Says in the status bar which dropped files were left out, if any.</summary>
    private void ReportSkippedFiles(IReadOnlyList<string> paths, Func<string, bool> accepted)
    {
        var skipped = paths.Where(p => !accepted(p)).Select(p => Path.GetFileName(p)).ToList();
        if (skipped.Count > 0)
            StatusMessage = "Skipped (not a picture, video or lyrics file): " + string.Join(", ", skipped);
    }

    private async Task ReportFailedImportsAsync(IReadOnlyList<string> failed)
    {
        if (failed.Count > 0 && Dialogs is not null)
            await Dialogs.ShowMessageAsync("Import songs", "Some files couldn't be imported:\n" + string.Join("\n", failed));
    }
}
