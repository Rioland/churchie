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

    /// <summary>Choices for sizing a picture, in the same order as the <see cref="ImageFit"/> enum.</summary>
    public static readonly string[] FitOptions = { "Fill the screen (crop edges)", "Show whole picture", "Stretch to screen" };

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
        BackgroundFitIndex = (int)theme.BackgroundFit;
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
    public string[] FitChoices => FitOptions;

    [ObservableProperty] private double _fontSize;
    [ObservableProperty] private string _fontFamily = "";
    [ObservableProperty] private string _textColor = "#FFFFFF";
    [ObservableProperty] private string _backgroundColor = "#000000";
    [ObservableProperty] private string? _backgroundImage;
    [ObservableProperty] private int _backgroundFitIndex;
    [ObservableProperty] private double _backgroundDim;
    [ObservableProperty] private bool _bold;
    [ObservableProperty] private bool _uppercase;
    [ObservableProperty] private bool _shadow;
    [ObservableProperty] private string _alignment = "Center";
    [ObservableProperty] private string _verticalAlignment = "Center";
    [ObservableProperty] private double _footerFontSize;

    // The colour picker buttons work with a Color, while the theme stores hex text
    // ("#RRGGBB"). These two properties translate between them, so the picker and
    // the text box next to it always show the same colour.
    public Color TextColorValue
    {
        get => ToColor(TextColor, Colors.White);
        set { if (value != TextColorValue) TextColor = ToHex(value); }
    }

    public Color BackgroundColorValue
    {
        get => ToColor(BackgroundColor, Colors.Black);
        set { if (value != BackgroundColorValue) BackgroundColor = ToHex(value); }
    }

    // Read-only helpers for the UI
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
            case nameof(TextColor): OnPropertyChanged(nameof(TextColorValue)); break;
            case nameof(BackgroundColor): OnPropertyChanged(nameof(BackgroundColorValue)); break;
            case nameof(BackgroundImage): OnPropertyChanged(nameof(BackgroundImageName)); break;
            case nameof(FontSize): OnPropertyChanged(nameof(FontSizeText)); break;
            case nameof(FooterFontSize): OnPropertyChanged(nameof(FooterFontSizeText)); break;
            case nameof(BackgroundDim): OnPropertyChanged(nameof(BackgroundDimText)); break;
            case nameof(TextColorValue) or nameof(BackgroundColorValue) or nameof(BackgroundImageName)
                or nameof(FontSizeText) or nameof(FooterFontSizeText) or nameof(BackgroundDimText):
                return; // these are only display helpers
        }

        _theme.FontSize = FontSize;
        _theme.FontFamily = FontFamily?.Trim() ?? "";
        _theme.TextColor = TextColor?.Trim() ?? "";
        _theme.BackgroundColor = BackgroundColor?.Trim() ?? "";
        _theme.BackgroundImage = BackgroundImage;
        // -1 can appear for a moment while a drop-down list is being filled; treat it as the first choice.
        _theme.BackgroundFit = (ImageFit)Math.Clamp(BackgroundFitIndex, 0, FitOptions.Length - 1);
        _theme.BackgroundDim = BackgroundDim;
        _theme.Bold = Bold;
        _theme.Uppercase = Uppercase;
        _theme.Shadow = Shadow;
        _theme.Alignment = Alignment ?? "Center";
        _theme.VerticalAlignment = VerticalAlignment ?? "Center";
        _theme.FooterFontSize = FooterFontSize;

        Changed?.Invoke();
    }

    /// <summary>Reads hex text such as "#FFCC00"; anything unreadable (e.g. half-typed) gives the fallback.</summary>
    private static Color ToColor(string? hex, Color fallback) =>
        !string.IsNullOrWhiteSpace(hex) && Color.TryParse(hex.Trim(), out var c) ? c : fallback;

    /// <summary>Writes a colour as "#RRGGBB" (slides are never see-through, so no transparency part).</summary>
    private static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
}
