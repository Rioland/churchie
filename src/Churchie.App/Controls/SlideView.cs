using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Churchie.Core.Models;
using HAlign = Avalonia.Layout.HorizontalAlignment;
using VAlign = Avalonia.Layout.VerticalAlignment;

namespace Churchie.App.Controls;

/// <summary>
/// Draws one slide. The slide is always laid out on a 1920×1080 canvas and then
/// scaled to fit, so a thumbnail, the live preview and the projector all look
/// exactly the same — only bigger or smaller.
///
/// Long text automatically shrinks to fit the screen.
/// </summary>
public sealed class SlideView : UserControl
{
    public static readonly StyledProperty<Slide?> SlideProperty =
        AvaloniaProperty.Register<SlideView, Slide?>(nameof(Slide));

    public static readonly StyledProperty<SlideTheme?> AppearanceProperty =
        AvaloniaProperty.Register<SlideView, SlideTheme?>(nameof(Appearance));

    /// <summary>What to show. Null shows just the background.</summary>
    public Slide? Slide
    {
        get => GetValue(SlideProperty);
        set => SetValue(SlideProperty, value);
    }

    /// <summary>How to show it (font, colours, background).</summary>
    public SlideTheme? Appearance
    {
        get => GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    private const double CanvasWidth = 1920;
    private const double CanvasHeight = 1080;
    private const double SideMargin = 100;

    private readonly Border _background = new();
    private readonly Image _backgroundImage = new() { Stretch = Stretch.UniformToFill };
    private readonly Border _dim = new() { Background = Brushes.Black };
    private readonly Image _slideImage = new() { Stretch = Stretch.Uniform };
    private readonly TextBlock _text = new()
    {
        TextWrapping = TextWrapping.Wrap,
        Width = CanvasWidth - 2 * SideMargin,
    };
    private readonly Viewbox _textFit = new()
    {
        Stretch = Stretch.Uniform,
        StretchDirection = StretchDirection.DownOnly,
        HorizontalAlignment = HAlign.Center,
    };
    private readonly TextBlock _footer = new()
    {
        TextWrapping = TextWrapping.NoWrap,
        TextTrimming = TextTrimming.CharacterEllipsis,
        HorizontalAlignment = HAlign.Center,
        VerticalAlignment = VAlign.Bottom,
        Margin = new Thickness(SideMargin, 0, SideMargin, 40),
    };
    private readonly DropShadowEffect _shadow = NewShadow();
    private readonly DropShadowEffect _footerShadow = NewShadow();

    private static DropShadowEffect NewShadow() => new()
    {
        Color = Colors.Black,
        BlurRadius = 14,
        OffsetX = 3,
        OffsetY = 3,
        Opacity = 0.9,
    };

    public SlideView()
    {
        _textFit.Child = _text;

        var canvas = new Grid
        {
            Width = CanvasWidth,
            Height = CanvasHeight,
            ClipToBounds = true,
        };
        canvas.Children.Add(_background);
        canvas.Children.Add(_backgroundImage);
        canvas.Children.Add(_dim);
        canvas.Children.Add(_slideImage);
        canvas.Children.Add(_textFit);
        canvas.Children.Add(_footer);

        Content = new Viewbox { Stretch = Stretch.Uniform, Child = canvas };
        ClipToBounds = true;
        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SlideProperty || change.Property == AppearanceProperty)
            Refresh();
    }

    private void Refresh()
    {
        // Can be called by the base class before our constructor has run.
        if (_textFit is null) return;

        var theme = Appearance ?? SlideTheme.Black();
        var slide = Slide;

        // A picture slide (media item or logo) fills the screen on black.
        if (slide?.ImagePath is { Length: > 0 } picture)
        {
            _background.Background = Brushes.Black;
            _backgroundImage.IsVisible = false;
            _dim.IsVisible = false;
            _slideImage.Source = ImageCache.Get(picture);
            _slideImage.IsVisible = true;
            _textFit.IsVisible = false;
            _footer.IsVisible = false;
            return;
        }

        _slideImage.IsVisible = false;

        // Background colour and optional image
        _background.Background = new SolidColorBrush(ParseColor(theme.BackgroundColor, Colors.Black));
        var bitmap = string.IsNullOrEmpty(theme.BackgroundImage) ? null : ImageCache.Get(theme.BackgroundImage);
        _backgroundImage.Source = bitmap;
        _backgroundImage.IsVisible = bitmap is not null;
        _dim.IsVisible = bitmap is not null && theme.BackgroundDim > 0;
        _dim.Opacity = Math.Clamp(theme.BackgroundDim, 0, 1);

        // Main text
        var text = slide?.Text ?? "";
        if (theme.Uppercase) text = text.ToUpper(CultureInfo.CurrentCulture);
        _text.Text = text;
        _text.FontSize = Math.Clamp(theme.FontSize, 8, 400);
        _text.FontWeight = theme.Bold ? FontWeight.Bold : FontWeight.Normal;
        _text.Foreground = new SolidColorBrush(ParseColor(theme.TextColor, Colors.White));
        _text.TextAlignment = theme.Alignment switch
        {
            "Left" => TextAlignment.Left,
            "Right" => TextAlignment.Right,
            _ => TextAlignment.Center,
        };
        if (string.IsNullOrWhiteSpace(theme.FontFamily)) _text.ClearValue(TextBlock.FontFamilyProperty);
        else _text.FontFamily = new FontFamily(theme.FontFamily);
        _text.Effect = theme.Shadow ? _shadow : null;
        _textFit.IsVisible = text.Length > 0;

        var hasFooter = !string.IsNullOrWhiteSpace(slide?.Footer);
        var bottomSpace = hasFooter ? theme.FooterFontSize + 90 : 80;
        _textFit.Margin = new Thickness(SideMargin, 80, SideMargin, bottomSpace);
        _textFit.VerticalAlignment = theme.VerticalAlignment switch
        {
            "Top" => VAlign.Top,
            "Bottom" => VAlign.Bottom,
            _ => VAlign.Center,
        };

        // Footer (e.g. "John 3:16 (KJV)")
        _footer.IsVisible = hasFooter;
        _footer.Text = slide?.Footer ?? "";
        _footer.FontSize = Math.Clamp(theme.FooterFontSize, 8, 200);
        _footer.Foreground = new SolidColorBrush(ParseColor(theme.FooterColor, Colors.LightGray));
        _footer.Effect = theme.Shadow ? _footerShadow : null;
        if (string.IsNullOrWhiteSpace(theme.FontFamily)) _footer.ClearValue(TextBlock.FontFamilyProperty);
        else _footer.FontFamily = new FontFamily(theme.FontFamily);
    }

    private static Color ParseColor(string? hex, Color fallback) =>
        !string.IsNullOrWhiteSpace(hex) && Color.TryParse(hex.Trim(), out var color) ? color : fallback;
}

/// <summary>
/// Keeps decoded images in memory so dozens of thumbnails can share one picture.
/// </summary>
public static class ImageCache
{
    private static readonly Dictionary<string, Bitmap?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static Bitmap? Get(string path)
    {
        if (Cache.TryGetValue(path, out var cached)) return cached;

        Bitmap? bitmap = null;
        try
        {
            if (File.Exists(path)) bitmap = new Bitmap(path);
        }
        catch
        {
            // Unreadable or unsupported image: show nothing rather than crash.
        }

        Cache[path] = bitmap;
        return bitmap;
    }
}
