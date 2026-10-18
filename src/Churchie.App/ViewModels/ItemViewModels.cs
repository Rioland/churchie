using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Churchie.Core.Models;

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

    public string KindLabel => Item.Kind switch
    {
        ScheduleItemKind.Song => "SONG",
        ScheduleItemKind.Scripture => "BIBLE",
        ScheduleItemKind.Text => "TEXT",
        _ => "IMAGE",
    };

    public IBrush KindBrush => Item.Kind switch
    {
        ScheduleItemKind.Song => new SolidColorBrush(Color.Parse("#5B4BDB")),
        ScheduleItemKind.Scripture => new SolidColorBrush(Color.Parse("#B7791F")),
        ScheduleItemKind.Text => new SolidColorBrush(Color.Parse("#2F855A")),
        _ => new SolidColorBrush(Color.Parse("#2B6CB0")),
    };

    /// <summary>Call after changing <see cref="Item"/> so the list redraws.</summary>
    public void Refresh() => OnPropertyChanged(nameof(Title));
}

/// <summary>A picture in the Media tab.</summary>
public sealed class MediaItemViewModel : ViewModelBase
{
    public MediaItemViewModel(string filePath)
    {
        FilePath = filePath;
        try
        {
            using var stream = File.OpenRead(filePath);
            Thumbnail = Bitmap.DecodeToWidth(stream, 240);
        }
        catch
        {
            Thumbnail = null; // unreadable image — still listed, just without a preview
        }
    }

    public string FilePath { get; }
    public string Name => Path.GetFileName(FilePath);
    public Bitmap? Thumbnail { get; }
}
