namespace Churchie.Core.Services;

/// <summary>
/// Works out where an item ends up when it is dragged to a new place in a list
/// (for example, reordering the schedule with the mouse).
/// </summary>
public static class ListReorder
{
    /// <summary>
    /// The new position of the item at <paramref name="from"/> when it is dropped in the gap
    /// just before position <paramref name="dropBefore"/> (use <paramref name="count"/> for
    /// "after the last item").
    ///
    /// Moving an item takes it out of the list first, which shifts everything after it up
    /// by one. So when it is dropped further down, it lands one place higher than the gap's number.
    /// </summary>
    /// <returns>The new position, or null when the drop leaves the item where it already is.</returns>
    public static int? MoveTarget(int from, int dropBefore, int count)
    {
        if (from < 0 || from >= count) return null;

        var gap = Math.Clamp(dropBefore, 0, count);
        var target = gap > from ? gap - 1 : gap;
        return target == from ? null : target;
    }
}
