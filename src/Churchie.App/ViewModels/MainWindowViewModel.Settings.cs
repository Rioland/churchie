using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Churchie.Core.Models;
using Churchie.Core.Services;

namespace Churchie.App.ViewModels;

// Themes & Settings tab.
public partial class MainWindowViewModel
{
    private static readonly string[] LinesOptions = { "Don't split", "2 lines", "3 lines", "4 lines", "5 lines", "6 lines", "8 lines" };
    private static readonly int[] LinesValues = { 0, 2, 3, 4, 5, 6, 8 };
    private static readonly string[] VersesOptions = { "1 verse", "2 verses", "3 verses", "4 verses" };
    private static readonly string[] TransitionOptions = { "Cut", "Quick fade", "Fade", "Slow fade" };
    private static readonly double[] TransitionValues = { 0, 0.25, 0.5, 1.0 };

    public ThemeEditorViewModel SongThemeEditor { get; }
    public ThemeEditorViewModel ScriptureThemeEditor { get; }

    public string[] LinesPerSlideOptions => LinesOptions;
    public string[] VersesPerSlideOptions => VersesOptions;
    public string[] TransitionChoices => TransitionOptions;
    public string[] PictureFitChoices => ThemeEditorViewModel.FitOptions;

    /// <summary>How long the live screens fade between slides (0 = cut).</summary>
    public TimeSpan TransitionDuration => TimeSpan.FromSeconds(Math.Max(0, _settings.TransitionSeconds));

    [ObservableProperty] private int _transitionIndex;

    /// <summary>How picture slides are sized to the screen (position in <see cref="PictureFitChoices"/>).</summary>
    [ObservableProperty] private int _pictureFitIndex;

    /// <summary>Names of the connected monitors, filled in by the main window.</summary>
    public ObservableCollection<string> ScreenNames { get; } = new();

    [ObservableProperty] private int _linesPerSlideIndex;
    [ObservableProperty] private int _versesPerSlideIndex;
    [ObservableProperty] private bool _showVerseNumbers;
    [ObservableProperty] private int _outputScreenIndex;

    /// <summary>Which bottom tab is showing (Songs, Scripture, Media, Record, Themes &amp; Settings).</summary>
    [ObservableProperty] private int _selectedTabIndex;

    /// <summary>The main window watches this and opens/closes the projector window.</summary>
    [ObservableProperty] private bool _isOutputOpen;

    /// <summary>Which monitor to use; the saved choice even if that monitor is unplugged now.</summary>
    public int PreferredScreenIndex => _settings.OutputScreenIndex;

    private void LoadSettingsIntoUi()
    {
        LinesPerSlideIndex = Math.Max(0, Array.IndexOf(LinesValues, _settings.MaxLinesPerSlide));
        VersesPerSlideIndex = Math.Clamp(_settings.VersesPerSlide - 1, 0, VersesOptions.Length - 1);
        ShowVerseNumbers = _settings.ShowVerseNumbers;
        OutputScreenIndex = _settings.OutputScreenIndex;
        TransitionIndex = ClosestTransition(_settings.TransitionSeconds);
        SelectedTabIndex = Math.Max(0, _settings.SelectedTab);
        PictureFitIndex = Math.Clamp((int)_settings.PictureFit, 0, PictureFitChoices.Length - 1);
    }

    /// <summary>The menu choice nearest to a saved number of seconds.</summary>
    private static int ClosestTransition(double seconds)
    {
        var best = 0;
        for (var i = 1; i < TransitionValues.Length; i++)
            if (Math.Abs(TransitionValues[i] - seconds) < Math.Abs(TransitionValues[best] - seconds)) best = i;
        return best;
    }

    partial void OnTransitionIndexChanged(int value)
    {
        if (_loading || value < 0) return;
        _settings.TransitionSeconds = TransitionValues[value];
        SaveSettings();
        OnPropertyChanged(nameof(TransitionDuration));
        StatusMessage = value == 0 ? "Transition: cut (no fade)" : $"Transition: {TransitionOptions[value].ToLowerInvariant()} ({TransitionValues[value]:0.##} s)";
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        if (_loading || value < 0) return;
        _settings.SelectedTab = value;
        SaveSettings();
    }

    partial void OnPictureFitIndexChanged(int value)
    {
        if (_loading || value < 0) return;
        _settings.PictureFit = (ImageFit)value;
        SaveSettings();
        RebuildSlides(); // picture slides remember their size, so make them again
        StatusMessage = "Picture slides: " + PictureFitChoices[value].ToLowerInvariant();
    }

    partial void OnLinesPerSlideIndexChanged(int value)
    {
        if (_loading || value < 0) return;
        _settings.MaxLinesPerSlide = LinesValues[value];
        SaveSettings();
        RebuildSlides();
    }

    partial void OnVersesPerSlideIndexChanged(int value)
    {
        if (_loading || value < 0) return;
        _settings.VersesPerSlide = value + 1;
        SaveSettings();
        RebuildSlides();
    }

    partial void OnShowVerseNumbersChanged(bool value)
    {
        if (_loading) return;
        _settings.ShowVerseNumbers = value;
        SaveSettings();
        RebuildSlides();
    }

    partial void OnOutputScreenIndexChanged(int value)
    {
        if (_loading || value < 0) return;
        _settings.OutputScreenIndex = value;
        SaveSettings();
    }

    /// <summary>Called by the main window with the list of monitors.</summary>
    public void SetScreens(IEnumerable<string> names)
    {
        var wasLoading = _loading;
        _loading = true;
        ScreenNames.Clear();
        foreach (var name in names) ScreenNames.Add(name);
        OutputScreenIndex = Math.Clamp(_settings.OutputScreenIndex, 0, Math.Max(0, ScreenNames.Count - 1));
        _loading = wasLoading;
    }

    /// <summary>A theme slider or colour changed: restyle every slide without rebuilding them.</summary>
    private void OnThemeEdited()
    {
        SaveSettings();

        if (_previewItem is not null)
        {
            var look = SlideFactory.ThemeFor(_previewItem.Kind, _settings).Clone();
            foreach (var slide in PreviewSlides) slide.Appearance = look;
        }
        if (_liveItem is not null)
        {
            var look = SlideFactory.ThemeFor(_liveItem.Kind, _settings).Clone();
            foreach (var slide in LiveSlides) slide.Appearance = look;
        }
        UpdateOutput();
    }

    private void SaveSettings()
    {
        try { SettingsStore.Save(_paths.SettingsFile, _settings); }
        catch (Exception ex) { StatusMessage = "Couldn't save settings: " + ex.Message; }
    }
}
