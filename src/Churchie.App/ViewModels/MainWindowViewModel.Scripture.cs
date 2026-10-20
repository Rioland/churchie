using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Churchie.Core.Bible;
using Churchie.Core.Models;

namespace Churchie.App.ViewModels;

// The Scripture tab.
public partial class MainWindowViewModel
{
    public ObservableCollection<BibleTranslation> Bibles { get; } = new();
    public ObservableCollection<Verse> ScriptureVerses { get; } = new();

    [ObservableProperty] private BibleTranslation? _selectedBible;
    [ObservableProperty] private IReadOnlyList<BibleBook> _books = Array.Empty<BibleBook>();
    [ObservableProperty] private BibleBook? _selectedBook;
    [ObservableProperty] private IReadOnlyList<int> _chapters = Array.Empty<int>();
    [ObservableProperty] private int _selectedChapterIndex = -1;
    [ObservableProperty] private string _referenceText = "";
    [ObservableProperty] private string _scriptureStatus = "Type a reference like John 3:16-18 and press Enter, or pick a book and chapter.";

    private Passage? _passage;

    partial void OnSelectedBibleChanged(BibleTranslation? value)
    {
        var book = SelectedBook?.Number;
        Books = value?.Books ?? (IReadOnlyList<BibleBook>)Array.Empty<BibleBook>();
        SelectedBook = book is null ? null : value?.GetBook(book.Value);

        if (value is not null && !_loading)
        {
            _settings.DefaultBible = value.Abbreviation;
            SaveSettings();
            if (!string.IsNullOrWhiteSpace(ReferenceText)) FindScripture();
        }
    }

    partial void OnSelectedBookChanged(BibleBook? value)
    {
        Chapters = value is null ? Array.Empty<int>() : Enumerable.Range(1, value.ChapterCount).ToArray();
    }

    partial void OnSelectedChapterIndexChanged(int value)
    {
        if (SelectedBook is null || value < 0 || _loading) return;
        ReferenceText = $"{SelectedBook.Name} {value + 1}";
        FindScripture();
    }

    [RelayCommand]
    private void FindScripture()
    {
        var passage = _bibles.Lookup(SelectedBible?.Abbreviation, ReferenceText, out var error);
        ScriptureVerses.Clear();

        if (passage is null)
        {
            _passage = null;
            ScriptureStatus = error ?? "Not found";
            return;
        }

        _passage = passage;
        ScriptureStatus = $"{passage.Title} ({passage.Bible.Abbreviation}) — {passage.Verses.Count} verse{(passage.Verses.Count == 1 ? "" : "s")}";
        foreach (var verse in passage.Verses) ScriptureVerses.Add(verse);
        ShowInPreview(ScheduleItem.ForScripture(passage.Bible.Abbreviation, passage.Title));
    }

    [RelayCommand]
    private void AddScriptureToSchedule()
    {
        FindScripture();
        if (_passage is not null)
            AddToSchedule(ScheduleItem.ForScripture(_passage.Bible.Abbreviation, _passage.Title));
    }

    [RelayCommand]
    private void ScriptureGoLive()
    {
        FindScripture();
        if (_passage is not null)
            SendLive(ScheduleItem.ForScripture(_passage.Bible.Abbreviation, _passage.Title), 0);
    }

    [RelayCommand]
    private void OpenBiblesFolder() => OpenFolder(_paths.Bibles);

    [RelayCommand]
    private Task ReloadBiblesAsync() => LoadBiblesAsync();
}
