using Churchie.Core.Bible;
using Churchie.Core.Models;
using Churchie.Core.Services;

namespace Churchie.Core.Tests;

public class LibraryTests
{
    private static string TempFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), "churchie-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }

    [Fact]
    public void First_run_adds_sample_hymns_and_reloads_them()
    {
        var folder = TempFolder();
        var library = new SongLibrary(folder);
        library.Load();
        Assert.Equal(3, library.Songs.Count);

        var again = new SongLibrary(folder);
        again.Load();
        Assert.Equal(3, again.Songs.Count);
        Assert.Contains(again.Songs, s => s.Title == "Amazing Grace");
        Assert.Empty(again.LoadErrors);
    }

    [Fact]
    public void Save_search_and_delete()
    {
        var library = new SongLibrary(TempFolder());
        library.Load();

        var song = new Song { Title = "Way Maker Test", Sections = SongParser.Parse("[Chorus]\nMiracle worker") };
        library.Save(song);

        Assert.Equal("Way Maker Test", library.Search("way maker").First().Title);
        Assert.Equal("Way Maker Test", library.Search("miracle").First().Title);

        library.Delete(song.Id);
        Assert.Null(library.Find(song.Id));
    }

    [Fact]
    public void Song_slides_split_long_sections()
    {
        var song = new Song { Sections = SongParser.Parse("[Verse 1]\n1\n2\n3\n4\n5\n6") };

        Assert.Single(SlideBuilder.FromSong(song));
        var split = SlideBuilder.FromSong(song, maxLinesPerSlide: 4);
        Assert.Equal(2, split.Count);
        Assert.Equal("Verse 1 (2/2)", split[1].Label);
        Assert.Equal("5\n6", split[1].Text);
    }

    [Fact]
    public void Schedule_round_trips_through_a_file()
    {
        var path = Path.Combine(TempFolder() + ScheduleStore.Extension);
        var schedule = new Schedule { Name = "Test" };
        schedule.Items.Add(ScheduleItem.ForScripture("KJV", "John 3:16"));
        schedule.Items.Add(ScheduleItem.ForText("Welcome", "Welcome to church\n\nPlease silence your phones"));

        ScheduleStore.Save(path, schedule);
        var loaded = ScheduleStore.Load(path);

        Assert.Equal(2, loaded.Items.Count);
        Assert.Equal(ScheduleItemKind.Scripture, loaded.Items[0].Kind);
        Assert.Equal(2, SlideBuilder.FromText(loaded.Items[1].Text).Count);
    }

    [Fact]
    public void Slide_factory_uses_the_edited_library_song()
    {
        var song = new Song { Title = "Old", Sections = SongParser.Parse("old words") };
        var item = ScheduleItem.ForSong(song);
        var edited = song.Clone();
        edited.Sections = SongParser.Parse("new words");

        var factory = new SlideFactory(new BibleLibrary(), id => id == song.Id ? edited : null);
        var slides = factory.Build(item, new AppSettings());

        Assert.Equal("new words", slides.Single().Text);
    }

    [Fact]
    public void Imports_openlyrics()
    {
        const string xml = """
            <song xmlns="http://openlyrics.info/namespace/2009/song" version="0.9">
              <properties>
                <titles><title>Test Song</title></titles>
                <authors><author>Someone</author></authors>
              </properties>
              <lyrics>
                <verse name="v1"><lines>Line one<br/>Line two</lines></verse>
                <verse name="c"><lines>Chorus line</lines></verse>
              </lyrics>
            </song>
            """;
        var song = SongImporter.FromOpenLyrics(xml);

        Assert.Equal("Test Song", song.Title);
        Assert.Equal("Someone", song.Author);
        Assert.Equal("Verse 1", song.Sections[0].Label);
        Assert.Equal("Line one\nLine two", song.Sections[0].Text);
        Assert.Equal("Chorus", song.Sections[1].Label);
    }
}
