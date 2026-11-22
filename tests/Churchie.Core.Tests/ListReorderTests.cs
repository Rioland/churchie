using Churchie.Core.Services;

namespace Churchie.Core.Tests;

public class ListReorderTests
{
    // A schedule of five items: positions 0 to 4. The gaps are numbered 0 (above the
    // first item) to 5 (below the last one).

    [Fact]
    public void Dragging_up_lands_in_the_gap()
    {
        Assert.Equal(0, ListReorder.MoveTarget(from: 3, dropBefore: 0, count: 5));
        Assert.Equal(1, ListReorder.MoveTarget(from: 3, dropBefore: 1, count: 5));
    }

    [Fact]
    public void Dragging_down_lands_one_place_higher_than_the_gap()
    {
        // Item 0 dropped between items 2 and 3 (gap 3) becomes the new item 2.
        Assert.Equal(2, ListReorder.MoveTarget(from: 0, dropBefore: 3, count: 5));
    }

    [Fact]
    public void Dropping_below_the_last_item_moves_it_to_the_end()
    {
        Assert.Equal(4, ListReorder.MoveTarget(from: 1, dropBefore: 5, count: 5));
        Assert.Equal(4, ListReorder.MoveTarget(from: 1, dropBefore: 99, count: 5)); // past the end counts as the end
    }

    [Fact]
    public void Dropping_right_above_or_below_itself_changes_nothing()
    {
        Assert.Null(ListReorder.MoveTarget(from: 2, dropBefore: 2, count: 5));
        Assert.Null(ListReorder.MoveTarget(from: 2, dropBefore: 3, count: 5));
    }

    [Fact]
    public void Items_outside_the_list_are_ignored()
    {
        Assert.Null(ListReorder.MoveTarget(from: -1, dropBefore: 0, count: 5));
        Assert.Null(ListReorder.MoveTarget(from: 5, dropBefore: 0, count: 5));
        Assert.Null(ListReorder.MoveTarget(from: 0, dropBefore: 0, count: 0));
    }
}
