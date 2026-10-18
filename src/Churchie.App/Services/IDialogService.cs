namespace Churchie.App.Services;

/// <summary>What the song editor window returns when the user presses Save.</summary>
public sealed record SongEditorResult(string Title, string Author, string Copyright, string Ccli, string Lyrics);

/// <summary>
/// Everything a view model needs from windows and pop-ups. View models talk to this
/// interface instead of to windows directly, which keeps them easy to test.
/// </summary>
public interface IDialogService
{
    Task<IReadOnlyList<string>> PickFilesAsync(string title, string filterName, IReadOnlyList<string> patterns, bool allowMultiple);

    Task<string?> PickSaveFileAsync(string title, string suggestedName, string filterName, string extension);

    /// <param name="showSongFields">False for plain text slides (hides author, copyright...).</param>
    Task<SongEditorResult?> EditSongAsync(SongEditorResult initial, string windowTitle, bool showSongFields);

    Task<bool> ConfirmAsync(string title, string message, string yes = "Yes");

    Task ShowMessageAsync(string title, string message);
}
