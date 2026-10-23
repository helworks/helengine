namespace helengine.tools.windowrecorderlinux.tests;

/// <summary>Checks timing boundaries so slow capture produces counted repeats instead of stretching the source timeline.</summary>
public sealed class FrameScheduleTests {
    /// <summary>Uses absolute deadlines that never fall before an exact rational CFR slot.</summary>
    [Fact]
    public void DeadlinesRoundOnlyAtClockBoundaryWithoutAccumulatingDrift() {
        var schedule = new FrameSchedule(1800);
        Assert.Equal(TimeSpan.FromSeconds(30), schedule.Deadline(1800));
        Assert.Equal(1, schedule.SlotAt(schedule.Deadline(1)));
        Assert.Equal(59, schedule.SlotAt(TimeSpan.FromTicks(TimeSpan.TicksPerSecond - 1)));
        Assert.Equal(60, schedule.SlotAt(TimeSpan.FromSeconds(1)));
    }

    /// <summary>A ten-slot stall accounts for nine previous-image repeats before the next actual capture.</summary>
    [Fact]
    public void MissedSlotsRemainExplicitAndEndIsClamped() {
        var schedule = new FrameSchedule(1800);
        long nextOutput = 1;
        long captureSlot = schedule.SlotAt(TimeSpan.FromMilliseconds(170));
        Assert.Equal(10, captureSlot);
        Assert.Equal(9, captureSlot - nextOutput);
        Assert.Equal(1800, schedule.SlotAt(TimeSpan.FromSeconds(30)));
        Assert.Equal(1800, schedule.SlotAt(TimeSpan.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => schedule.SlotAt(TimeSpan.FromTicks(-1)));
    }
}
