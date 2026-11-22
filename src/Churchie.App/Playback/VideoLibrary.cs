using Avalonia;
using Avalonia.Media.Imaging;
using LibVLCSharp.Shared;

namespace Churchie.App.Playback;

/// <summary>What a video is doing on a slide: it is the slide itself, or the theme background behind the words.</summary>
public enum VideoRole
{
    Slide,
    Background,
}

/// <summary>
/// Shares playing videos between everything that shows them, and makes still
/// thumbnails ("posters") of videos.
///
/// The Live panel and the projector show the same slide, so they share one player:
/// the video is decoded once, both screens show the same picture at the same moment,
/// and its sound plays once instead of twice slightly out of step. The Preview panel
/// is a different "channel", so pausing the preview never pauses what's live.
///
/// Every <see cref="Acquire"/> must be matched by one <see cref="Release"/>. When the
/// last screen lets go, the video stops. Only use this from the window's thread.
/// </summary>
public static class VideoLibrary
{
    /// <summary>The Live panel and projector.</summary>
    public const string LiveChannel = "live";

    /// <summary>The big screen on the Preview panel.</summary>
    public const string PreviewChannel = "preview";

    /// <summary>Players are shared only when they play the same file in the same way on the same channel.</summary>
    private sealed record Key(string Path, bool Muted, bool Loop, string Channel);

    private sealed class Entry
    {
        public Entry(VideoPlayer player, VideoRole role) { Player = player; Role = role; }
        public VideoPlayer Player { get; }
        public VideoRole Role { get; }
        public int Users { get; set; }
        public long LastUsed { get; set; } // bigger = asked for more recently
    }

    private static readonly Dictionary<Key, Entry> Playing = new();
    private static long _useCounter;

    /// <summary>Raised (with the channel's name) when a video starts or stops on that channel.</summary>
    public static event Action<string>? Changed;

    /// <summary>Starts the video, or joins it if another screen on the same channel is already playing it.</summary>
    /// <param name="pauseAtStart">Show the first picture and wait for Play (the Preview panel does this).</param>
    public static VideoPlayer Acquire(LibVLC vlc, string path, bool muted, bool loop, string channel, VideoRole role, bool pauseAtStart)
    {
        var key = new Key(path, muted, loop, channel);
        if (!Playing.TryGetValue(key, out var entry))
        {
            entry = new Entry(new VideoPlayer(vlc, path, muted, loop, pauseAtStart: pauseAtStart), role);
            Playing[key] = entry;
            entry.Player.Play();
        }
        entry.Users++;
        entry.LastUsed = ++_useCounter;
        Changed?.Invoke(channel);
        return entry.Player;
    }

    /// <summary>Lets go of a video from <see cref="Acquire"/>. The last one to let go stops it.</summary>
    public static void Release(VideoPlayer player)
    {
        foreach (var (key, entry) in Playing)
        {
            if (!ReferenceEquals(entry.Player, player)) continue;

            entry.Users--;
            if (entry.Users <= 0)
            {
                Playing.Remove(key);
                player.Dispose();
            }
            Changed?.Invoke(key.Channel);
            return;
        }
    }

    /// <summary>
    /// The video that the Play/Pause controls under a screen work on: the newest video slide
    /// on that channel, or else its newest background video. Null when no video is showing.
    /// </summary>
    public static VideoPlayer? Current(string channel) =>
        Playing
            .Where(pair => pair.Key.Channel == channel)
            .Select(pair => pair.Value)
            .OrderByDescending(entry => entry.Role == VideoRole.Slide)
            .ThenByDescending(entry => entry.LastUsed)
            .FirstOrDefault()?.Player;

    // ------------------------------------------------------------------
    // Posters: a still picture of a video, for thumbnails
    // ------------------------------------------------------------------

    private const int PosterWidth = 640;
    private static readonly Dictionary<string, Task<Bitmap?>> Posters = new();

    // Making a poster briefly opens the video. One at a time, so adding twenty videos
    // doesn't start twenty decoders at once.
    private static readonly SemaphoreSlim PosterTurn = new(1, 1);

    /// <summary>A small still picture from the video, made once and remembered. Null if it can't be made.</summary>
    public static Task<Bitmap?> GetPosterAsync(string path)
    {
        if (!Posters.TryGetValue(path, out var poster))
        {
            poster = MakePosterAsync(path);
            Posters[path] = poster;
        }
        return poster;
    }

    private static async Task<Bitmap?> MakePosterAsync(string path)
    {
        if (!File.Exists(path)) return null;
        var vlc = await VideoEngine.StartAsync();
        if (vlc is null) return null;

        await PosterTurn.WaitAsync();
        try
        {
            // Try one second in first (many videos open on black), then the very start
            // for clips shorter than that.
            foreach (var startSeconds in new[] { 1.0, 0.0 })
            {
                var player = new VideoPlayer(vlc, path, muted: true, loop: false, startSeconds);
                player.Play();

                var timeout = Task.Delay(TimeSpan.FromSeconds(8));
                var finished = await Task.WhenAny(player.FirstFrame, timeout);
                var gotPicture = finished == player.FirstFrame && player.FirstFrame.Result;
                var frame = player.Frame;
                player.Dispose();

                if (gotPicture && frame is not null)
                    return await Task.Run(() => Shrink(frame));
            }
            return null;
        }
        catch
        {
            return null;
        }
        finally
        {
            PosterTurn.Release();
        }
    }

    /// <summary>A smaller copy, so dozens of thumbnails don't each hold a full-size picture.</summary>
    private static Bitmap Shrink(Bitmap frame)
    {
        var size = frame.PixelSize;
        if (size.Width <= PosterWidth) return frame;
        var height = Math.Max(1, size.Height * PosterWidth / size.Width);
        return frame.CreateScaledBitmap(new PixelSize(PosterWidth, height), BitmapInterpolationMode.HighQuality);
    }
}
