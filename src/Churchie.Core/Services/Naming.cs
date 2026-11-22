namespace Churchie.Core.Services;

/// <summary>Tidies names that people type when they rename something.</summary>
public static class Naming
{
    /// <summary>Longest name we keep; anything longer would not fit on a thumbnail anyway.</summary>
    public const int MaxLength = 120;

    /// <summary>
    /// The name without spaces at either end, cut to <see cref="MaxLength"/>.
    /// Null when nothing is left (an empty name would leave an item with no label).
    /// </summary>
    public static string? Clean(string? name)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0) return null;
        return trimmed.Length <= MaxLength ? trimmed : trimmed[..MaxLength].TrimEnd();
    }

    /// <summary>
    /// A name that can be used as a file name on Windows, macOS and Linux: the characters
    /// Windows forbids (\ / : * ? " &lt; &gt; |) and invisible control characters become spaces
    /// (so "Part 1/2" stays readable as "Part 1 2" instead of "Part 12"), double spaces are
    /// squeezed to one, and dots or spaces at the ends are trimmed (Windows drops them silently).
    /// Null when nothing usable is left.
    /// </summary>
    public static string? SafeFileName(string? name)
    {
        var cleaned = Clean(name);
        if (cleaned is null) return null;

        var replaced = new string(cleaned.Select(c => char.IsControl(c) || "\\/:*?\"<>|".Contains(c) ? ' ' : c).ToArray());
        while (replaced.Contains("  ")) replaced = replaced.Replace("  ", " ");
        var safe = replaced.Trim().Trim('.').Trim();
        return safe.Length == 0 ? null : safe;
    }
}
