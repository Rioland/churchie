using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Churchie.App.Playback;
using Churchie.Core.Services;

namespace Churchie.App.Controls;

/// <summary>
/// Play/Pause, Restart and a position bar for the video on one screen. It sits under
/// the Preview screen and under the Live screen, and only appears while that screen
/// shows a video (a video slide, or else a video background).
///
/// It finds the video through <see cref="VideoLibrary.Current"/> and checks it four
/// times a second to keep the button and the bar up to date.
/// </summary>
public sealed class VideoControls : UserControl
{
    public static readonly StyledProperty<string> ChannelProperty =
        AvaloniaProperty.Register<VideoControls, string>(nameof(Channel), VideoLibrary.LiveChannel);

    /// <summary>Which screen's video this controls: "live" or "preview".</summary>
    public string Channel
    {
        get => GetValue(ChannelProperty);
        set => SetValue(ChannelProperty, value);
    }

    private const string PlayText = "▶  Play";
    private const string PauseText = "❚❚  Pause";

    private readonly Button _playPause = new() { Content = PauseText, MinWidth = 96, Margin = new Thickness(0, 0, 4, 0) };
    private readonly Button _restart = new() { Content = "↺  Restart", Margin = new Thickness(0, 0, 10, 0) };
    private readonly Slider _position = new()
    {
        Minimum = 0,
        Maximum = 1,
        SmallChange = 0.01, // arrow keys move 1% of the video
        LargeChange = 0.1,  // Page Up/Down move 10%
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly TextBlock _time = new()
    {
        FontSize = 12,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(10, 0, 0, 0),
    };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };

    private VideoPlayer? _player;
    private bool _updating; // true while we move the bar ourselves (so it isn't taken as the user jumping)
    private bool _dragging; // true while the user holds the bar's handle

    public VideoControls()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto") };
        Grid.SetColumn(_restart, 1);
        Grid.SetColumn(_position, 2);
        Grid.SetColumn(_time, 3);
        row.Children.Add(_playPause);
        row.Children.Add(_restart);
        row.Children.Add(_position);
        row.Children.Add(_time);
        Content = row;
        IsVisible = false;

        ToolTip.SetTip(_playPause, "Pause or play the video");
        ToolTip.SetTip(_restart, "Start the video again from the beginning");

        _playPause.Click += (_, _) =>
        {
            _player?.TogglePause();
            Update();
        };
        _restart.Click += (_, _) => _player?.Restart();

        // Moving the bar jumps in the video — but not when we move it ourselves.
        _position.ValueChanged += (_, e) =>
        {
            if (!_updating) _player?.Seek(e.NewValue);
        };
        // While the handle is held, don't pull it back to where the video currently is.
        _position.AddHandler(PointerPressedEvent, (_, _) => _dragging = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        _position.AddHandler(PointerReleasedEvent, (_, _) => _dragging = false, RoutingStrategies.Tunnel, handledEventsToo: true);

        _timer.Tick += (_, _) => Update();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        VideoLibrary.Changed += OnLibraryChanged;
        FindVideo();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        VideoLibrary.Changed -= OnLibraryChanged;
        _timer.Stop();
        _player = null;
    }

    private void OnLibraryChanged(string channel)
    {
        if (channel == Channel) FindVideo();
    }

    /// <summary>Looks up which video (if any) is on our screen now, and shows or hides the controls.</summary>
    private void FindVideo()
    {
        _player = VideoLibrary.Current(Channel);
        IsVisible = _player is not null;
        if (_player is null)
        {
            _timer.Stop();
            return;
        }
        _timer.Start();
        Update();
    }

    /// <summary>Brings the button, bar and time up to date with the video.</summary>
    private void Update()
    {
        if (_player is not { } player) return;

        _playPause.Content = player.IsPlaying ? PauseText : PlayText;

        if (!_dragging)
        {
            _updating = true;
            _position.Value = player.Position;
            _updating = false;
        }

        var time = MediaTime.Format(player.Time) + " / " + MediaTime.Format(player.Length);
        // The preview never makes sound, so say so (the live screens do).
        _time.Text = Channel == VideoLibrary.LiveChannel ? time : time + "  (silent)";
    }
}
