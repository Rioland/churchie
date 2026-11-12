using System.Text.Json;
using Churchie.Core.Bible;
using Churchie.Core.Models;
using Churchie.Core.Services;

namespace Churchie.Core.Tests;

public class ImageFitTests
{
    [Fact]
    public void Defaults_keep_the_old_behaviour()
    {
        // Backgrounds have always covered the screen; picture slides have always shown the whole picture.
        Assert.Equal(ImageFit.Fill, new SlideTheme().BackgroundFit);
        Assert.Equal(ImageFit.Fit, new AppSettings().PictureFit);
        Assert.Equal(ImageFit.Fit, SlideBuilder.FromImage("/pictures/logo.png").ImageFit);
    }

    [Fact]
    public void Picture_slides_use_the_fit_from_settings()
    {
        var factory = new SlideFactory(new BibleLibrary(), _ => null);
        var settings = new AppSettings { PictureFit = ImageFit.Fill };

        var slide = Assert.Single(factory.Build(ScheduleItem.ForMedia("/pictures/portrait.jpg"), settings));

        Assert.Equal(ImageFit.Fill, slide.ImageFit);
    }

    [Fact]
    public void Settings_saved_before_this_option_existed_still_load()
    {
        // An old settings.json has no "backgroundFit" or "pictureFit" lines.
        var old = """{ "songTheme": { "name": "Songs", "backgroundImage": "/pictures/sunrise.jpg" } }""";

        var settings = JsonSerializer.Deserialize<AppSettings>(old, JsonFile.Options)!;

        Assert.Equal(ImageFit.Fill, settings.SongTheme.BackgroundFit);
        Assert.Equal(ImageFit.Fit, settings.PictureFit);
    }

    [Fact]
    public void Fit_is_saved_as_a_readable_word()
    {
        var json = JsonSerializer.Serialize(new SlideTheme { BackgroundFit = ImageFit.Stretch }, JsonFile.Options);

        Assert.Contains("\"backgroundFit\": \"Stretch\"", json);
    }
}
