using System.ComponentModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Churchie.Core.Models;

namespace Churchie.App.ViewModels;

/// <summary>
/// Edits one <see cref="SlideTheme"/> (the Songs look or the Scripture look).
/// Every change is written straight back to the theme and announced through
/// <see cref="Changed"/>, so the projector updates while you drag a slider.
/// </summary>
public partial class ThemeEditorViewModel : ViewModelBase
{
    private static readonly string[] HorizontalOptions = { "Left", "Center", "Right" };
    private static readonly string[] VerticalOptions = { "Top", "Center", "Bottom" };

    private readonly SlideTheme _theme;
    private readonly Func<Task<string?>> _pickImage;
    private readonly bool _loading;

    public event Action? Changed;

    public ThemeEditorViewModel(SlideTheme theme, Func<Task<string?>> pickImage)
    {
        _theme = theme;
        _pickImage = pickImage;

        _loading = true;
        FontSize = theme.FontSize;
        FontFamily = theme.FontFamily;
        TextColor = theme.TextColor;
        BackgroundColor = theme.BackgroundColor;
        BackgroundImage = theme.BackgroundImage;
        BackgroundDim = theme.BackgroundDim;
        Bold = theme.Bold;
        Uppercase = theme.Uppercase;
        Shadow = theme.Shadow;
        Alignment = theme.Alignment;
        VerticalAlignment = theme.VerticalAlignment;
        FooterFontSize = theme.FooterFontSize;
        _loading = false;
    }

    public string Title => _theme.Name;
    public string[] Alignments => HorizontalOptions;
    public string[] VerticalAlignments => VerticalOptions;

    [ObservableProperty] private double _fontSize;
    [ObservableProperty] private string _fontFamily = "";
    [ObservableProperty] private string _textColor = "#FFFFFF";
    [ObservableProperty] private string _backgroundColor = "#000000";
    [ObservableProperty] private string? _backgroundImage;
    [ObservableProperty] private double _backgroundDim;
    [ObservableProperty] private bool _bold;
    [ObservableProperty] private bool _uppercase;
    [ObservableProperty] private bool _shadow;
    [ObservableProperty] private string _alignment = "Center";
    [ObservableProperty] private string _verticalAlignment = "Center";
    [ObservableProperty] private double _footerFontSize;

    // Read-only helpers for the UI
    public IBrush TextBrush => ToBrush(TextColor, Colors.White);
    public IBrush BackgroundBrush => ToBrush(BackgroundColor, Colors.Black);
    public string BackgroundImageName => string.IsNullOrEmpty(BackgroundImage) ? "No image" : Path.GetFileName(BackgroundImage);
    public string FontSizeText => $"{FontSize:0} px";
    public string FooterFontSizeText => $"{FooterFontSize:0} px";
    public string BackgroundDimText => $"{BackgroundDim * 100:0}%";

    [RelayCommand]
    private async Task ChooseImageAsync()
    {
        var path = await _pickImage();
        if (path is not null) BackgroundImage = path;
    }

    [RelayCommand]
    private void ClearImage() => BackgroundImage = null;

    /// <summary>Used by the Media tab ("Use as background").</summary>
    public void SetBackgroundImage(string? path) => BackgroundImage = path;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_loading) return;

        switch (e.PropertyName)
        {
            case nameof(TextColor): OnPropertyChanged(nameof(TextBrush)); break;
            case nameof(BackgroundColor): OnPropertyChanged(nameof(BackgroundBrush)); break;
            case nameof(BackgroundImage): OnPropertyChanged(nameof(BackgroundImageName)); break;
            case nameof(FontSize): OnPropertyChanged(nameof(FontSizeText)); break;
            case nameof(FooterFontSize): OnPropertyChanged(nameof(FooterFontSizeText)); break;
            case nameof(BackgroundDim): OnPropertyChanged(nameof(BackgroundDimText)); break;
            case nameof(TextBrush) or nameof(BackgroundBrush) or nameof(BackgroundImageName)
                or nameof(FontSizeText) or nameof(FooterFontSizeText) or nameof(BackgroundDimText):
                return; // these are only display helpers
        }

        _theme.FontSize = FontSize;
        _theme.FontFamily = FontFamily?.Trim() ?? "";
        _theme.TextColor = TextColor?.Trim() ?? "";
        _theme.BackgroundColor = BackgroundColor?.Trim() ?? "";
        _theme.BackgroundImage = BackgroundImage;
        _theme.BackgroundDim = BackgroundDim;
        _theme.Bold = Bold;
        _theme.Uppercase = Uppercase;
        _theme.Shadow = Shadow;
        _theme.Alignment = Alignment ?? "Center";
        _theme.VerticalAlignment = VerticalAlignment ?? "Center";
        _theme.FooterFontSize = FooterFontSize;

        Changed?.Invoke();
    }

    private static IBrush ToBrush(string? hex, Color fallback) =>
        new SolidColorBrush(!string.IsNullOrWhiteSpace(hex) && Color.TryParse(hex.Trim(), out var c) ? c : fallback);
}
