using System.Diagnostics;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SkiaSharp;

namespace Churchie.App.Playback;

/// <summary>
/// The frames of an animated GIF (or animated WebP), ready to show one after another.
///
/// Avalonia shows only the first frame of a GIF, so we unpack all frames ourselves with
/// SkiaSharp — the drawing library Avalonia already uses — once, in the background.
/// Many GIF frames only contain the part of the picture that changed, so each frame is
/// drawn on top of the one before to get the full picture, the same way a web browser does.
/// </summary>
public sealed class AnimatedPicture
{
    // Unpacked frames take far more memory than the GIF file. Big or long GIFs are
    // shrunk until all their frames fit in about this much.
    private const long MemoryBudgetBytes = 200L * 1024 * 1024;
    private const int MaxSide = 1920;

    private static readonly Dictionary<string, Task<AnimatedPicture?>> Cache = new();

    private readonly Bitmap[] _frames;
    private readonly long[] _frameEndsMs; // when each frame stops showing, counted from the start
    private readonly long _lengthMs;

    private AnimatedPicture(Bitmap[] frames, long[] frameEndsMs)
    {
        _frames = frames;
        _frameEndsMs = frameEndsMs;
        _lengthMs = Math.Max(1, frameEndsMs[^1]);
    }

    /// <summary>
    /// Unpacks the animation (once; later calls share the result). Finishes with null for a
    /// still picture or an unreadable file — those are shown as an ordinary picture instead.
    /// </summary>
    public static Task<AnimatedPicture?> LoadAsync(string path)
    {
        if (!Cache.TryGetValue(path, out var loading))
        {
            loading = Task.Run(() => Decode(path));
            Cache[path] = loading;
        }
        return loading;
    }

    /// <summary>The frame to show at a moment in time. The animation repeats for ever.</summary>
    public Bitmap FrameAt(TimeSpan time)
    {
        var ms = (long)time.TotalMilliseconds % _lengthMs;
        for (var i = 0; i < _frames.Length; i++)
        {
            if (ms < _frameEndsMs[i]) return _frames[i];
        }
        return _frames[^1];
    }

    private static AnimatedPicture? Decode(string path)
    {
        try
        {
            using var codec = SKCodec.Create(path);
            if (codec is null || codec.FrameCount < 2) return null;

            var width = codec.Info.Width;
            var height = codec.Info.Height;
            var frameInfo = codec.FrameInfo;
            var target = ShrunkSize(width, height, codec.FrameCount);

            // One full-size canvas that each frame is drawn onto in turn.
            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var canvas = new SKBitmap(info);

            var frames = new List<Bitmap>();
            var ends = new List<long>();
            long time = 0;

            for (var i = 0; i < codec.FrameCount; i++)
            {
                // Which earlier frame this one is drawn on top of (-1 = none, it stands alone).
                var required = frameInfo[i].RequiredFrame;
                if (required == -1) canvas.Erase(SKColors.Transparent);

                // The canvas still holds frame i-1, so tell Skia it can build on that.
                // If Skia says that shortcut isn't allowed for this frame, let it start from scratch.
                var prior = required != -1 && i - 1 >= required ? i - 1 : -1;
                var result = codec.GetPixels(info, canvas.GetPixels(), new SKCodecOptions(i, prior));
                if (result == SKCodecResult.InvalidParameters && prior != -1)
                    result = codec.GetPixels(info, canvas.GetPixels(), new SKCodecOptions(i));
                if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput) break;

                frames.Add(ToBitmap(canvas, target));

                // Browsers treat "no delay" frames as 1/10 s; so do we, or they'd flash past.
                var duration = frameInfo[i].Duration;
                time += duration <= 10 ? 100 : duration;
                ends.Add(time);
            }

            return frames.Count >= 2 ? new AnimatedPicture(frames.ToArray(), ends.ToArray()) : null;
        }
        catch
        {
            return null; // fall back to showing the first frame as a still picture
        }
    }

    /// <summary>The size to keep frames at, so all of them together fit the memory budget.</summary>
    private static PixelSize ShrunkSize(int width, int height, int frameCount)
    {
        var scale = Math.Min(1.0, (double)MaxSide / Math.Max(width, height));
        var bytesPerFrame = MemoryBudgetBytes / Math.Max(1, frameCount);
        var bytesAtScale = width * scale * height * scale * 4;
        if (bytesAtScale > bytesPerFrame) scale *= Math.Sqrt(bytesPerFrame / bytesAtScale);
        return new PixelSize(Math.Max(1, (int)(width * scale)), Math.Max(1, (int)(height * scale)));
    }

    /// <summary>Copies the canvas into an Avalonia bitmap, shrinking it if needed.</summary>
    private static Bitmap ToBitmap(SKBitmap canvas, PixelSize target)
    {
        var full = new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Premul, canvas.GetPixels(),
            new PixelSize(canvas.Width, canvas.Height), new Vector(96, 96), canvas.RowBytes);
        if (target.Width >= canvas.Width) return full;

        var small = full.CreateScaledBitmap(target, BitmapInterpolationMode.MediumQuality);
        full.Dispose();
        return small;
    }
}

/// <summary>
/// One shared timer that moves every animated GIF on to its next frame. Sharing the clock
/// keeps the Live panel and the projector showing exactly the same frame.
/// </summary>
public static class AnimationClock
{
    private static readonly Stopwatch Watch = Stopwatch.StartNew();
    private static readonly List<Action> Listeners = new();
    private static DispatcherTimer? _timer;

    /// <summary>Time since the app started; animations pick their frame from this.</summary>
    public static TimeSpan Now => Watch.Elapsed;

    /// <summary>Calls <paramref name="onTick"/> about 50 times a second until <see cref="Remove"/>.</summary>
    public static void Add(Action onTick)
    {
        Listeners.Add(onTick);
        if (_timer is not null) return;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        _timer.Tick += (_, _) =>
        {
            // A copy, because a listener may remove itself while we go through the list.
            foreach (var listener in Listeners.ToArray()) listener();
        };
        _timer.Start();
    }

    public static void Remove(Action onTick)
    {
        Listeners.Remove(onTick);
        if (Listeners.Count > 0 || _timer is null) return;

        // Nothing is animating: stop the timer so the computer can rest.
        _timer.Stop();
        _timer = null;
    }
}
