using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Churchie.App.Playback;
using Churchie.Core.Services;

namespace Churchie.App.Controls;

/// <summary>
/// Shows one media file — a picture, an animated GIF or a video — sized like an
/// <see cref="Image"/>. Used by <see cref="SlideView"/> for picture/video slides and
/// for theme backgrounds.
///
/// When told to play, GIFs move and videos play. Otherwise (thumbnails) it shows a
/// still: the first frame of a GIF, or a poster frame of a video. Only screens that
/// are actually on show play anything: leaving the window stops playback.
/// </summary>
public sealed class MediaView : Grid
{
    /// <summary>What to show and how. A record, so "same as before?" is a simple comparison.</summary>
    private sealed record Request(string Path, bool Play, bool Muted, bool Loop);

    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly TextBlock _message = new()
    {
        FontSize = 44,
        Foreground = Brushes.White,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(120),
        IsVisible = false,
    };

    private Request? _request;
    private bool _onScreen;
    private int _version;              // goes up on every change, so slow background work can tell it's out of date
    private VideoPlayer? _video;       // set while a video plays
    private AnimatedPicture? _animation; // set while a GIF moves
    private readonly Action _onTick;

    public MediaView()
    {
        _onTick = OnAnimationTick;
        Children.Add(_image);
        Children.Add(_message);
    }

    /// <summary>How the picture or video is sized to fit (like Image.Stretch).</summary>
    public Stretch Stretch
    {
        get => _image.Stretch;
        set => _image.Stretch = value;
    }

    /// <summary>True when this file can be shown at all: a video that exists, or a picture that opens.</summary>
    public static bool CanShow(string? path) =>
        MediaFiles.IsVideo(path) ? File.Exists(path) : !string.IsNullOrEmpty(path) && ImageCache.Get(path) is not null;

    /// <summary>
    /// Shows a file (or nothing, when <paramref name="path"/> is null). Calling it again with
    /// the same values does nothing, so a video doesn't restart when only the text style changes.
    /// </summary>
    /// <param name="play">Make GIFs move and videos play. False shows a still picture.</param>
    /// <param name="muted">For a video: no sound.</param>
    /// <param name="loop">For a video: start again at the end.</param>
    public void Show(string? path, bool play, bool muted = true, bool loop = true)
    {
        var request = string.IsNullOrEmpty(path) ? null : new Request(path, play, muted, loop);
        if (request == _request) return;
        _request = request;
        Restart();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _onScreen = true;
        Restart();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _onScreen = false;
        Stop();
    }

    private void Restart()
    {
        Stop();
        var request = _request;
        if (request is null)
        {
            _image.Source = null;
            return;
        }

        if (MediaFiles.IsVideo(request.Path))
        {
            _image.Source = null;
            if (request.Play && _onScreen) _ = PlayVideoAsync(request);
            else _ = ShowPosterAsync(request);
            return;
        }

        // A picture: show it straight away (for a GIF this is its first frame)...
        _image.Source = ImageCache.Get(request.Path);

        // ...then, if it may move and we're allowed to play it, unpack the animation.
        if (request.Play && _onScreen && MediaFiles.MayBeAnimated(request.Path))
            _ = AnimateAsync(request);
    }

    /// <summary>Lets go of anything playing.</summary>
    private void Stop()
    {
        _version++;
        if (_video is not null)
        {
            _video.FrameChanged -= OnVideoFrame;
            VideoLibrary.Release(_video);
            _video = null;
        }
        if (_animation is not null)
        {
            AnimationClock.Remove(_onTick);
            _animation = null;
        }
        ShowMessage(null);
    }

    // ------------------------------------------------------------------
    // Videos
    // ------------------------------------------------------------------

    private async Task PlayVideoAsync(Request request)
    {
        var version = _version;
        try
        {
            var vlc = await VideoEngine.StartAsync();
            if (version != _version) return; // something else was asked for meanwhile
            if (vlc is null)
            {
                ShowMessage(VideoEngine.Problem);
                return;
            }

            _video = VideoLibrary.Acquire(vlc, request.Path, request.Muted, request.Loop);
            _video.FrameChanged += OnVideoFrame;
            OnVideoFrame(); // another screen may already be playing it: show its picture now
        }
        catch (Exception ex)
        {
            if (version == _version) ShowMessage("This video can't be played: " + ex.Message);
        }
    }

    private void OnVideoFrame()
    {
        var frame = _video?.Frame;
        if (!ReferenceEquals(_image.Source, frame)) _image.Source = frame;

        // The bitmap is the same object with new pixels in it, so ask for a redraw.
        _image.InvalidateVisual();
    }

    private async Task ShowPosterAsync(Request request)
    {
        var version = _version;
        try
        {
            var poster = await VideoLibrary.GetPosterAsync(request.Path);
            if (version != _version) return;
            _image.Source = poster;
            if (poster is null) ShowMessage(VideoEngine.Problem);
        }
        catch
        {
            // No poster: the slide shows black with its "VIDEO" label, which is fine.
        }
    }

    // ------------------------------------------------------------------
    // Animated GIFs
    // ------------------------------------------------------------------

    private async Task AnimateAsync(Request request)
    {
        var version = _version;
        var animation = await AnimatedPicture.LoadAsync(request.Path);
        if (version != _version || animation is null) return; // out of date, or a still picture

        _animation = animation;
        AnimationClock.Add(_onTick);
        OnAnimationTick();
    }

    private void OnAnimationTick()
    {
        if (_animation is null) return;
        var frame = _animation.FrameAt(AnimationClock.Now);
        if (!ReferenceEquals(_image.Source, frame)) _image.Source = frame;
    }

    private void ShowMessage(string? text)
    {
        _message.Text = text ?? "";
        _message.IsVisible = !string.IsNullOrEmpty(text);
    }
}

/// <summary>
/// Keeps decoded pictures in memory so dozens of thumbnails can share one picture.
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
