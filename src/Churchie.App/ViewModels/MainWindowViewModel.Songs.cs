using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Churchie.App.Services;
using Churchie.Core.Models;
using Churchie.Core.Services;

namespace Churchie.App.ViewModels;

// The Songs tab.
public partial class MainWindowViewModel
{
    public ObservableCollection<Song> FilteredSongs { get; } = new();

    [ObservableProperty] private string _songSearch = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedSongLyrics))]
    [NotifyPropertyChangedFor(nameof(SelectedSongDetails))]
    private Song? _selectedSong;

    public string SongCountText => $"{_songs.Songs.Count} song{(_songs.Songs.Count == 1 ? "" : "s")} in library";

    public string SelectedSongLyrics => SelectedSong is null ? "" : SongParser.ToText(SelectedSong.Sections);

    public string SelectedSongDetails
    {
        get
        {
            if (SelectedSong is null) return "Select a song to see its words.";
            var parts = new[] { SelectedSong.Author, SelectedSong.Copyright, string.IsNullOrEmpty(SelectedSong.CcliNumber) ? "" : "CCLI " + SelectedSong.CcliNumber };
            return string.Join("  ·  ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        }
    }

    partial void OnSongSearchChanged(string value) => RefreshSongList();

    partial void OnSelectedSongChanged(Song? value)
    {
        if (value is not null && !_loading) ShowInPreview(ScheduleItem.ForSong(value));
    }

    private void RefreshSongList()
    {
        var keep = SelectedSong?.Id;
        FilteredSongs.Clear();
        foreach (var song in _songs.Search(SongSearch)) FilteredSongs.Add(song);
        SelectedSong = FilteredSongs.FirstOrDefault(s => s.Id == keep);
        OnPropertyChanged(nameof(SongCountText));
    }

    [RelayCommand]
    private async Task NewSongAsync()
    {
        var song = await EditSongInDialogAsync(new Song(), "New song");
        if (song is not null) SelectedSong = FilteredSongs.FirstOrDefault(s => s.Id == song.Id);
    }

    [RelayCommand]
    private async Task EditSongAsync()
    {
        if (SelectedSong is null) return;
        await EditSongInDialogAsync(SelectedSong, "Edit song");
    }

    [RelayCommand]
    private async Task DeleteSongAsync()
    {
        if (SelectedSong is not { } song || Dialogs is null) return;
        if (!await Dialogs.ConfirmAsync("Delete song", $"Delete \"{song.Title}\" from the library? This can't be undone.", "Delete"))
            return;

        _songs.Delete(song.Id);
        RefreshSongList();
        StatusMessage = $"Deleted \"{song.Title}\"";
    }

    [RelayCommand]
    private void AddSongToSchedule()
    {
        if (SelectedSong is not null) AddToSchedule(ScheduleItem.ForSong(SelectedSong));
    }

    /// <summary>Double-clicking a song adds it to the schedule.</summary>
    public void AddSongToScheduleFromList(Song song) => AddToSchedule(ScheduleItem.ForSong(song));

    [RelayCommand]
    private async Task ImportSongsAsync()
    {
        if (Dialogs is null) return;
        var files = await Dialogs.PickFilesAsync("Import songs", "Lyrics (.txt) or OpenLyrics (.xml)", new[] { "*.txt", "*.xml" }, true);
        if (files.Count == 0) return;

        var imported = 0;
        var failed = new List<string>();
        foreach (var file in files)
        {
            try
            {
                var song = SongImporter.ImportFile(file);
                if (song.Sections.Count == 0) { failed.Add($"{Path.GetFileName(file)} (no lyrics found)"); continue; }
                _songs.Save(song);
                imported++;
            }
            catch (Exception ex)
            {
                failed.Add($"{Path.GetFileName(file)} ({ex.Message})");
            }
        }

        RefreshSongList();
        var message = $"Imported {imported} song{(imported == 1 ? "" : "s")}.";
        if (failed.Count > 0) message += "\n\nSkipped:\n" + string.Join("\n", failed);
        await Dialogs.ShowMessageAsync("Import songs", message);
    }

    /// <summary>Opens the editor for a song, saves it to the library and refreshes everything that shows it.</summary>
    private async Task<Song?> EditSongInDialogAsync(Song song, string windowTitle)
    {
        if (Dialogs is null) return null;

        var initial = new SongEditorResult(song.Title, song.Author, song.Copyright, song.CcliNumber, SongParser.ToText(song.Sections));
        var result = await Dialogs.EditSongAsync(initial, windowTitle, showSongFields: true);
        if (result is null) return null;

        song.Title = result.Title;
        song.Author = result.Author;
        song.Copyright = result.Copyright;
        song.CcliNumber = result.Ccli;
        song.Sections = SongParser.Parse(result.Lyrics);
        _songs.Save(song);

        RefreshSongList();
        OnPropertyChanged(nameof(SelectedSongLyrics));
        OnPropertyChanged(nameof(SelectedSongDetails));
        RebuildSlides();
        StatusMessage = $"Saved \"{song.Title}\"";
        return song;
    }
}
