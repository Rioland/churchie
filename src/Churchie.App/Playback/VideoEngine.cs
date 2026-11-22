using System.Reflection;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;

namespace Churchie.App.Playback;

/// <summary>
/// Starts VLC, the free video engine Churchie uses to play videos.
///
/// VLC itself is native code (not C#). On Windows and macOS it ships inside Churchie,
/// in a "libvlc" folder next to the program. On Linux it comes from the system
/// ("sudo apt install vlc"), which is how Linux programs normally share libraries.
///
/// Starting VLC takes a moment, so it only happens the first time a video is needed.
/// If it can't start, <see cref="Problem"/> explains why and videos show that message instead.
/// </summary>
public static class VideoEngine
{
    private static readonly object Gate = new();
    private static Task<LibVLC?>? _starting;

    /// <summary>Why videos can't play, or null when they can (or nobody has tried yet).</summary>
    public static string? Problem { get; private set; }

    /// <summary>
    /// The VLC engine, started on first use on a background thread (it takes a moment
    /// and the window shouldn't freeze). Later calls get the same engine straight away.
    /// The result is null if VLC couldn't start; <see cref="Problem"/> then says why.
    /// </summary>
    public static Task<LibVLC?> StartAsync()
    {
        lock (Gate)
        {
            _starting ??= Task.Run(Start);
            return _starting;
        }
    }

    private static LibVLC? Start()
    {
        try
        {
            if (OperatingSystem.IsLinux()) UseSystemVlcOnLinux();

            // Finds and loads the VLC libraries (see the class comment for where they are).
            // Written out in full because "Core" alone would mean our own Churchie.Core.
            LibVLCSharp.Shared.Core.Initialize();

            // --no-video-title-show: don't print the file name over the video.
            // --no-osd: no on-screen messages ("Volume 80%") on the projector.
            // --quiet: keep VLC's own chatter out of the console.
            return new LibVLC("--no-video-title-show", "--no-osd", "--quiet");
        }
        catch (Exception ex)
        {
            Problem = OperatingSystem.IsLinux()
                ? "Videos need VLC. Install it with: sudo apt install vlc"
                : "Videos can't play: the video engine (VLC) didn't start. " + ex.Message;
            return null;
        }
    }

    /// <summary>
    /// LibVLCSharp asks for a library called "libvlc". Linux names it "libvlc.so.5",
    /// and only the developer package adds the plain "libvlc.so" name. This tells .NET
    /// to try the real name too, so installing the normal "vlc" package is enough.
    /// </summary>
    private static void UseSystemVlcOnLinux()
    {
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(LibVLC).Assembly, ResolveOnLinux);
        }
        catch (InvalidOperationException)
        {
            // A resolver was already set (e.g. by a newer LibVLCSharp) — that one wins.
        }
    }

    private static IntPtr ResolveOnLinux(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        var candidates = name switch
        {
            "libvlc" => new[] { "libvlc.so.5", "libvlc.so" },
            "libvlccore" => new[] { "libvlccore.so.9", "libvlccore.so" },
            _ => Array.Empty<string>(),
        };

        foreach (var candidate in candidates)
        {
            if (NativeLibrary.TryLoad(candidate, out var handle)) return handle;
        }

        // IntPtr.Zero means "not handled here": .NET then searches the usual way.
        return IntPtr.Zero;
    }
}
