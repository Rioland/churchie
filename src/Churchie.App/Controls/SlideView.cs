using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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
///
/// Pictures, GIFs and videos (as slides or as the theme background) are drawn by
/// <see cref="MediaView"/>. They only move when <see cref="Play"/> is on — the Live
/// panel and the projector turn it on; thumbnails leave it off and show a still.
/// </summary>
public sealed class SlideView : UserControl
{
    public static readonly StyledProperty<Slide?> SlideProperty =
        AvaloniaProperty.Register<SlideView, Slide?>(nameof(Slide));

    public static readonly StyledProperty<SlideTheme?> AppearanceProperty =
        AvaloniaProperty.Register<SlideView, SlideTheme?>(nameof(Appearance));

    public static readonly StyledProperty<bool> PlayProperty =
        AvaloniaProperty.Register<SlideView, bool>(nameof(Play));

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

    /// <summary>Play videos and animated GIFs. Off (the default) shows still pictures, for thumbnails.</summary>
    public bool Play
    {
        get => GetValue(PlayProperty);
        set => SetValue(PlayProperty, value);
    }

    private const double CanvasWidth = 1920;
    private const double CanvasHeight = 1080;
    private const double SideMargin = 100;

    private readonly Border _background = new();
    private readonly MediaView _backgroundMedia = new() { Stretch = Stretch.UniformToFill };
    private readonly Border _dim = new() { Background = Brushes.Black };
    private readonly MediaView _slideMedia = new() { Stretch = Stretch.Uniform };

    // A "▶ VIDEO" label on video thumbnails, so a still poster isn't mistaken for a picture.
    private readonly Border _videoBadge = new()
    {
        Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
        CornerRadius = new CornerRadius(16),
        Padding = new Thickness(36, 14),
        Margin = new Thickness(48),
        HorizontalAlignment = HAlign.Left,
        VerticalAlignment = VAlign.Top,
        Child = new TextBlock { Text = "▶ VIDEO", FontSize = 96, FontWeight = FontWeight.Bold, Foreground = Brushes.White },
    };
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
        canvas.Children.Add(_backgroundMedia);
        canvas.Children.Add(_dim);
        canvas.Children.Add(_slideMedia);
        canvas.Children.Add(_textFit);
        canvas.Children.Add(_footer);
        canvas.Children.Add(_videoBadge);

        Content = new Viewbox { Stretch = Stretch.Uniform, Child = canvas };
        ClipToBounds = true;
        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SlideProperty || change.Property == AppearanceProperty || change.Property == PlayProperty)
            Refresh();
    }

    private void Refresh()
    {
        // Can be called by the base class before our constructor has run.
        if (_textFit is null) return;

        var theme = Appearance ?? SlideTheme.Black();
        var slide = Slide;

        // A picture or video slide (media item or logo) fills the screen on black.
        if (slide?.MediaPath is { Length: > 0 } media)
        {
            _background.Background = Brushes.Black;
            _backgroundMedia.Show(null, play: false);
            _backgroundMedia.IsVisible = false;
            _dim.IsVisible = false;
            _slideMedia.Stretch = ToStretch(slide.ImageFit);
            _slideMedia.Show(media, Play, muted: slide.Muted, loop: slide.Loop);
            _slideMedia.IsVisible = true;
            _videoBadge.IsVisible = slide.Kind == SlideKind.Video && !Play;
            _textFit.IsVisible = false;
            _footer.IsVisible = false;
            return;
        }

        _slideMedia.Show(null, play: false);
        _slideMedia.IsVisible = false;
        _videoBadge.IsVisible = false;

        // Background colour, and an optional picture, GIF or video (always silent and looping)
        _background.Background = new SolidColorBrush(ParseColor(theme.BackgroundColor, Colors.Black));
        var hasBackground = MediaView.CanShow(theme.BackgroundImage);
        _backgroundMedia.Stretch = ToStretch(theme.BackgroundFit);
        _backgroundMedia.Show(hasBackground ? theme.BackgroundImage : null, Play, muted: true, loop: true);
        _backgroundMedia.IsVisible = hasBackground;
        _dim.IsVisible = hasBackground && theme.BackgroundDim > 0;
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

    /// <summary>Translates our own fit setting into Avalonia's name for the same thing.</summary>
    private static Stretch ToStretch(ImageFit fit) => fit switch
    {
        ImageFit.Fill => Stretch.UniformToFill, // cover the screen, edges may be cropped
        ImageFit.Stretch => Stretch.Fill,       // exact screen shape, picture may look squashed
        _ => Stretch.Uniform,                   // whole picture, bars beside it
    };

    private static Color ParseColor(string? hex, Color fallback) =>
        !string.IsNullOrWhiteSpace(hex) && Color.TryParse(hex.Trim(), out var color) ? color : fallback;
}
