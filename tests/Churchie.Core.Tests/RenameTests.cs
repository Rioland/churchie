using System.Text.Json;
using Churchie.Core.Audio;
using Churchie.Core.Models;
using Churchie.Core.Services;

namespace Churchie.Core.Tests;

public class RenameTests
{
    [Theory]
    [InlineData("  Welcome slide  ", "Welcome slide")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Names_are_trimmed_and_empty_names_refused(string? typed, string? expected)
    {
        Assert.Equal(expected, Naming.Clean(typed));
    }

    [Fact]
    public void Very_long_names_are_cut()
    {
        var name = Naming.Clean(new string('a', 500));
        Assert.Equal(Naming.MaxLength, name!.Length);
    }

    [Theory]
    [InlineData("Sunday: sermon?", "Sunday sermon")]
    [InlineData("Part 1/2", "Part 1 2")]
    [InlineData("Sunday sermon: Part 1/2", "Sunday sermon Part 1 2")]
    [InlineData("  .hidden.  ", "hidden")]
    [InlineData("\"<>|*", null)]
    [InlineData("Ìwàásù Ọjọ́ Àìkú", "Ìwàásù Ọjọ́ Àìkú")] // Yoruba letters are fine in file names
    public void File_names_lose_characters_windows_forbids(string typed, string? expected)
    {
        Assert.Equal(expected, Naming.SafeFileName(typed));
    }

    [Fact]
    public void Media_shows_its_given_name_or_else_its_file_name()
    {
        var names = new Dictionary<string, string> { ["/pictures/IMG_0042.jpg"] = "Welcome picture" };

        Assert.Equal("Welcome picture", MediaFiles.DisplayName("/pictures/IMG_0042.jpg", names));
        Assert.Equal("intro.mp4", MediaFiles.DisplayName("/videos/intro.mp4", names));
        Assert.Equal("intro.mp4", MediaFiles.DisplayName("/videos/intro.mp4", null));
    }

    [Fact]
    public void Settings_saved_before_renaming_existed_still_load()
    {
        var old = """{ "mediaFiles": ["/pictures/sunrise.jpg"] }""";

        var settings = JsonSerializer.Deserialize<AppSettings>(old, JsonFile.Options)!;

        Assert.Empty(settings.MediaNames);
    }

    [Fact]
    public void Given_media_names_are_saved_and_loaded()
    {
        var settings = new AppSettings();
        settings.MediaNames["/videos/intro.mp4"] = "Opening video";

        var json = JsonSerializer.Serialize(settings, JsonFile.Options);
        var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonFile.Options)!;

        Assert.Equal("Opening video", loaded.MediaNames["/videos/intro.mp4"]);
    }

    [Fact]
    public void A_recording_can_be_renamed_in_its_folder()
    {
        var folder = Directory.CreateTempSubdirectory("churchie-rename-").FullName;
        try
        {
            var original = Path.Combine(folder, "Recording 2026-11-22 09.30.05.wav");
            File.WriteAllText(original, "sound");

            var renamed = RecordingFiles.Rename(original, "Sunday sermon: part 1");

            Assert.Equal(Path.Combine(folder, "Sunday sermon part 1.wav"), renamed);
            Assert.True(File.Exists(renamed));
            Assert.False(File.Exists(original));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_recording_is_never_renamed_over_another_one()
    {
        var folder = Directory.CreateTempSubdirectory("churchie-rename-").FullName;
        try
        {
            var first = Path.Combine(folder, "First.wav");
            var second = Path.Combine(folder, "Second.wav");
            File.WriteAllText(first, "one");
            File.WriteAllText(second, "two");

            Assert.Throws<IOException>(() => RecordingFiles.Rename(first, "Second"));
            Assert.Throws<ArgumentException>(() => RecordingFiles.Rename(first, "  "));
            Assert.Equal("two", File.ReadAllText(second));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
