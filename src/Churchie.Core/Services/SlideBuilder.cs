using Churchie.Core.Bible;
using Churchie.Core.Models;

namespace Churchie.Core.Services;

/// <summary>Turns songs, passages, text and images into slides.</summary>
public static class SlideBuilder
{
    /// <param name="maxLinesPerSlide">Split long sections into several slides. 0 = never split.</param>
    public static List<Slide> FromSong(Song song, int maxLinesPerSlide = 0)
    {
        var slides = new List<Slide>();
        foreach (var section in song.Sections)
        {
            var lines = section.Text.Split('\n');
            if (maxLinesPerSlide <= 0 || lines.Length <= maxLinesPerSlide)
            {
                slides.Add(new Slide(SlideKind.Song, section.Label, section.Text));
                continue;
            }

            var parts = Chunk(lines, maxLinesPerSlide);
            for (var i = 0; i < parts.Count; i++)
                slides.Add(new Slide(SlideKind.Song, $"{section.Label} ({i + 1}/{parts.Count})", string.Join("\n", parts[i])));
        }
        return slides;
    }

    public static List<Slide> FromPassage(Passage passage, int versesPerSlide = 1, bool showVerseNumbers = true)
    {
        var slides = new List<Slide>();
        var size = Math.Max(1, versesPerSlide);
        var chapter = passage.Reference.Chapter;

        foreach (var group in Chunk(passage.Verses, size))
        {
            var first = group[0].Number;
            var last = group[^1].Number;
            var range = first == last ? $"{chapter}:{first}" : $"{chapter}:{first}-{last}";
            var text = string.Join(" ", group.Select(v => showVerseNumbers ? $"{v.Number} {v.Text}" : v.Text));
            var footer = $"{passage.Book.Name} {range} ({passage.Bible.Abbreviation})";
            slides.Add(new Slide(SlideKind.Scripture, range, text, footer));
        }
        return slides;
    }

    /// <summary>Plain text: each blank-line separated block becomes a slide.</summary>
    public static List<Slide> FromText(string? text)
    {
        var blocks = (text ?? "").Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(b => b.Trim('\n', ' '))
            .Where(b => b.Length > 0)
            .ToList();

        return blocks.Select((b, i) => new Slide(SlideKind.Text, $"Slide {i + 1}", b)).ToList();
    }

    public static Slide FromImage(string path) =>
        new(SlideKind.Image, Path.GetFileNameWithoutExtension(path), "", null, path);

    private static List<T[]> Chunk<T>(IReadOnlyList<T> items, int size)
    {
        var result = new List<T[]>();
        for (var i = 0; i < items.Count; i += size)
            result.Add(items.Skip(i).Take(size).ToArray());
        return result;
    }
}
