using System.Globalization;
using System.Text.RegularExpressions;
using Churchie.Core.Models;

namespace Churchie.Core.Services;

/// <summary>
/// Converts between the plain text the operator types in the song editor and
/// a list of <see cref="SongSection"/>s.
///
/// Rules (the same ones people already use when pasting lyrics):
///  • A blank line starts a new section (= a new slide).
///  • A line like "[Chorus]", "Verse 2", "Bridge:" or "V1" names the section below it.
///  • Sections without a name are called "Verse 1", "Verse 2", ...
/// </summary>
public static class SongParser
{
    private static readonly Regex BracketHeader = new(@"^\[(?<label>[^\]]+)\]$", RegexOptions.Compiled);

    private static readonly Regex WordHeader = new(
        @"^(?<word>verse|chorus|refrain|bridge|pre[- ]?chorus|tag|ending|intro|outro|interlude|vamp|coda)\s*(?<num>\d+)?\s*:?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ShortHeader = new(
        @"^(?<letter>v|c|b|pc)\s*(?<num>\d+):?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AutoVerse = new(@"^Verse (?<n>\d+)$", RegexOptions.Compiled);

    public static List<SongSection> Parse(string? text)
    {
        var sections = new List<SongSection>();
        var lines = new List<string>();
        string? pendingLabel = null;

        void Flush()
        {
            if (lines.Count == 0) return;
            sections.Add(new SongSection
            {
                Label = pendingLabel ?? NextVerseLabel(sections),
                Text = string.Join("\n", lines),
            });
            lines.Clear();
            pendingLabel = null;
        }

        foreach (var raw in (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.Trim();

            if (line.Length == 0)
            {
                Flush(); // a header followed by a blank line keeps its label for the next block
                continue;
            }

            if (TryGetHeader(line, out var label))
            {
                Flush();
                pendingLabel = label;
                continue;
            }

            lines.Add(line);
        }

        Flush();
        return sections;
    }

    /// <summary>Turns sections back into editable text with [Label] headers.</summary>
    public static string ToText(IEnumerable<SongSection> sections) =>
        string.Join("\n\n", sections.Select(s => $"[{s.Label}]\n{s.Text}"));

    public static bool TryGetHeader(string line, out string label)
    {
        label = "";

        var m = BracketHeader.Match(line);
        if (m.Success)
        {
            label = m.Groups["label"].Value.Trim();
            return label.Length > 0;
        }

        m = WordHeader.Match(line);
        if (m.Success)
        {
            var word = m.Groups["word"].Value.ToLowerInvariant().Replace(" ", "-");
            word = word is "prechorus" ? "pre-chorus" : word;
            label = WithNumber(TitleCase(word), m.Groups["num"].Value);
            return true;
        }

        m = ShortHeader.Match(line);
        if (m.Success)
        {
            var name = m.Groups["letter"].Value.ToLowerInvariant() switch
            {
                "v" => "Verse",
                "c" => "Chorus",
                "b" => "Bridge",
                _ => "Pre-Chorus",
            };
            label = WithNumber(name, m.Groups["num"].Value);
            return true;
        }

        return false;
    }

    private static string WithNumber(string name, string number) =>
        string.IsNullOrEmpty(number) ? name : $"{name} {number}";

    private static string TitleCase(string word) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(word);

    private static string NextVerseLabel(List<SongSection> sections)
    {
        var max = 0;
        foreach (var s in sections)
        {
            var m = AutoVerse.Match(s.Label);
            if (m.Success) max = Math.Max(max, int.Parse(m.Groups["n"].Value));
        }
        return $"Verse {max + 1}";
    }
}
