using Avalonia.Controls;
using Churchie.App.ViewModels;

namespace Churchie.App.Views;

public partial class SongEditorWindow : Window
{
    // Needed by the XAML loader and the designer.
    public SongEditorWindow()
    {
        InitializeComponent();
    }

    public SongEditorWindow(SongEditorViewModel viewModel) : this()
    {
        DataContext = viewModel;
        // The view model decides when to close; the result goes back to ShowDialog.
        viewModel.CloseRequested += result => Close(result);
    }
}
