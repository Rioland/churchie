using System.Xml.Linq;
using Churchie.Core.Models;

namespace Churchie.Core.Services;

/// <summary>
/// Imports songs from other programs.
///  • .txt  — plain lyrics (file name becomes the title)
///  • .xml  — OpenLyrics, the open format used by OpenLP, OpenSong exports and others
/// </summary>
public static class SongImporter
{
    public static Song ImportFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var text = File.ReadAllText(path);
        return ext == ".xml"
            ? FromOpenLyrics(text)
            : FromPlainText(Path.GetFileNameWithoutExtension(path), text);
    }

    public static Song FromPlainText(string title, string text)
    {
        // Allow an optional "Title: ..." first line.
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        if (lines.Count > 0 && lines[0].StartsWith("Title:", StringComparison.OrdinalIgnoreCase))
        {
            title = lines[0]["Title:".Length..].Trim();
            lines.RemoveAt(0);
        }

        return new Song
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim(),
            Sections = SongParser.Parse(string.Join("\n", lines)),
        };
    }

    public static Song FromOpenLyrics(string xml)
    {
        var doc = XDocument.Parse(xml);
        XNamespace ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        var props = doc.Root?.Element(ns + "properties");
        var title = props?.Element(ns + "titles")?.Elements(ns + "title").FirstOrDefault()?.Value ?? "Untitled";
        var authors = props?.Element(ns + "authors")?.Elements(ns + "author").Select(a => a.Value) ?? Enumerable.Empty<string>();
        var copyright = props?.Element(ns + "copyright")?.Value ?? "";
        var ccli = props?.Element(ns + "ccliNo")?.Value ?? "";

        var song = new Song
        {
            Title = title.Trim(),
            Author = string.Join(", ", authors),
            Copyright = copyright,
            CcliNumber = ccli,
        };

        var verses = doc.Root?.Element(ns + "lyrics")?.Elements(ns + "verse") ?? Enumerable.Empty<XElement>();
        foreach (var verse in verses)
        {
            var label = LabelFromOpenLyricsName((string?)verse.Attribute("name") ?? "");
            foreach (var linesEl in verse.Elements(ns + "lines"))
            {
                var text = LinesText(linesEl);
                if (text.Length > 0) song.Sections.Add(new SongSection { Label = label, Text = text });
            }
        }

        return song;
    }

    /// <summary>"v1" -> "Verse 1", "c" -> "Chorus", "b2" -> "Bridge 2".</summary>
    internal static string LabelFromOpenLyricsName(string name)
    {
        if (name.Length == 0) return "Verse";
        var letter = char.ToLowerInvariant(name[0]);
        var number = new string(name.Skip(1).Where(char.IsDigit).ToArray());
        var word = letter switch
        {
            'v' => "Verse",
            'c' => "Chorus",
            'b' => "Bridge",
            'p' => "Pre-Chorus",
            'i' => "Intro",
            'e' => "Ending",
            _ => "Other",
        };
        return number.Length > 0 ? $"{word} {number}" : word;
    }

    /// <summary>OpenLyrics uses &lt;br/&gt; for line breaks and may contain chord tags.</summary>
    private static string LinesText(XElement linesEl)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var node in linesEl.DescendantNodes())
        {
            if (node is XText t && t.Parent?.Name.LocalName != "comment") sb.Append(t.Value);
            else if (node is XElement { Name.LocalName: "br" }) sb.Append('\n');
        }
        var lines = sb.ToString().Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);
        return string.Join("\n", lines);
    }
}
