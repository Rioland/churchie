using Churchie.Core.Services;

namespace Churchie.Core.Tests;

public class ScheduleStepperTests
{
    // A small service: a song with 3 slides, a passage with 2, then a picture with 1.
    private static readonly int[] Service = { 3, 2, 1 };

    [Fact]
    public void Next_moves_one_slide_inside_an_item()
    {
        Assert.Equal(new SchedulePosition(0, 1), ScheduleStepper.Step(Service, new SchedulePosition(0, 0), +1));
    }

    [Fact]
    public void Next_after_the_last_slide_goes_to_the_next_item()
    {
        Assert.Equal(new SchedulePosition(1, 0), ScheduleStepper.Step(Service, new SchedulePosition(0, 2), +1));
        Assert.Equal(new SchedulePosition(2, 0), ScheduleStepper.Step(Service, new SchedulePosition(1, 1), +1));
    }

    [Fact]
    public void Previous_before_the_first_slide_goes_to_the_last_slide_of_the_item_before()
    {
        Assert.Equal(new SchedulePosition(0, 2), ScheduleStepper.Step(Service, new SchedulePosition(1, 0), -1));
    }

    [Fact]
    public void Nothing_happens_at_the_ends_of_the_schedule()
    {
        Assert.Null(ScheduleStepper.Step(Service, new SchedulePosition(2, 0), +1));
        Assert.Null(ScheduleStepper.Step(Service, new SchedulePosition(0, 0), -1));
    }

    [Fact]
    public void Items_without_slides_are_skipped()
    {
        int[] withEmpty = { 2, 0, 0, 1 };

        Assert.Equal(new SchedulePosition(3, 0), ScheduleStepper.Step(withEmpty, new SchedulePosition(0, 1), +1));
        Assert.Equal(new SchedulePosition(0, 1), ScheduleStepper.Step(withEmpty, new SchedulePosition(3, 0), -1));
    }

    [Fact]
    public void Next_with_nothing_selected_starts_at_the_first_slide()
    {
        Assert.Equal(new SchedulePosition(0, 0), ScheduleStepper.Step(Service, new SchedulePosition(-1, 0), +1));
    }

    [Fact]
    public void An_item_on_its_own_steps_only_through_its_own_slides()
    {
        // Previewing a song straight from the library (not in the schedule) is a list of one.
        int[] single = { 2 };

        Assert.Equal(new SchedulePosition(0, 1), ScheduleStepper.Step(single, new SchedulePosition(0, 0), +1));
        Assert.Null(ScheduleStepper.Step(single, new SchedulePosition(0, 1), +1));
    }

    [Fact]
    public void Item_jumps_land_on_the_first_slide()
    {
        Assert.Equal(new SchedulePosition(1, 0), ScheduleStepper.StepItem(Service, 0, +1));
        Assert.Equal(new SchedulePosition(0, 0), ScheduleStepper.StepItem(Service, 1, -1));
        Assert.Equal(new SchedulePosition(0, 0), ScheduleStepper.StepItem(Service, -1, +1));
        Assert.Null(ScheduleStepper.StepItem(Service, 2, +1));
        Assert.Null(ScheduleStepper.StepItem(Service, 0, -1));
    }

    [Fact]
    public void An_empty_schedule_goes_nowhere()
    {
        Assert.Null(ScheduleStepper.Step(Array.Empty<int>(), new SchedulePosition(-1, 0), +1));
        Assert.Null(ScheduleStepper.StepItem(Array.Empty<int>(), -1, +1));
    }
}
