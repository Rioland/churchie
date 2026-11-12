using System.Runtime.InteropServices;
using Churchie.Core.Audio;
using PortAudioSharp;
using PaStream = PortAudioSharp.Stream; // "Stream" alone would be confused with System.IO.Stream

namespace Churchie.App.Services;

/// <summary>A microphone (or other sound input) the computer offers.</summary>
public sealed record AudioInputDevice(int Index, string Name, int SampleRate, bool IsDefault)
{
    // Drop-down lists show whatever ToString() returns.
    public override string ToString() => IsDefault ? Name + " (default)" : Name;
}

/// <summary>
/// Records sound from a microphone into a .wav file.
///
/// It uses PortAudio, a small free library that talks to the sound system on
/// Windows, macOS and Linux, so this one class works on all three.
///
/// How it works: once a microphone is opened, PortAudio calls <see cref="OnAudio"/>
/// many times a second on its own thread, each time with a small block of sound.
/// We measure how loud the block is (for the level meter) and, if a recording is
/// running and not paused, add it to the file.
/// </summary>
public sealed class AudioRecorder : IDisposable
{
    /// <summary>How often the file's header is corrected, so a power cut loses at most this much.</summary>
    private static readonly TimeSpan HeaderInterval = TimeSpan.FromSeconds(5);

    private static readonly object LibraryGate = new();
    private static bool _libraryReady;

    // _gate protects everything below: the sound thread and the window's thread both use it.
    private readonly object _gate = new();
    private PaStream? _stream;
    private PaStream.Callback? _callback; // kept in a field so it stays alive while PortAudio uses it
    private WavWriter? _writer;
    private float[] _block = new float[4096];
    private float _peak;
    private bool _paused;
    private TimeSpan _headerUpdatedAt;
    private string? _error;
    private int _sampleRate;

    /// <summary>True while a microphone is open (checking the level, or recording).</summary>
    public bool IsOpen => _stream is not null;

    public bool IsRecording { get { lock (_gate) return _writer is not null; } }
    public bool IsPaused { get { lock (_gate) return _paused; } }

    /// <summary>The file being recorded, or null.</summary>
    public string? FilePath { get; private set; }

    /// <summary>Length of the recording so far (pauses not counted).</summary>
    public TimeSpan Duration { get { lock (_gate) return _writer?.Duration ?? TimeSpan.Zero; } }

    public long FileSize { get { lock (_gate) return _writer?.FileSize ?? 0; } }

    // ------------------------------------------------------------------
    // Finding microphones
    // ------------------------------------------------------------------

    /// <summary>
    /// Lists the microphones. PortAudio only looks for devices when it starts, so
    /// <paramref name="rescan"/> restarts it to notice a microphone that was just plugged in.
    /// Don't rescan while a microphone is open.
    /// </summary>
    public static List<AudioInputDevice> ListDevices(bool rescan = false)
    {
        lock (LibraryGate)
        {
            if (rescan && _libraryReady)
            {
                PortAudio.Terminate();
                _libraryReady = false;
            }
            EnsureLibrary();

            var devices = new List<AudioInputDevice>();
            var defaultIndex = PortAudio.DefaultInputDevice;
            if (defaultIndex == PortAudio.NoDevice) return devices;

            // Windows offers every microphone several times, once per sound system
            // (MME, DirectSound, WASAPI...). Showing only the default system's list
            // gives each microphone once.
            var system = PortAudio.GetDeviceInfo(defaultIndex).hostApi;

            for (var i = 0; i < PortAudio.DeviceCount; i++)
            {
                var info = PortAudio.GetDeviceInfo(i);
                if (info.maxInputChannels < 1 || info.hostApi != system) continue;
                var name = string.IsNullOrWhiteSpace(info.name) ? $"Microphone {i + 1}" : info.name.Trim();
                devices.Add(new AudioInputDevice(i, name, (int)Math.Round(info.defaultSampleRate), i == defaultIndex));
            }

            // Default microphone first.
            return devices.OrderByDescending(d => d.IsDefault).ToList();
        }
    }

    private static void EnsureLibrary()
    {
        if (_libraryReady) return;
        PortAudio.Initialize();
        _libraryReady = true;
    }

    /// <summary>Call once when the app closes, after every recorder is disposed.</summary>
    public static void Shutdown()
    {
        lock (LibraryGate)
        {
            if (!_libraryReady) return;
            try { PortAudio.Terminate(); } catch (PortAudioException) { /* closing anyway */ }
            _libraryReady = false;
        }
    }

    // ------------------------------------------------------------------
    // Listening and recording
    // ------------------------------------------------------------------

    /// <summary>Opens the microphone and starts measuring its level. Nothing is saved yet.</summary>
    public void Open(AudioInputDevice device)
    {
        if (IsOpen) return;

        lock (LibraryGate) EnsureLibrary();
        var info = PortAudio.GetDeviceInfo(device.Index);

        var input = new StreamParameters
        {
            device = device.Index,
            channelCount = 1,                               // mono: right for speech, and half the file size
            sampleFormat = SampleFormat.Float32,            // samples arrive as numbers from -1 to +1
            suggestedLatency = info.defaultHighInputLatency, // bigger blocks: no need to be instant, and fewer glitches
            hostApiSpecificStreamInfo = IntPtr.Zero,
        };

        _sampleRate = device.SampleRate > 0 ? device.SampleRate : 44100;
        _callback = OnAudio;
        lock (_gate) { _peak = 0; _error = null; }

        var stream = new PaStream(
            inParams: input,
            outParams: null,
            sampleRate: _sampleRate,
            framesPerBuffer: PortAudio.FramesPerBufferUnspecified,
            streamFlags: StreamFlags.ClipOff,
            callback: _callback,
            userData: IntPtr.Zero);

        try
        {
            stream.Start();
        }
        catch
        {
            stream.Dispose();
            throw;
        }
        _stream = stream;
    }

    /// <summary>Starts saving the sound to a new file. The microphone must be open.</summary>
    public void StartRecording(string path)
    {
        if (!IsOpen) throw new InvalidOperationException("Open a microphone first.");

        var writer = new WavWriter(path, _sampleRate);
        lock (_gate)
        {
            if (_writer is not null) { writer.Dispose(); throw new InvalidOperationException("Already recording."); }
            _writer = writer;
            _paused = false;
            _headerUpdatedAt = TimeSpan.Zero;
            _error = null;
        }
        FilePath = path;
    }

    public void Pause() { lock (_gate) _paused = true; }
    public void Resume() { lock (_gate) _paused = false; }

    /// <summary>Finishes the file (the microphone stays open). Returns the file's path, or null if nothing was recording.</summary>
    public string? StopRecording()
    {
        WavWriter? writer;
        lock (_gate)
        {
            writer = _writer;
            _writer = null;
            _paused = false;
        }
        if (writer is null) return null;

        writer.Dispose(); // corrects the header and closes the file
        var path = FilePath;
        FilePath = null;
        return path;
    }

    /// <summary>Stops recording (if any) and lets go of the microphone.</summary>
    public void Close()
    {
        var stream = _stream;
        _stream = null;
        if (stream is not null)
        {
            // Stop waits for the sound thread to finish its last block. If the microphone
            // was unplugged this can fail; we still want to close the file below.
            try { stream.Stop(); } catch (PortAudioException) { }
            try { stream.Dispose(); } catch (PortAudioException) { }
        }
        StopRecording();
        _callback = null;
    }

    /// <summary>The loudest moment since the last call (0..1), for the level meter.</summary>
    public float TakePeak()
    {
        lock (_gate)
        {
            var peak = _peak;
            _peak = 0;
            return peak;
        }
    }

    /// <summary>A problem that happened on the sound thread (e.g. disk full), or null. Reading it clears it.</summary>
    public string? TakeError()
    {
        lock (_gate)
        {
            var error = _error;
            _error = null;
            return error;
        }
    }

    /// <summary>Called by PortAudio on its own thread with each new block of sound.</summary>
    private StreamCallbackResult OnAudio(IntPtr input, IntPtr output, uint frameCount,
        ref StreamCallbackTimeInfo timeInfo, StreamCallbackFlags statusFlags, IntPtr userData)
    {
        // An exception must never escape into PortAudio (it would crash the app), so catch everything.
        try
        {
            if (input == IntPtr.Zero || frameCount == 0) return StreamCallbackResult.Continue;

            var count = (int)frameCount;
            if (_block.Length < count) _block = new float[count];
            Marshal.Copy(input, _block, 0, count);
            var samples = _block.AsSpan(0, count);
            var peak = AudioLevel.Peak(samples);

            lock (_gate)
            {
                if (peak > _peak) _peak = peak;
                if (_writer is null || _paused) return StreamCallbackResult.Continue;

                _writer.Write(samples);
                if (_writer.Duration - _headerUpdatedAt >= HeaderInterval)
                {
                    _writer.UpdateHeader();
                    _headerUpdatedAt = _writer.Duration;
                }
            }
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _error = ex.Message;
                _paused = true; // don't keep hammering a full disk; the window will stop and report
            }
        }
        return StreamCallbackResult.Continue;
    }

    public void Dispose() => Close();
}
