using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Churchie.App.Services;
using Churchie.App.ViewModels;
using Churchie.App.Views;
using Churchie.Core.Services;

namespace Churchie.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainWindowViewModel(new AppPaths());
            var window = new MainWindow();
            viewModel.Dialogs = new DialogService(window);
            window.DataContext = viewModel;

            desktop.MainWindow = window;
            // Closing the control window closes the projector output too.
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
