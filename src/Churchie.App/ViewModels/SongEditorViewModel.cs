using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Churchie.App.Services;
using Churchie.Core.Services;

namespace Churchie.App.ViewModels;

/// <summary>The song (or text slide) editor window.</summary>
public partial class SongEditorViewModel : ViewModelBase
{
    public SongEditorViewModel(SongEditorResult initial, string windowTitle, bool showSongFields)
    {
        WindowTitle = windowTitle;
        ShowSongFields = showSongFields;
        Title = initial.Title;
        Author = initial.Author;
        Copyright = initial.Copyright;
        Ccli = initial.Ccli;
        Lyrics = initial.Lyrics;
    }

    /// <summary>Raised when the window should close. Null means "cancelled".</summary>
    public event Action<SongEditorResult?>? CloseRequested;

    public string WindowTitle { get; }
    public bool ShowSongFields { get; }

    public string LyricsHint => ShowSongFields
        ? "Paste or type the lyrics. Leave a blank line between slides.\nName a section by putting [Verse 1], [Chorus], [Bridge]... on the line above it."
        : "Type the text. Leave a blank line between slides.";

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _author = "";
    [ObservableProperty] private string _copyright = "";
    [ObservableProperty] private string _ccli = "";
    [ObservableProperty] private string _error = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SectionSummary))]
    private string _lyrics = "";

    /// <summary>A live summary like "4 slides: Verse 1 · Chorus · Verse 2 · Chorus".</summary>
    public string SectionSummary
    {
        get
        {
            if (!ShowSongFields)
            {
                var count = SlideBuilder.FromText(Lyrics).Count;
                return $"{count} slide{(count == 1 ? "" : "s")}";
            }

            var sections = SongParser.Parse(Lyrics);
            if (sections.Count == 0) return "No slides yet";
            return $"{sections.Count} slide{(sections.Count == 1 ? "" : "s")}: " + string.Join(" · ", sections.Select(s => s.Label));
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            Error = "Please give it a title.";
            return;
        }

        CloseRequested?.Invoke(new SongEditorResult(
            Title.Trim(), Author.Trim(), Copyright.Trim(), Ccli.Trim(), Lyrics ?? ""));
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(null);
}
