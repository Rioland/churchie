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
    /// Windows forbids (\ / : * ? " &lt; &gt; |) and invisible control characters are removed,
    /// and dots or spaces at the ends are trimmed (Windows drops them silently).
    /// Null when nothing usable is left.
    /// </summary>
    public static string? SafeFileName(string? name)
    {
        var cleaned = Clean(name);
        if (cleaned is null) return null;

        var kept = new string(cleaned.Where(c => !char.IsControl(c) && !"\\/:*?\"<>|".Contains(c)).ToArray());
        var safe = kept.Trim().Trim('.').Trim();
        return safe.Length == 0 ? null : safe;
    }
}
