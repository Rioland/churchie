using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Threading;
using Churchie.Core.Models;

namespace Churchie.App.Controls;

/// <summary>
/// Shows what's live, with a smooth cross-fade whenever it changes: going live,
/// moving to the next verse, switching items, Black / Clear / Logo.
///
/// How the fade works: there are two <see cref="SlideView"/> layers stacked on
/// top of each other. The new slide goes into the hidden layer, which is brought
/// to the front and faded in over the old one. Because the old slide stays fully
/// visible underneath until the new one covers it, the background never dips
/// darker halfway through (which happens if you fade one out while the other fades in).
/// </summary>
public sealed class LiveSlideView : UserControl
{
    public static readonly StyledProperty<LiveFrame?> FrameProperty =
        AvaloniaProperty.Register<LiveSlideView, LiveFrame?>(nameof(Frame));

    public static readonly StyledProperty<TimeSpan> TransitionDurationProperty =
        AvaloniaProperty.Register<LiveSlideView, TimeSpan>(nameof(TransitionDuration), TimeSpan.FromSeconds(0.5));

    /// <summary>What to show. Setting a new frame starts the transition.</summary>
    public LiveFrame? Frame
    {
        get => GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    /// <summary>How long the fade takes. Zero means "cut" (change instantly).</summary>
    public TimeSpan TransitionDuration
    {
        get => GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    private readonly SlideView[] _layers = { new SlideView(), new SlideView { Opacity = 0 } };
    private int _front;            // which of the two layers is currently in front
    private int _fadeNumber;       // lets a finished fade know whether a newer one has started
    private IDisposable? _cleanup; // timer that hides the old layer after a fade

    public LiveSlideView()
    {
        var stack = new Grid { ClipToBounds = true };
        foreach (var layer in _layers) stack.Children.Add(layer);
        Content = stack;
        ClipToBounds = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == FrameProperty)
            Show(change.OldValue as LiveFrame, change.NewValue as LiveFrame);
    }

    private void Show(LiveFrame? previous, LiveFrame? next)
    {
        next ??= LiveFrame.Black();
        var front = _layers[_front];
        var back = _layers[1 - _front];

        // Same words, new look (theme being edited): no fade, just restyle.
        if (next.HasSameContentAs(previous))
        {
            front.Appearance = next.Theme;
            return;
        }

        _cleanup?.Dispose();
        var duration = TransitionDuration;

        if (duration <= TimeSpan.Zero)
        {
            // "Cut": replace the picture instantly.
            Place(front, next, opacity: 1);
            back.Transitions = null;
            back.Opacity = 0;
            return;
        }

        // 1. Put the new slide on the hidden layer, fully transparent.
        Place(back, next, opacity: 0);

        // 2. Bring it to the front. The old slide stays fully visible underneath.
        back.ZIndex = 1;
        front.ZIndex = 0;
        front.Transitions = null;
        front.Opacity = 1;

        // 3. Fade it in. Setting Transitions first makes the Opacity change animate.
        back.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = OpacityProperty,
                Duration = duration,
                Easing = new SineEaseInOut(),
            },
        };
        back.Opacity = 1;
        _front = 1 - _front;

        // 4. When the fade is done, hide the old layer so it isn't drawn for nothing.
        var thisFade = ++_fadeNumber;
        _cleanup = DispatcherTimer.RunOnce(() =>
        {
            if (thisFade == _fadeNumber) front.Opacity = 0;
        }, duration + TimeSpan.FromMilliseconds(50));
    }

    /// <summary>Shows a frame on a layer immediately (no animation).</summary>
    private static void Place(SlideView layer, LiveFrame frame, double opacity)
    {
        layer.Transitions = null;
        layer.Opacity = opacity;
        layer.Slide = frame.Slide;
        layer.Appearance = frame.Theme;
    }
}
