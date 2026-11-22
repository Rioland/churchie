using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Churchie.Core.Models;
using Churchie.Core.Services;

namespace Churchie.App.ViewModels;

// The Media tab: pictures, animated GIFs and videos for slides, backgrounds and the logo.
public partial class MainWindowViewModel
{
    public ObservableCollection<MediaItemViewModel> MediaItems { get; } = new();

    [ObservableProperty] private MediaItemViewModel? _selectedMedia;

    public string LogoText => string.IsNullOrEmpty(_settings.LogoPath) ? "Logo: none (choose a picture or video and press \"Use as logo\")" : "Logo: " + Path.GetFileName(_settings.LogoPath);

    partial void OnSelectedMediaChanged(MediaItemViewModel? value)
    {
        if (value is not null && !_loading) ShowInPreview(MediaItem(value.FilePath));
    }

    [RelayCommand]
    private async Task AddMediaAsync()
    {
        if (Dialogs is null) return;
        var files = await Dialogs.PickFilesAsync("Add pictures, GIFs and videos", "Pictures and videos", MediaFiles.AllPatterns, true);
        AddMediaFiles(files);

        // "All files" is offered in the dialog too, so mention anything we can't show.
        var skipped = files.Where(f => MediaFiles.KindOf(f) == MediaKind.None).Select(f => Path.GetFileName(f)).ToList();
        if (skipped.Count > 0)
            StatusMessage = "Not a picture or video Churchie can show: " + string.Join(", ", skipped);
    }

    /// <summary>
    /// Adds pictures and videos to the Media tab (files already there are kept as they are).
    /// Used by the "Add" button and by files dragged in from the computer.
    /// </summary>
    /// <returns>Every picture or video among <paramref name="files"/>, including ones that were already listed.</returns>
    private List<string> AddMediaFiles(IEnumerable<string> files)
    {
        var media = files.Where(f => MediaFiles.KindOf(f) != MediaKind.None).ToList();
        foreach (var file in media)
        {
            if (_settings.MediaFiles.Contains(file, StringComparer.OrdinalIgnoreCase)) continue;
            _settings.MediaFiles.Add(file);
            MediaItems.Add(new MediaItemViewModel(file, MediaFiles.DisplayName(file, _settings.MediaNames)));
        }
        if (media.Count > 0) SaveSettings();
        return media;
    }

    /// <summary>A schedule item for a media file, titled with the name it has on the Media tab.</summary>
    private ScheduleItem MediaItem(string path)
    {
        var item = ScheduleItem.ForMedia(path);
        item.Title = MediaFiles.DisplayName(path, _settings.MediaNames);
        return item;
    }

    /// <summary>
    /// Gives the selected picture or video a new name in Churchie. The file on the computer
    /// keeps its own name: it may be used by other programs, or sit in a folder like Downloads.
    /// </summary>
    [RelayCommand]
    private async Task RenameMediaAsync()
    {
        if (SelectedMedia is not { } media || Dialogs is null) return;
        var typed = await Dialogs.AskTextAsync("Rename", "New name for this picture or video:", media.Name);
        if (typed is null) return; // cancelled

        var oldName = media.Name;
        var fileName = Path.GetFileName(media.FilePath);
        var newName = Naming.Clean(typed) ?? fileName; // an empty name goes back to the file name

        if (newName == fileName) _settings.MediaNames.Remove(media.FilePath);
        else _settings.MediaNames[media.FilePath] = newName;
        SaveSettings();
        media.Name = newName;

        // Schedule items that still show the old name follow the new one.
        var renamedInSchedule = false;
        foreach (var entry in ScheduleEntries)
        {
            if (entry.Item.Kind != ScheduleItemKind.Media || entry.Item.MediaPath != media.FilePath || entry.Title != oldName) continue;
            entry.Item.Title = newName;
            entry.Refresh();
            renamedInSchedule = true;
        }
        if (renamedInSchedule) ScheduleChanged();
        if (_previewItem is { Kind: ScheduleItemKind.Media } preview && preview.MediaPath == media.FilePath) PreviewTitle = newName;

        StatusMessage = $"Renamed to \"{newName}\"";
    }

    [RelayCommand]
    private void RemoveMedia()
    {
        if (SelectedMedia is not { } media) return;
        _settings.MediaFiles.RemoveAll(f => string.Equals(f, media.FilePath, StringComparison.OrdinalIgnoreCase));
        MediaItems.Remove(media);
        SaveSettings();
    }

    [RelayCommand]
    private void UseAsSongBackground()
    {
        if (SelectedMedia is not null) SongThemeEditor.SetBackgroundImage(SelectedMedia.FilePath);
    }

    [RelayCommand]
    private void UseAsScriptureBackground()
    {
        if (SelectedMedia is not null) ScriptureThemeEditor.SetBackgroundImage(SelectedMedia.FilePath);
    }

    [RelayCommand]
    private void UseAsLogo()
    {
        if (SelectedMedia is null) return;
        _settings.LogoPath = SelectedMedia.FilePath;
        SaveSettings();
        OnPropertyChanged(nameof(LogoText));
        UpdateOutput();
    }

    [RelayCommand]
    private void ShowMediaLive()
    {
        if (SelectedMedia is not null) SendLive(MediaItem(SelectedMedia.FilePath), 0);
    }

    [RelayCommand]
    private void AddMediaToSchedule()
    {
        if (SelectedMedia is not null) AddToSchedule(MediaItem(SelectedMedia.FilePath));
    }
}
