using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Churchie.App.ViewModels;
using Churchie.App.Views;

namespace Churchie.App.Services;

/// <summary>Shows file pickers and pop-up windows on top of the main window.</summary>
public sealed class DialogService : IDialogService
{
    private readonly Window _owner;

    public DialogService(Window owner)
    {
        _owner = owner;
    }

    public async Task<IReadOnlyList<string>> PickFilesAsync(string title, string filterName, IReadOnlyList<string> patterns, bool allowMultiple)
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = allowMultiple,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(filterName) { Patterns = patterns },
                FilePickerFileTypes.All,
            },
        });

        var paths = new List<string>();
        foreach (var file in files)
        {
            var path = file.TryGetLocalPath();
            if (path is not null) paths.Add(path);
        }
        return paths;
    }

    public async Task<string?> PickSaveFileAsync(string title, string suggestedName, string filterName, string extension)
    {
        var file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            FileTypeChoices = new[] { new FilePickerFileType(filterName) { Patterns = new[] { "*" + extension } } },
        });
        return file?.TryGetLocalPath();
    }

    public async Task<SongEditorResult?> EditSongAsync(SongEditorResult initial, string windowTitle, bool showSongFields)
    {
        var viewModel = new SongEditorViewModel(initial, windowTitle, showSongFields);
        var window = new SongEditorWindow(viewModel);
        return await window.ShowDialog<SongEditorResult?>(_owner);
    }

    public async Task<bool> ConfirmAsync(string title, string message, string yes = "Yes")
    {
        var dialog = SimpleDialog(title, message);
        var yesButton = new Button { Content = yes, IsDefault = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        var noButton = new Button { Content = "Cancel", IsCancel = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        yesButton.Click += (_, _) => dialog.Close(true);
        noButton.Click += (_, _) => dialog.Close(false);
        AddButtons(dialog, noButton, yesButton);
        return await dialog.ShowDialog<bool>(_owner);
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var dialog = SimpleDialog(title, message);
        var ok = new Button { Content = "OK", IsDefault = true, IsCancel = true, MinWidth = 90, HorizontalContentAlignment = HorizontalAlignment.Center };
        ok.Click += (_, _) => dialog.Close();
        AddButtons(dialog, ok);
        await dialog.ShowDialog(_owner);
    }

    private static Window SimpleDialog(string title, string message) => new()
    {
        Title = title,
        Width = 460,
        SizeToContent = SizeToContent.Height,
        CanResize = false,
        ShowInTaskbar = false,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(22),
            Spacing = 18,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            },
        },
    };

    private static void AddButtons(Window dialog, params Button[] buttons)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        foreach (var b in buttons) row.Children.Add(b);
        ((StackPanel)dialog.Content!).Children.Add(row);
    }
}
