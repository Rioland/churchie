using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using LibVLCSharp.Shared;

namespace Churchie.App.Playback;

/// <summary>
/// Plays one video file and keeps its newest picture in <see cref="Frame"/>, an ordinary
/// bitmap that Avalonia draws like any photo. Because the video ends up as a normal
/// picture, the cross-fade, the background dimming and the words on top all work
/// exactly as they do for pictures, on the Live panel and on the projector.
///
/// How it works: VLC decodes the video on its own threads and calls us back.
///   1. "format"  — VLC says how big the video is; we ask for 4-byte BGRA pixels and set up memory.
///   2. "lock"    — VLC asks where to draw the next picture; we hand it that memory.
///   3. "display" — the picture is due on screen; we copy it into <see cref="Frame"/>
///                  and ask the window's thread to redraw.
///   4. "cleanup" — playback stopped; we free the memory.
/// Use <see cref="VideoLibrary"/> rather than creating players directly, so screens can share them.
/// </summary>
public sealed class VideoPlayer : IDisposable
{
    // Bigger videos (4K) are shrunk by VLC to this size. A projector can't show more,
    // and it saves the computer a lot of work.
    private const double MaxWidth = 1920;
    private const double MaxHeight = 1080;

    private readonly Media _media;
    private readonly MediaPlayer _player;

    // _gate protects the fields below, because VLC's threads and the window's thread both use them.
    private readonly object _gate = new();
    private IntPtr _buffer;      // memory VLC draws each picture into (outside .NET's managed memory)
    private int _width;          // picture size in pixels
    private int _height;
    private int _pitch;          // bytes from the start of one row of pixels to the next
    private WriteableBitmap? _frame;
    private bool _disposed;

    // 1 while a "new picture" message is waiting for the window's thread, so a busy
    // computer gets one redraw request at a time instead of hundreds piling up.
    private int _redrawPending;

    private readonly TaskCompletionSource<bool> _firstFrame =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    // For VideoLog only: how many pictures have been shown, and since when.
    private readonly string _name;
    private readonly Stopwatch _clock = new();
    private int _frameCount;

    /// <param name="vlc">The engine from <see cref="VideoEngine"/>.</param>
    /// <param name="muted">True for backgrounds and the logo: no sound.</param>
    /// <param name="loop">Start again from the beginning at the end.</param>
    /// <param name="startSeconds">Where to start; thumbnails start a little in, past any black opening frames.</param>
    public VideoPlayer(LibVLC vlc, string path, bool muted, bool loop, double startSeconds = 0)
    {
        _name = Path.GetFileName(path);
        _media = new Media(vlc, path, FromType.FromPath);
        if (muted) _media.AddOption(":no-audio");
        if (loop) _media.AddOption(":input-repeat=65535"); // VLC's way of saying "repeat (almost) for ever"
        if (startSeconds > 0) _media.AddOption(":start-time=" + startSeconds.ToString(CultureInfo.InvariantCulture));
        _media.AddOption(":no-sub-autodetect-file"); // don't show subtitle files that happen to sit beside the video

        _player = new MediaPlayer(_media);
        _player.SetVideoFormatCallbacks(OnFormat, OnCleanup);
        _player.SetVideoCallbacks(OnLock, null, OnDisplay);

        // If the video ends or fails before showing anything, nobody should wait for a first picture.
        _player.EndReached += (_, _) =>
        {
            VideoLog.Write($"{_name}: reached the end after {_frameCount} pictures");
            _firstFrame.TrySetResult(false);
        };
        _player.EncounteredError += (_, _) =>
        {
            VideoLog.Write($"{_name}: VLC reported an error");
            _firstFrame.TrySetResult(false);
        };
    }

    /// <summary>The newest picture from the video, or null until the first one arrives.</summary>
    public WriteableBitmap? Frame
    {
        get { lock (_gate) return _frame; }
    }

    /// <summary>Raised on the window's thread whenever <see cref="Frame"/> has a new picture (or is a new bitmap).</summary>
    public event Action? FrameChanged;

    /// <summary>Finishes with true once the first picture has arrived, or false if the video ended or failed first.</summary>
    public Task<bool> FirstFrame => _firstFrame.Task;

    public void Play()
    {
        VideoLog.Write($"{_name}: play");
        _clock.Start();
        _player.Play();
    }

    /// <summary>
    /// Stops the video and frees VLC's resources. Stopping waits for VLC's threads to
    /// finish, which can take a moment, so it happens in the background and the window
    /// never freezes. <see cref="Frame"/> keeps the last picture (thumbnails rely on that).
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        FrameChanged = null;

        Task.Run(() =>
        {
            try
            {
                _player.Stop();
                _player.Dispose();
                _media.Dispose();
            }
            catch
            {
                // Nothing useful to do if VLC complains while shutting down.
            }
            lock (_gate) FreeBuffer();
            VideoLog.Write($"{_name}: stopped after {_frameCount} pictures in {_clock.Elapsed.TotalSeconds:0.0} s");
        });
    }

    // ------------------------------------------------------------------
    // VLC callbacks. These run on VLC's threads — never on the window's
    // thread — and must never call back into VLC (that would deadlock).
    // ------------------------------------------------------------------

    private uint OnFormat(ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines)
    {
        if (width == 0 || height == 0) return 0; // 0 tells VLC "can't do this"

        // An error escaping from a callback would close the whole app (VLC can't
        // pass .NET errors on), so anything unexpected just means "no picture".
        try
        {
            // Shrink big videos, keeping their shape. Even sizes keep VLC's converters happy.
            var scale = Math.Min(1.0, Math.Min(MaxWidth / width, MaxHeight / height));
            var w = Math.Max(2, (int)(width * scale) & ~1);
            var h = Math.Max(2, (int)(height * scale) & ~1);

            // VLC works fastest when rows and row counts are multiples of 32, so round up.
            var pitch = (w * 4 + 31) & ~31;
            var rows = (h + 31) & ~31;

            lock (_gate)
            {
                FreeBuffer();
                _buffer = Marshal.AllocHGlobal(pitch * rows);
                _width = w;
                _height = h;
                _pitch = pitch;
                // Opaque: the "unused" fourth byte is ignored instead of being read as transparency.
                _frame = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            }

            // "RV32" = 4 bytes per pixel: blue, green, red, unused — what Avalonia calls Bgra8888.
            Marshal.Copy(Encoding.ASCII.GetBytes("RV32"), 0, chroma, 4);
            VideoLog.Write($"{_name}: video is {width}x{height}, shown at {w}x{h}");
            width = (uint)w;
            height = (uint)h;
            pitches = (uint)pitch;
            lines = (uint)rows;
            return 1; // the number of picture buffers we set up
        }
        catch (Exception ex)
        {
            VideoLog.Write($"{_name}: couldn't set up pictures: {ex.Message}");
            return 0;
        }
    }

    private IntPtr OnLock(IntPtr opaque, IntPtr planes)
    {
        // "planes" is a list of pointers; RV32 has a single plane, so we fill in the first one.
        lock (_gate) Marshal.WriteIntPtr(planes, _buffer);
        return IntPtr.Zero;
    }

    private void OnDisplay(IntPtr opaque, IntPtr picture)
    {
        try
        {
            lock (_gate)
            {
                if (_disposed || _frame is null || _buffer == IntPtr.Zero) return;
                using var target = _frame.Lock();
                CopyRows(_buffer, _pitch, target.Address, target.RowBytes, _width * 4, _height);
            }
        }
        catch (Exception ex)
        {
            VideoLog.Write($"{_name}: skipped a picture: {ex.Message}");
            return; // skip this picture rather than close the app (see OnFormat)
        }

        // Every 100 pictures, note how fast they arrive (a 30 fps video should show about 30 a second).
        var count = Interlocked.Increment(ref _frameCount);
        if (VideoLog.IsOn && (count == 1 || count % 100 == 0))
            VideoLog.Write($"{_name}: picture {count} after {_clock.Elapsed.TotalSeconds:0.00} s");

        _firstFrame.TrySetResult(true);

        if (Interlocked.Exchange(ref _redrawPending, 1) == 0)
            Dispatcher.UIThread.Post(RaiseFrameChanged, DispatcherPriority.Render);
    }

    private void OnCleanup(ref IntPtr opaque)
    {
        lock (_gate) FreeBuffer();
    }

    // ------------------------------------------------------------------

    private void RaiseFrameChanged()
    {
        Interlocked.Exchange(ref _redrawPending, 0);
        if (!_disposed) FrameChanged?.Invoke();
    }

    /// <summary>Only call while holding _gate.</summary>
    private void FreeBuffer()
    {
        if (_buffer == IntPtr.Zero) return;
        Marshal.FreeHGlobal(_buffer);
        _buffer = IntPtr.Zero;
    }

    /// <summary>
    /// Copies a picture from VLC's memory into the bitmap, row by row (the two may
    /// have different row lengths). This is the one place Churchie uses "unsafe" code:
    /// raw memory addresses, which C# normally hides, are the fastest way to copy
    /// several megabytes thirty times a second.
    /// </summary>
    private static unsafe void CopyRows(IntPtr source, int sourcePitch, IntPtr target, int targetPitch, int rowBytes, int rows)
    {
        var bytes = Math.Min(rowBytes, Math.Min(sourcePitch, targetPitch));
        for (var y = 0; y < rows; y++)
        {
            var from = (byte*)source + (long)y * sourcePitch;
            var to = (byte*)target + (long)y * targetPitch;
            Buffer.MemoryCopy(from, to, targetPitch, bytes);
        }
    }
}
