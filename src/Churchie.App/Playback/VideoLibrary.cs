using Avalonia;
using Avalonia.Media.Imaging;
using LibVLCSharp.Shared;

namespace Churchie.App.Playback;

/// <summary>
/// Shares playing videos between everything that shows them, and makes still
/// thumbnails ("posters") of videos.
///
/// The Live panel and the projector show the same slide, so they share one player:
/// the video is decoded once, both screens show the same picture at the same moment,
/// and its sound plays once instead of twice slightly out of step.
///
/// Every <see cref="Acquire"/> must be matched by one <see cref="Release"/>. When the
/// last screen lets go, the video stops. Only use this from the window's thread.
/// </summary>
public static class VideoLibrary
{
    /// <summary>Players are shared only when they play the same file in the same way.</summary>
    private sealed record Key(string Path, bool Muted, bool Loop);

    private sealed class Entry
    {
        public Entry(VideoPlayer player) => Player = player;
        public VideoPlayer Player { get; }
        public int Users { get; set; }
    }

    private static readonly Dictionary<Key, Entry> Playing = new();

    /// <summary>Starts the video, or joins it if another screen is already playing it.</summary>
    public static VideoPlayer Acquire(LibVLC vlc, string path, bool muted, bool loop)
    {
        var key = new Key(path, muted, loop);
        if (!Playing.TryGetValue(key, out var entry))
        {
            entry = new Entry(new VideoPlayer(vlc, path, muted, loop));
            Playing[key] = entry;
            entry.Player.Play();
        }
        entry.Users++;
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
            return;
        }
    }

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
