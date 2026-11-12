using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Churchie.App.Services;
using Churchie.Core.Audio;

namespace Churchie.App.ViewModels;

/// <summary>One saved recording in the list on the Record tab.</summary>
public sealed class RecordingItemViewModel
{
    public RecordingItemViewModel(RecordingFile file)
    {
        FilePath = file.Path;
        Name = file.Name;
        var length = file.Duration is { } d ? RecordingFiles.FormatDuration(d) : "unknown length";
        Details = $"{length}  ·  {RecordingFiles.FormatSize(file.Size)}  ·  {file.Modified:d MMM yyyy, HH:mm}";
    }

    public string FilePath { get; }
    public string Name { get; }
    public string Details { get; }
}

// The Record tab: record a sermon or message from a microphone into a sound file.
// The actual sound work is in Services/AudioRecorder.cs; this file is the buttons and labels.
public partial class MainWindowViewModel
{
    private readonly AudioRecorder _recorder = new();

    // Updates the level meter and the clock about 12 times a second while a microphone is open.
    private DispatcherTimer? _recordingTimer;
    private int _clipHold; // keeps the red "too loud" warning visible for a moment

    public ObservableCollection<AudioInputDevice> Microphones { get; } = new();
    public ObservableCollection<RecordingItemViewModel> Recordings { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand), nameof(CheckLevelCommand))]
    private AudioInputDevice? _selectedMicrophone;

    /// <summary>True while a recording is being made (also while it is paused).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand), nameof(PauseRecordingCommand), nameof(StopRecordingCommand), nameof(CheckLevelCommand))]
    [NotifyPropertyChangedFor(nameof(CanChooseMicrophone))]
    private bool _isRecording;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PauseButtonText), nameof(RecordingBadge))]
    private bool _isRecordingPaused;

    /// <summary>True while the microphone is open only to watch the level (nothing is saved).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CheckLevelText), nameof(CanChooseMicrophone))]
    private bool _isCheckingLevel;

    /// <summary>The level meter, from 0 (empty) to 1 (full).</summary>
    [ObservableProperty] private double _recordingLevel;

    /// <summary>True when the sound is so loud it is distorting.</summary>
    [ObservableProperty] private bool _isClipping;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordingBadge))]
    private string _recordingTime = "0:00";

    [ObservableProperty] private string _recordingStatus = "Looking for microphones…";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PlayRecordingCommand), nameof(DeleteRecordingCommand))]
    private RecordingItemViewModel? _selectedRecording;

    public bool CanChooseMicrophone => !IsRecording && !IsCheckingLevel;
    public string PauseButtonText => IsRecordingPaused ? "Resume" : "Pause";
    public string CheckLevelText => IsCheckingLevel ? "Stop checking" : "Check level";

    /// <summary>Shown in the status bar on every tab, so the operator always knows a recording is running.</summary>
    public string RecordingBadge => (IsRecordingPaused ? "❚❚ PAUSED " : "● REC ") + RecordingTime;

    // ------------------------------------------------------------------
    // Microphones
    // ------------------------------------------------------------------

    /// <summary>Finds the microphones without holding up the window (it can take a second on Windows).</summary>
    private async Task LoadMicrophonesAsync(bool rescan = false)
    {
        try
        {
            var found = await Task.Run(() => AudioRecorder.ListDevices(rescan));

            Microphones.Clear();
            foreach (var device in found) Microphones.Add(device);

            // Prefer the microphone used last time, otherwise the computer's default (listed first).
            SelectedMicrophone = Microphones.FirstOrDefault(d => d.Name == _settings.RecordingDevice)
                                 ?? Microphones.FirstOrDefault();
            RecordingStatus = Microphones.Count == 0
                ? "No microphone found. Plug one in, then press Refresh."
                : "Ready. Press Check level to test the microphone, or Record to start.";
        }
        catch (Exception ex)
        {
            // For example the sound library is missing from the install.
            RecordingStatus = "Recording isn't available on this computer: " + ex.Message;
        }
    }

    partial void OnSelectedMicrophoneChanged(AudioInputDevice? value)
    {
        if (value is null || _loading) return;
        // Remember the choice by name: the position in the list changes when devices are plugged in.
        if (_settings.RecordingDevice == value.Name) return;
        _settings.RecordingDevice = value.Name;
        SaveSettings();
    }

    [RelayCommand]
    private Task RefreshMicrophonesAsync()
    {
        if (!CanChooseMicrophone) return Task.CompletedTask; // can't rescan while a microphone is in use
        RecordingStatus = "Looking for microphones…";
        return LoadMicrophonesAsync(rescan: true);
    }

    // ------------------------------------------------------------------
    // Check level / Record / Pause / Stop
    // ------------------------------------------------------------------

    // True for the moment between pressing a button and the microphone being open.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartRecordingCommand), nameof(CheckLevelCommand))]
    private bool _isOpeningMicrophone;

    private bool CanUseMicrophone() => SelectedMicrophone is not null && !IsRecording && !IsOpeningMicrophone;

    /// <summary>Opens the microphone just to watch the meter, so the level can be set before the service.</summary>
    [RelayCommand(CanExecute = nameof(CanUseMicrophone))]
    private async Task CheckLevelAsync()
    {
        if (IsCheckingLevel)
        {
            CloseMicrophone();
            RecordingStatus = "Ready.";
            return;
        }

        if (!await OpenMicrophoneAsync()) return;
        IsCheckingLevel = true;
        RecordingStatus = "Listening (not recording). Speak: the bar should reach about three-quarters and never turn red.";
    }

    [RelayCommand(CanExecute = nameof(CanUseMicrophone))]
    private async Task StartRecordingAsync()
    {
        if (!await OpenMicrophoneAsync()) return; // does nothing if "Check level" already opened it

        try
        {
            Directory.CreateDirectory(_paths.Recordings);
            _recorder.StartRecording(RecordingFiles.NewFilePath(_paths.Recordings, DateTime.Now));
        }
        catch (Exception ex)
        {
            CloseMicrophone();
            RecordingStatus = "Couldn't start recording: " + ex.Message;
            return;
        }

        IsCheckingLevel = false;
        IsRecordingPaused = false;
        IsRecording = true;
        RecordingTime = "0:00";
        RecordingStatus = "Recording…";
        StatusMessage = "Recording started";
    }

    [RelayCommand(CanExecute = nameof(IsRecording))]
    private void PauseRecording()
    {
        if (IsRecordingPaused) _recorder.Resume(); else _recorder.Pause();
        IsRecordingPaused = !IsRecordingPaused;
        RecordingStatus = IsRecordingPaused ? "Paused. Press Resume to carry on in the same file." : "Recording…";
    }

    [RelayCommand(CanExecute = nameof(IsRecording))]
    private void StopRecording() => FinishRecording(problem: null);

    /// <summary>Closes the file and the microphone, then shows the new recording in the list.</summary>
    private void FinishRecording(string? problem)
    {
        var length = _recorder.Duration;
        var path = _recorder.FilePath;
        CloseMicrophone();

        IsRecording = false;
        IsRecordingPaused = false;
        RefreshRecordings(select: path);

        var saved = path is null ? "" : $"Saved {Path.GetFileName(path)} ({RecordingFiles.FormatDuration(length)}).";
        RecordingStatus = problem is null ? saved : $"Recording stopped: {problem} {saved}";
        StatusMessage = problem is null ? "Recording saved" : "Recording stopped: " + problem;
    }

    /// <summary>
    /// Opens the microphone on a background thread. The first time, macOS and Windows may
    /// ask the user for permission, and that can take as long as the user takes to answer;
    /// doing it in the background keeps the window working meanwhile.
    /// </summary>
    private async Task<bool> OpenMicrophoneAsync()
    {
        if (SelectedMicrophone is not { } microphone) return false;
        if (_recorder.IsOpen) return true;

        IsOpeningMicrophone = true;
        RecordingStatus = "Opening the microphone… If the computer asks whether Churchie may use it, choose Allow.";
        try
        {
            await Task.Run(() => _recorder.Open(microphone));
        }
        catch (Exception ex)
        {
            RecordingStatus = "Couldn't open the microphone: " + ex.Message;
            return false;
        }
        finally
        {
            IsOpeningMicrophone = false;
        }

        _recordingTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(80), DispatcherPriority.Background, (_, _) => OnRecordingTick());
        _recordingTimer.Start();
        return true;
    }

    private void CloseMicrophone()
    {
        _recordingTimer?.Stop();
        try { _recorder.Close(); }
        catch (Exception ex) { StatusMessage = "Problem closing the microphone: " + ex.Message; }
        IsCheckingLevel = false;
        RecordingLevel = 0;
        IsClipping = false;
    }

    private void OnRecordingTick()
    {
        // The meter jumps up at once but falls back slowly, which is easier to read than a flickering bar.
        var peak = _recorder.TakePeak();
        RecordingLevel = Math.Max(AudioLevel.ToMeter(peak), RecordingLevel - 0.04);

        if (AudioLevel.IsClipping(peak)) _clipHold = 12; // about one second
        else if (_clipHold > 0) _clipHold--;
        IsClipping = _clipHold > 0;

        if (!IsRecording) return;

        RecordingTime = RecordingFiles.FormatDuration(_recorder.Duration);
        if (!IsRecordingPaused)
            RecordingStatus = $"Recording…  {RecordingFiles.FormatSize(_recorder.FileSize)}" + (IsClipping ? "   Too loud: turn the microphone down a little." : "");

        // Something went wrong on the sound thread (most likely the disk is full).
        if (_recorder.TakeError() is { } error) FinishRecording(error);
    }

    /// <summary>Called when the window closes, so a running recording is saved properly.</summary>
    public void ShutdownRecording()
    {
        if (IsRecording) FinishRecording(problem: null);
        else CloseMicrophone();
        AudioRecorder.Shutdown();
    }

    // ------------------------------------------------------------------
    // The list of saved recordings
    // ------------------------------------------------------------------

    private void RefreshRecordings(string? select = null)
    {
        Recordings.Clear();
        try
        {
            foreach (var file in RecordingFiles.List(_paths.Recordings))
                Recordings.Add(new RecordingItemViewModel(file));
        }
        catch (Exception ex)
        {
            RecordingStatus = "Couldn't read the recordings folder: " + ex.Message;
        }
        if (select is not null)
            SelectedRecording = Recordings.FirstOrDefault(r => r.FilePath == select);
    }

    private bool HasSelectedRecording() => SelectedRecording is not null;

    /// <summary>Plays the recording in the computer's own music player.</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedRecording))]
    private void PlayRecording()
    {
        if (SelectedRecording is not { } recording) return;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // On Windows, "starting" a document opens it in its usual program.
                Process.Start(new ProcessStartInfo(recording.FilePath) { UseShellExecute = true });
            }
            else
            {
                var start = new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open") { UseShellExecute = false };
                start.ArgumentList.Add(recording.FilePath);
                Process.Start(start);
            }
        }
        catch (Exception ex)
        {
            RecordingStatus = "Couldn't open the recording: " + ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedRecording))]
    private async Task DeleteRecordingAsync()
    {
        if (SelectedRecording is not { } recording || Dialogs is null) return;
        if (!await Dialogs.ConfirmAsync("Delete recording", $"Delete \"{recording.Name}\"? This can't be undone.", "Delete")) return;

        try
        {
            File.Delete(recording.FilePath);
            RefreshRecordings();
            RecordingStatus = $"Deleted {recording.Name}.";
        }
        catch (Exception ex)
        {
            RecordingStatus = "Couldn't delete it: " + ex.Message;
        }
    }

    [RelayCommand]
    private void OpenRecordingsFolder() => OpenFolder(_paths.Recordings);
}
