namespace Churchie.Core.Services;

/// <summary>A place in the schedule: which item (0 = the first) and which of its slides (0 = the first).</summary>
public readonly record struct SchedulePosition(int Item, int Slide);

/// <summary>
/// Works out where the Preview panel's Next and Previous buttons go, so the operator
/// can walk through the whole service — every slide of every item — with one button.
/// It only needs to know how many slides each item has, which keeps it easy to test.
/// </summary>
public static class ScheduleStepper
{
    /// <summary>
    /// One slide forward (<paramref name="step"/> = +1) or back (-1).
    /// Past the last slide of an item it carries on to the first slide of the next item;
    /// before the first slide it goes back to the last slide of the item before.
    /// Items with no slides (e.g. a passage whose Bible is missing) are skipped.
    /// </summary>
    /// <param name="slideCounts">How many slides each schedule item has, in schedule order.</param>
    /// <returns>The new position, or null at the very start or end of the schedule.</returns>
    public static SchedulePosition? Step(IReadOnlyList<int> slideCounts, SchedulePosition current, int step)
    {
        if (step == 0) return current;
        var direction = Math.Sign(step);

        // Still inside the same item?
        if (current.Item >= 0 && current.Item < slideCounts.Count)
        {
            var slide = current.Slide + direction;
            if (slide >= 0 && slide < slideCounts[current.Item])
                return current with { Slide = slide };
        }

        // No: find the next item (in that direction) that has something to show.
        var item = NextItemWithSlides(slideCounts, current.Item, direction);
        if (item is null) return null;

        // Going forward lands on its first slide; going back lands on its last slide.
        return new SchedulePosition(item.Value, direction > 0 ? 0 : slideCounts[item.Value] - 1);
    }

    /// <summary>
    /// Jumps a whole item forward or back, landing on that item's first slide.
    /// Use -1 as <paramref name="currentItem"/> when nothing in the schedule is selected yet.
    /// </summary>
    /// <returns>The new position, or null when there is no item in that direction.</returns>
    public static SchedulePosition? StepItem(IReadOnlyList<int> slideCounts, int currentItem, int step)
    {
        if (step == 0) return null;
        var item = NextItemWithSlides(slideCounts, currentItem, Math.Sign(step));
        return item is null ? null : new SchedulePosition(item.Value, 0);
    }

    /// <summary>The nearest item after (direction +1) or before (-1) <paramref name="from"/> that has at least one slide.</summary>
    private static int? NextItemWithSlides(IReadOnlyList<int> slideCounts, int from, int direction)
    {
        for (var i = from + direction; i >= 0 && i < slideCounts.Count; i += direction)
        {
            if (slideCounts[i] > 0) return i;
        }
        return null;
    }
}
