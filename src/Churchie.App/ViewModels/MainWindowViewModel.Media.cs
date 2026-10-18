using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Churchie.Core.Models;

namespace Churchie.App.ViewModels;

// The Media tab (pictures for backgrounds, announcements and the logo).
public partial class MainWindowViewModel
{
    public ObservableCollection<MediaItemViewModel> MediaItems { get; } = new();

    [ObservableProperty] private MediaItemViewModel? _selectedMedia;

    public string LogoText => string.IsNullOrEmpty(_settings.LogoPath) ? "Logo: none (choose a picture and press \"Use as logo\")" : "Logo: " + Path.GetFileName(_settings.LogoPath);

    partial void OnSelectedMediaChanged(MediaItemViewModel? value)
    {
        if (value is not null && !_loading) ShowInPreview(ScheduleItem.ForMedia(value.FilePath));
    }

    [RelayCommand]
    private async Task AddMediaAsync()
    {
        if (Dialogs is null) return;
        var files = await Dialogs.PickFilesAsync("Add pictures", "Images", ImagePatterns, true);
        foreach (var file in files)
        {
            if (_settings.MediaFiles.Contains(file, StringComparer.OrdinalIgnoreCase)) continue;
            _settings.MediaFiles.Add(file);
            MediaItems.Add(new MediaItemViewModel(file));
        }
        SaveSettings();
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
        if (SelectedMedia is not null) SendLive(ScheduleItem.ForMedia(SelectedMedia.FilePath), 0);
    }

    [RelayCommand]
    private void AddMediaToSchedule()
    {
        if (SelectedMedia is not null) AddToSchedule(ScheduleItem.ForMedia(SelectedMedia.FilePath));
    }
}
