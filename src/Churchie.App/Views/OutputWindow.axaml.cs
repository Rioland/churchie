using Avalonia.Controls;
using Avalonia.Input;

namespace Churchie.App.Views;

/// <summary>The window shown on the projector / TV.</summary>
public partial class OutputWindow : Window
{
    public OutputWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
        DoubleTapped += (_, _) => ToggleFullScreen();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Escape closes the projector window; F11 toggles full screen.
        if (e.Key == Key.Escape) Close();
        else if (e.Key == Key.F11) ToggleFullScreen();
    }

    private void ToggleFullScreen() =>
        WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;
}
