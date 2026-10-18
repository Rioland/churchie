using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO.Compression;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Churchie.App.Services;
using Churchie.Core.Bible;
using Churchie.Core.Models;
using Churchie.Core.Services;

namespace Churchie.App.ViewModels;

/// <summary>
/// The brain of the main window. It is split over several files ("partial class"):
///   MainWindowViewModel.cs           — startup, preview, live output, schedule
///   MainWindowViewModel.Songs.cs     — the Songs tab
///   MainWindowViewModel.Scripture.cs — the Scripture tab
///   MainWindowViewModel.Media.cs     — the Media tab
///   MainWindowViewModel.Settings.cs  — themes and settings
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppPaths _paths;
    private readonly SongLibrary _songs;
    private readonly BibleLibrary _bibles = new();
    private readonly SlideFactory _slideFactory;
    private readonly AppSettings _settings;

    private bool _loading = true;
    private string? _schedulePath;
    private ScheduleItem? _previewItem;
    private ScheduleItem? _liveItem;

    /// <summary>Set by the app after the main window exists.</summary>
    public IDialogService? Dialogs { get; set; }

    public MainWindowViewModel(AppPaths paths)
    {
        _paths = paths;
        _paths.EnsureCreated();
        _settings = SettingsStore.Load(paths.SettingsFile);

        _songs = new SongLibrary(paths.Songs);
        _songs.Load();
        _slideFactory = new SlideFactory(_bibles, id => _songs.Find(id));

        SongThemeEditor = new ThemeEditorViewModel(_settings.SongTheme, PickImageAsync);
        ScriptureThemeEditor = new ThemeEditorViewModel(_settings.ScriptureTheme, PickImageAsync);
        SongThemeEditor.Changed += OnThemeEdited;
        ScriptureThemeEditor.Changed += OnThemeEdited;

        LoadSettingsIntoUi();
        RefreshSongList();
        foreach (var file in _settings.MediaFiles.Where(File.Exists))
            MediaItems.Add(new MediaItemViewModel(file));
        RestoreAutosavedSchedule();

        _loading = false;
        UpdateOutput();
        _ = LoadBiblesAsync();
    }

    public string WindowTitle =>
        _schedulePath is null ? $"Churchie — {ScheduleName}" : $"Churchie — {ScheduleName} ({Path.GetFileName(_schedulePath)})";

    [ObservableProperty] private string _statusMessage = "Welcome to Churchie";

    // ------------------------------------------------------------------
    // Preview: what the operator is looking at before sending it live
    // ------------------------------------------------------------------

    public ObservableCollection<SlideItemViewModel> PreviewSlides { get; } = new();

    [ObservableProperty] private SlideItemViewModel? _selectedPreviewSlide;
    [ObservableProperty] private string _previewTitle = "Nothing selected";

    private void ShowInPreview(ScheduleItem item)
    {
        _previewItem = item;
        PreviewTitle = item.Title;
        FillSlides(PreviewSlides, item);
        SelectedPreviewSlide = PreviewSlides.FirstOrDefault();
    }

    private void FillSlides(ObservableCollection<SlideItemViewModel> target, ScheduleItem item)
    {
        target.Clear();
        var look = SlideFactory.ThemeFor(item.Kind, _settings).Clone();
        var number = 0;
        foreach (var slide in _slideFactory.Build(item, _settings))
            target.Add(new SlideItemViewModel(slide, ++number, look));
    }

    // ------------------------------------------------------------------
    // Live: what the congregation sees
    // ------------------------------------------------------------------

    public ObservableCollection<SlideItemViewModel> LiveSlides { get; } = new();

    [ObservableProperty] private SlideItemViewModel? _selectedLiveSlide;
    [ObservableProperty] private string _liveTitle = "Nothing live";
    [ObservableProperty] private bool _isBlack;
    [ObservableProperty] private bool _isCleared;
    [ObservableProperty] private bool _isLogo;

    /// <summary>The slide on the projector right now (after Black/Clear/Logo are applied).</summary>
    [ObservableProperty] private Slide? _outputSlide;
    [ObservableProperty] private SlideTheme _outputAppearance = SlideTheme.Black();

    partial void OnSelectedLiveSlideChanged(SlideItemViewModel? value) => UpdateOutput();
    partial void OnIsBlackChanged(bool value) => UpdateOutput();
    partial void OnIsClearedChanged(bool value) => UpdateOutput();
    partial void OnIsLogoChanged(bool value) => UpdateOutput();

    private void UpdateOutput()
    {
        if (_loading) return;

        if (IsBlack || _liveItem is null && !IsLogo)
        {
            OutputAppearance = SlideTheme.Black();
            OutputSlide = null;
            return;
        }

        if (IsLogo)
        {
            OutputAppearance = SlideTheme.Black();
            OutputSlide = string.IsNullOrEmpty(_settings.LogoPath) ? null : SlideBuilder.FromImage(_settings.LogoPath);
            return;
        }

        OutputAppearance = SlideFactory.ThemeFor(_liveItem!.Kind, _settings).Clone();
        var current = SelectedLiveSlide?.Slide;
        if (current is not null && IsCleared && current.ImagePath is null)
            current = current with { Text = "", Footer = null };
        OutputSlide = current;
    }

    /// <summary>Sends an item to the projector, starting at a given slide.</summary>
    public void SendLive(ScheduleItem item, int startIndex)
    {
        _liveItem = item;
        LiveTitle = item.Title;
        _loading = true; // avoid redrawing the output four times
        IsBlack = false;
        IsCleared = false;
        IsLogo = false;
        FillSlides(LiveSlides, item);
        SelectedLiveSlide = LiveSlides.Count == 0 ? null : LiveSlides[Math.Clamp(startIndex, 0, LiveSlides.Count - 1)];
        _loading = false;
        UpdateOutput();
    }

    /// <summary>Called when a preview thumbnail is double-clicked.</summary>
    public void GoLiveAt(SlideItemViewModel slide)
    {
        if (_previewItem is null) return;
        SendLive(_previewItem, PreviewSlides.IndexOf(slide));
    }

    [RelayCommand]
    private void GoLive()
    {
        if (_previewItem is null) return;
        var index = SelectedPreviewSlide is null ? 0 : PreviewSlides.IndexOf(SelectedPreviewSlide);
        SendLive(_previewItem, index);
    }

    [RelayCommand]
    private void NextSlide() => MoveLive(+1);

    [RelayCommand]
    private void PreviousSlide() => MoveLive(-1);

    private void MoveLive(int step)
    {
        if (LiveSlides.Count == 0) return;
        var index = SelectedLiveSlide is null ? -1 : LiveSlides.IndexOf(SelectedLiveSlide);
        SelectedLiveSlide = LiveSlides[Math.Clamp(index + step, 0, LiveSlides.Count - 1)];
    }

    [RelayCommand]
    private void ClearLive()
    {
        _liveItem = null;
        LiveTitle = "Nothing live";
        LiveSlides.Clear();
        SelectedLiveSlide = null;
        UpdateOutput();
    }

    // ------------------------------------------------------------------
    // Schedule (service order)
    // ------------------------------------------------------------------

    public ObservableCollection<ScheduleEntryViewModel> ScheduleEntries { get; } = new();

    [ObservableProperty] private ScheduleEntryViewModel? _selectedScheduleEntry;
    [ObservableProperty] private string _scheduleName = "Sunday Service";

    partial void OnSelectedScheduleEntryChanged(ScheduleEntryViewModel? value)
    {
        if (value is not null) ShowInPreview(value.Item);
    }

    partial void OnScheduleNameChanged(string value)
    {
        if (!_loading) ScheduleChanged();
    }

    private void AddToSchedule(ScheduleItem item)
    {
        var entry = new ScheduleEntryViewModel(item);
        ScheduleEntries.Add(entry);
        SelectedScheduleEntry = entry;
        ScheduleChanged();
        StatusMessage = $"Added \"{item.Title}\" to the schedule";
    }

    /// <summary>Called after any change: autosaves so nothing is lost if the power goes off.</summary>
    private void ScheduleChanged()
    {
        try { ScheduleStore.Save(_paths.AutosaveSchedule, CurrentSchedule()); }
        catch (Exception ex) { StatusMessage = "Autosave failed: " + ex.Message; }
        OnPropertyChanged(nameof(WindowTitle));
    }

    private Schedule CurrentSchedule() => new()
    {
        Name = ScheduleName,
        Items = ScheduleEntries.Select(e => e.Item).ToList(),
    };

    private void LoadSchedule(Schedule schedule, string? path)
    {
        var wasLoading = _loading;
        _loading = true;
        ScheduleEntries.Clear();
        foreach (var item in schedule.Items) ScheduleEntries.Add(new ScheduleEntryViewModel(item));
        ScheduleName = schedule.Name;
        _schedulePath = path;
        _loading = wasLoading;
        OnPropertyChanged(nameof(WindowTitle));
    }

    private void RestoreAutosavedSchedule()
    {
        try
        {
            if (File.Exists(_paths.AutosaveSchedule))
                LoadSchedule(ScheduleStore.Load(_paths.AutosaveSchedule), null);
        }
        catch
        {
            // A damaged autosave is not worth bothering anyone about.
        }
    }

    /// <summary>Double-clicking a schedule item sends it live.</summary>
    public void GoLiveWith(ScheduleEntryViewModel entry) => SendLive(entry.Item, 0);

    [RelayCommand]
    private void MoveUp()
    {
        if (SelectedScheduleEntry is not { } entry) return;
        var i = ScheduleEntries.IndexOf(entry);
        if (i <= 0) return;
        ScheduleEntries.Move(i, i - 1);
        SelectedScheduleEntry = entry;
        ScheduleChanged();
    }

    [RelayCommand]
    private void MoveDown()
    {
        if (SelectedScheduleEntry is not { } entry) return;
        var i = ScheduleEntries.IndexOf(entry);
        if (i < 0 || i >= ScheduleEntries.Count - 1) return;
        ScheduleEntries.Move(i, i + 1);
        SelectedScheduleEntry = entry;
        ScheduleChanged();
    }

    [RelayCommand]
    private void RemoveFromSchedule()
    {
        if (SelectedScheduleEntry is not { } entry) return;
        var i = ScheduleEntries.IndexOf(entry);
        ScheduleEntries.Remove(entry);
        SelectedScheduleEntry = ScheduleEntries.Count == 0 ? null : ScheduleEntries[Math.Min(i, ScheduleEntries.Count - 1)];
        ScheduleChanged();
    }

    [RelayCommand]
    private async Task AddTextItemAsync()
    {
        if (Dialogs is null) return;
        var result = await Dialogs.EditSongAsync(new SongEditorResult("Announcement", "", "", "", ""), "New text slides", showSongFields: false);
        if (result is null) return;
        AddToSchedule(ScheduleItem.ForText(result.Title, result.Lyrics));
    }

    [RelayCommand]
    private async Task EditScheduleEntryAsync()
    {
        if (SelectedScheduleEntry is not { } entry || Dialogs is null) return;
        var item = entry.Item;

        if (item.Kind == ScheduleItemKind.Song && item.Song is not null)
        {
            var song = _songs.Find(item.Song.Id) ?? item.Song.Clone();
            var edited = await EditSongInDialogAsync(song, "Edit song");
            if (edited is null) return;
            item.Song = edited.Clone();
            item.Title = edited.Title;
        }
        else if (item.Kind == ScheduleItemKind.Text)
        {
            var result = await Dialogs.EditSongAsync(new SongEditorResult(item.Title, "", "", "", item.Text ?? ""), "Edit text slides", showSongFields: false);
            if (result is null) return;
            item.Title = result.Title;
            item.Text = result.Lyrics;
        }
        else
        {
            StatusMessage = "Scripture and pictures can't be edited — remove and add them again.";
            return;
        }

        entry.Refresh();
        ScheduleChanged();
        RebuildSlides();
    }

    [RelayCommand]
    private async Task NewScheduleAsync()
    {
        if (ScheduleEntries.Count > 0 && Dialogs is not null &&
            !await Dialogs.ConfirmAsync("New schedule", "Start a new, empty schedule? Unsaved changes to the current one will be lost.", "Start new"))
            return;

        LoadSchedule(new Schedule { Name = $"Service {DateTime.Today:d MMM yyyy}" }, null);
        ScheduleChanged();
    }

    [RelayCommand]
    private async Task OpenScheduleAsync()
    {
        if (Dialogs is null) return;
        var files = await Dialogs.PickFilesAsync("Open schedule", "Churchie schedules", new[] { "*" + ScheduleStore.Extension }, false);
        if (files.Count == 0) return;

        try
        {
            LoadSchedule(ScheduleStore.Load(files[0]), files[0]);
            ScheduleChanged();
            StatusMessage = $"Opened {Path.GetFileName(files[0])}";
        }
        catch (Exception ex)
        {
            await Dialogs.ShowMessageAsync("Couldn't open schedule", ex.Message);
        }
    }

    [RelayCommand]
    private async Task SaveScheduleAsync()
    {
        if (_schedulePath is null)
        {
            await SaveScheduleAsAsync();
            return;
        }
        ScheduleStore.Save(_schedulePath, CurrentSchedule());
        StatusMessage = $"Saved {Path.GetFileName(_schedulePath)}";
    }

    [RelayCommand]
    private async Task SaveScheduleAsAsync()
    {
        if (Dialogs is null) return;
        var path = await Dialogs.PickSaveFileAsync("Save schedule", SafeFileName(ScheduleName) + ScheduleStore.Extension, "Churchie schedule", ScheduleStore.Extension);
        if (path is null) return;

        try
        {
            ScheduleStore.Save(path, CurrentSchedule());
            _schedulePath = path;
            OnPropertyChanged(nameof(WindowTitle));
            StatusMessage = $"Saved {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            await Dialogs.ShowMessageAsync("Couldn't save schedule", ex.Message);
        }
    }

    // ------------------------------------------------------------------
    // Helpers shared by all tabs
    // ------------------------------------------------------------------

    /// <summary>Re-creates preview and live slides (e.g. after a song edit or a settings change).</summary>
    private void RebuildSlides()
    {
        if (_previewItem is not null)
        {
            var i = SelectedPreviewSlide is null ? 0 : PreviewSlides.IndexOf(SelectedPreviewSlide);
            FillSlides(PreviewSlides, _previewItem);
            SelectedPreviewSlide = PreviewSlides.Count == 0 ? null : PreviewSlides[Math.Clamp(i, 0, PreviewSlides.Count - 1)];
        }

        if (_liveItem is not null)
        {
            var i = SelectedLiveSlide is null ? 0 : LiveSlides.IndexOf(SelectedLiveSlide);
            var wasLoading = _loading;
            _loading = true;
            FillSlides(LiveSlides, _liveItem);
            SelectedLiveSlide = LiveSlides.Count == 0 ? null : LiveSlides[Math.Clamp(i, 0, LiveSlides.Count - 1)];
            _loading = wasLoading;
        }

        UpdateOutput();
    }

    private async Task LoadBiblesAsync()
    {
        StatusMessage = "Loading Bibles…";
        try
        {
            var folder = _paths.Bibles;
            var (found, errors) = await Task.Run(() =>
            {
                var list = new List<BibleTranslation>();
                var problems = new List<string>();

                try
                {
                    using var resource = typeof(MainWindowViewModel).Assembly.GetManifestResourceStream("Churchie.Bibles.kjv.json.gz");
                    if (resource is not null)
                    {
                        using var unzipped = new GZipStream(resource, CompressionMode.Decompress);
                        list.Add(BibleLoader.LoadJson(unzipped));
                    }
                }
                catch (Exception ex)
                {
                    problems.Add("KJV: " + ex.Message);
                }

                var fromFolder = new BibleLibrary();
                fromFolder.LoadFolder(folder);
                list.AddRange(fromFolder.Bibles);
                problems.AddRange(fromFolder.LoadErrors);
                return (list, problems);
            });

            foreach (var bible in found) _bibles.Add(bible);

            _loading = true;
            Bibles.Clear();
            foreach (var bible in _bibles.Bibles) Bibles.Add(bible);
            SelectedBible = _bibles.Find(_settings.DefaultBible) ?? _bibles.Bibles.FirstOrDefault();
            _loading = false;

            StatusMessage = errors.Count == 0
                ? $"Ready — {_songs.Songs.Count} songs, {Bibles.Count} Bible{(Bibles.Count == 1 ? "" : "s")}"
                : "Some Bible files could not be loaded: " + string.Join("; ", errors);

            // Scripture items may have been waiting for the Bible to load.
            RebuildSlides();
        }
        catch (Exception ex)
        {
            _loading = false;
            StatusMessage = "Couldn't load Bibles: " + ex.Message;
        }
    }

    private async Task<string?> PickImageAsync()
    {
        if (Dialogs is null) return null;
        var files = await Dialogs.PickFilesAsync("Choose a picture", "Images", ImagePatterns, false);
        return files.Count > 0 ? files[0] : null;
    }

    private static readonly string[] ImagePatterns = { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp" };

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim();
        return clean.Length == 0 ? "schedule" : clean;
    }

    /// <summary>Opens a folder in Explorer / Finder / the Linux file manager.</summary>
    private static void OpenFolder(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var program = OperatingSystem.IsWindows() ? "explorer.exe" : OperatingSystem.IsMacOS() ? "open" : "xdg-open";
            var start = new ProcessStartInfo(program) { UseShellExecute = false };
            start.ArgumentList.Add(path);
            Process.Start(start);
        }
        catch
        {
            // No file manager available (e.g. in a container) — nothing else to do.
        }
    }

    [RelayCommand]
    private void OpenDataFolder() => OpenFolder(_paths.Root);

    [RelayCommand]
    private async Task ShowShortcutsAsync()
    {
        if (Dialogs is null) return;
        await Dialogs.ShowMessageAsync("Keyboard shortcuts",
            "Page Down / Page Up — next / previous slide (works with presentation clickers)\n" +
            "→ / ← or Space — next / previous slide (when the Live panel is focused)\n" +
            "B — black screen\n" +
            "C — clear text (keep background)\n" +
            "L — show logo\n" +
            "Ctrl+Enter — send the preview live\n" +
            "Double-click a schedule item or preview slide — send it live");
    }

    [RelayCommand]
    private async Task ShowAboutAsync()
    {
        if (Dialogs is null) return;
        await Dialogs.ShowMessageAsync("About Churchie",
            "Churchie 0.1 — worship lyrics and scripture presentation.\n\n" +
            "Built with C# and Avalonia; runs on Windows, macOS and Linux.\n" +
            $"Your songs and settings are stored in:\n{_paths.Root}");
    }
}
