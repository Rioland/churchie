using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Churchie.App.Playback;
using Churchie.Core.Models;
using Churchie.Core.Services;

namespace Churchie.App.ViewModels;

/// <summary>One slide thumbnail in the Preview or Live panel.</summary>
public partial class SlideItemViewModel : ViewModelBase
{
    public SlideItemViewModel(Slide slide, int number, SlideTheme appearance)
    {
        Slide = slide;
        Number = number;
        Appearance = appearance;
    }

    public Slide Slide { get; }
    public int Number { get; }

    public string Caption => string.IsNullOrEmpty(Slide.Label) ? Number.ToString() : $"{Number}. {Slide.Label}";

    // [ObservableProperty] writes the "Appearance" property for us, including the
    // change notification, from this private field.
    [ObservableProperty]
    private SlideTheme _appearance = new();
}

/// <summary>One row in the service schedule.</summary>
public sealed class ScheduleEntryViewModel : ViewModelBase
{
    public ScheduleEntryViewModel(ScheduleItem item)
    {
        Item = item;
    }

    public ScheduleItem Item { get; }

    public string Title => Item.Title;

    private bool IsVideo => Item.Kind == ScheduleItemKind.Media && MediaFiles.IsVideo(Item.MediaPath);

    public string KindLabel => Item.Kind switch
    {
        ScheduleItemKind.Song => "SONG",
        ScheduleItemKind.Scripture => "BIBLE",
        ScheduleItemKind.Text => "TEXT",
        _ => IsVideo ? "VIDEO" : "IMAGE",
    };

    public IBrush KindBrush => Item.Kind switch
    {
        ScheduleItemKind.Song => new SolidColorBrush(Color.Parse("#5B4BDB")),
        ScheduleItemKind.Scripture => new SolidColorBrush(Color.Parse("#B7791F")),
        ScheduleItemKind.Text => new SolidColorBrush(Color.Parse("#2F855A")),
        _ => new SolidColorBrush(Color.Parse(IsVideo ? "#2C7A7B" : "#2B6CB0")),
    };

    /// <summary>Call after changing <see cref="Item"/> so the list redraws.</summary>
    public void Refresh() => OnPropertyChanged(nameof(Title));
}

/// <summary>A picture, animated GIF or video in the Media tab.</summary>
public sealed partial class MediaItemViewModel : ViewModelBase
{
    /// <param name="name">The name to show (the user's own name for it, or the file name).</param>
    public MediaItemViewModel(string filePath, string? name = null)
    {
        FilePath = filePath;
        _name = name ?? Path.GetFileName(filePath);
        IsVideo = MediaFiles.IsVideo(filePath);

        if (IsVideo) _ = LoadPosterAsync(); // takes a moment, so the thumbnail appears when ready
        else Thumbnail = LoadPicture(filePath);
    }

    public string FilePath { get; }
    public bool IsVideo { get; }

    /// <summary>The name shown under the thumbnail; "Rename" changes it.</summary>
    [ObservableProperty] private string _name;

    /// <summary>A small label on the thumbnail ("VIDEO" or "GIF"), or "" for an ordinary picture.</summary>
    public string Badge =>
        IsVideo ? "VIDEO" : string.Equals(Path.GetExtension(FilePath), ".gif", StringComparison.OrdinalIgnoreCase) ? "GIF" : "";

    public bool HasBadge => Badge.Length > 0;

    [ObservableProperty] private Bitmap? _thumbnail;

    private static Bitmap? LoadPicture(string filePath)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            return Bitmap.DecodeToWidth(stream, 240);
        }
        catch
        {
            return null; // unreadable image — still listed, just without a preview
        }
    }

    private async Task LoadPosterAsync()
    {
        try
        {
            Thumbnail = await VideoLibrary.GetPosterAsync(FilePath);
        }
        catch
        {
            // No poster (e.g. VLC missing): the "VIDEO" label still shows what it is.
        }
    }
}
