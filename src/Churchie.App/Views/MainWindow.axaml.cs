using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Churchie.App.ViewModels;
using Churchie.Core.Models;

namespace Churchie.App.Views;

/// <summary>
/// Code-behind for the main window. Almost everything lives in the view model;
/// this file only handles things that need the actual window: keyboard shortcuts,
/// double-clicks, and opening the projector window on the right monitor.
/// </summary>
public partial class MainWindow : Window
{
    private OutputWindow? _output;
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        // "Tunnel" means we see key presses before the focused control does.
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);

        Opened += (_, _) => RefreshScreens();
        Closing += (_, _) => _output?.Close();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null) _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.IsOutputOpen) || _viewModel is null) return;
        if (_viewModel.IsOutputOpen) OpenOutput();
        else CloseOutput();
    }

    // ---------------------------------------------------------------
    // Projector output
    // ---------------------------------------------------------------

    private void RefreshScreens()
    {
        if (_viewModel is null) return;
        var names = Screens.All.Select((screen, i) =>
            $"Screen {i + 1} — {screen.Bounds.Width}×{screen.Bounds.Height}{(screen.IsPrimary ? " (main)" : "")}");
        _viewModel.SetScreens(names);
    }

    private void OpenOutput()
    {
        if (_output is not null || _viewModel is null) return;
        RefreshScreens();

        var screens = Screens.All;
        if (screens.Count == 0) return;
        var index = Math.Clamp(_viewModel.PreferredScreenIndex, 0, screens.Count - 1);
        var target = screens[index];

        var window = new OutputWindow { DataContext = _viewModel };
        window.WindowStartupLocation = WindowStartupLocation.Manual;

        if (screens.Count > 1)
        {
            // A real second screen: borderless and full screen on it.
            window.SystemDecorations = SystemDecorations.None;
            window.Position = target.Bounds.Position;
            window.Show();
            window.WindowState = WindowState.FullScreen;
        }
        else
        {
            // Only one screen: a normal window, so the operator can still see this one.
            window.Width = 960;
            window.Height = 540;
            window.Position = new PixelPoint(target.WorkingArea.X + 60, target.WorkingArea.Y + 60);
            window.Show();
        }

        window.Closed += (_, _) =>
        {
            _output = null;
            if (_viewModel is not null) _viewModel.IsOutputOpen = false;
        };
        _output = window;
        Activate(); // keep keyboard focus on the control window
    }

    private void CloseOutput()
    {
        var window = _output;
        _output = null;
        window?.Close();
    }

    // ---------------------------------------------------------------
    // Keyboard
    // ---------------------------------------------------------------

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel is not { } vm) return;
        var focused = FocusManager?.GetFocusedElement();
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);

        // Shortcuts that work everywhere
        if (ctrl)
        {
            switch (e.Key)
            {
                case Key.S: vm.SaveScheduleCommand.Execute(null); e.Handled = true; return;
                case Key.O: vm.OpenScheduleCommand.Execute(null); e.Handled = true; return;
                case Key.N: vm.NewScheduleCommand.Execute(null); e.Handled = true; return;
                case Key.Enter: vm.GoLiveCommand.Execute(null); e.Handled = true; return;
            }
            return;
        }

        // Enter in the scripture box looks the passage up
        if (e.Key == Key.Enter && focused == ReferenceBox)
        {
            vm.FindScriptureCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Presentation clickers send Page Down / Page Up
        if (e.Key is Key.PageDown or Key.PageUp && focused is not TextBox)
        {
            if (e.Key == Key.PageDown) vm.NextSlideCommand.Execute(null);
            else vm.PreviousSlideCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Don't steal letters or arrows while someone is typing or picking from a list
        if (focused is TextBox or ComboBox) return;

        switch (e.Key)
        {
            case Key.B: vm.IsBlack = !vm.IsBlack; e.Handled = true; return;
            case Key.C: vm.IsCleared = !vm.IsCleared; e.Handled = true; return;
            case Key.L: vm.IsLogo = !vm.IsLogo; e.Handled = true; return;
        }

        // Arrows and Space drive the live slides when the Live panel has focus
        // (or nothing does), and keep their normal meaning everywhere else.
        var inLivePanel = focused is null || focused == this ||
                          (focused is Visual visual && (visual == LivePanel || LivePanel.IsVisualAncestorOf(visual)));
        if (!inLivePanel) return;

        switch (e.Key)
        {
            case Key.Right or Key.Down or Key.Space:
                vm.NextSlideCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Left or Key.Up:
                vm.PreviousSlideCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    // ---------------------------------------------------------------
    // Double-clicks
    // ---------------------------------------------------------------

    private void OnPreviewSlideDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as StyledElement)?.DataContext is SlideItemViewModel slide)
            _viewModel?.GoLiveAt(slide);
    }

    private void OnScheduleDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as StyledElement)?.DataContext is ScheduleEntryViewModel entry)
            _viewModel?.GoLiveWith(entry);
    }

    private void OnSongDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as StyledElement)?.DataContext is Song song)
            _viewModel?.AddSongToScheduleFromList(song);
    }
}
