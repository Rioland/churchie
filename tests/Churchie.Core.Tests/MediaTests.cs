using System.Text.Json;
using Churchie.Core.Bible;
using Churchie.Core.Models;
using Churchie.Core.Services;

namespace Churchie.Core.Tests;

public class MediaTests
{
    [Theory]
    [InlineData("/pictures/sunrise.jpg", MediaKind.Picture)]
    [InlineData("/pictures/Welcome.PNG", MediaKind.Picture)]
    [InlineData("/pictures/countdown.gif", MediaKind.Picture)]
    [InlineData("/videos/intro.mp4", MediaKind.Video)]
    [InlineData("/videos/Announcements.MOV", MediaKind.Video)]
    [InlineData("/videos/worship loop.webm", MediaKind.Video)]
    [InlineData("/documents/notes.txt", MediaKind.None)]
    [InlineData("/no-extension", MediaKind.None)]
    [InlineData("", MediaKind.None)]
    [InlineData(null, MediaKind.None)]
    public void Files_are_sorted_by_extension(string? path, MediaKind expected)
    {
        Assert.Equal(expected, MediaFiles.KindOf(path));
    }

    [Fact]
    public void Only_gif_and_webp_may_be_animated()
    {
        Assert.True(MediaFiles.MayBeAnimated("/pictures/countdown.GIF"));
        Assert.True(MediaFiles.MayBeAnimated("/pictures/sticker.webp"));
        Assert.False(MediaFiles.MayBeAnimated("/pictures/sunrise.jpg"));
        Assert.False(MediaFiles.MayBeAnimated("/videos/intro.mp4"));
    }

    [Fact]
    public void Dialog_patterns_cover_pictures_and_videos()
    {
        Assert.Contains("*.gif", MediaFiles.AllPatterns);
        Assert.Contains("*.mp4", MediaFiles.AllPatterns);
        Assert.DoesNotContain("*.mp4", MediaFiles.PicturePatterns);
        Assert.Equal(MediaFiles.PicturePatterns.Count + MediaFiles.VideoPatterns.Count, MediaFiles.AllPatterns.Count);
    }

    [Fact]
    public void A_video_file_becomes_a_video_slide()
    {
        var slide = SlideBuilder.FromMedia("/videos/intro.mp4", ImageFit.Fill, loopVideo: true);

        Assert.Equal(SlideKind.Video, slide.Kind);
        Assert.Equal("intro", slide.Label);
        Assert.Equal("/videos/intro.mp4", slide.MediaPath);
        Assert.Equal(ImageFit.Fill, slide.ImageFit);
        Assert.True(slide.Loop);
    }

    [Fact]
    public void A_gif_is_a_picture_slide_and_never_loops_as_a_video()
    {
        // GIFs repeat by themselves; the Loop flag is only for videos.
        var slide = SlideBuilder.FromMedia("/pictures/countdown.gif", loopVideo: true);

        Assert.Equal(SlideKind.Image, slide.Kind);
        Assert.False(slide.Loop);
    }

    [Fact]
    public void Video_slides_follow_the_loop_setting()
    {
        var factory = new SlideFactory(new BibleLibrary(), _ => null);
        var item = ScheduleItem.ForMedia("/videos/intro.mp4");

        var once = Assert.Single(factory.Build(item, new AppSettings()));
        var looping = Assert.Single(factory.Build(item, new AppSettings { LoopVideos = true }));

        Assert.False(once.Loop);
        Assert.True(looping.Loop);
    }

    [Fact]
    public void Settings_saved_before_videos_existed_still_load()
    {
        var old = """{ "pictureFit": "Fill", "mediaFiles": ["/pictures/sunrise.jpg"] }""";

        var settings = JsonSerializer.Deserialize<AppSettings>(old, JsonFile.Options)!;

        Assert.False(settings.LoopVideos);
        Assert.Equal(ImageFit.Fill, settings.PictureFit);
    }
}
