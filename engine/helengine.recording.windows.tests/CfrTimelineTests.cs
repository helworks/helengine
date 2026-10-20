namespace helengine.recording.windows.tests;
/// <summary>Checks rational timing against literal deadlines instead of a wall clock.</summary>
public sealed class CfrTimelineTests {
    /// <summary>Integer QPC deadlines cannot accumulate rounded 1/60 second errors.</summary>
    [Theory][InlineData(10_000_000L, 36_000_000_000L)][InlineData(3_579_545L, 12_886_362_000L)]
    public void UsesRationalDeadlinesWithoutDrift(long frequency, long hourDeadline) {
        var timeline = new CfrTimeline(0, frequency, TimeSpan.FromHours(1));
        Assert.Equal(216000, timeline.FrameLimit); Assert.Equal(hourDeadline, timeline.GetDeadlineCounter(216000));
        Assert.Equal(frequency / 60, timeline.GetDeadlineCounter(1));
        Assert.False(timeline.IsEligible(166667, 1)); Assert.True(timeline.IsEligible(166666, 1));
    }
    /// <summary>Positive fractional durations round upward to a complete output slot.</summary>
    [Theory][InlineData(10000L, 1L)][InlineData(10010000L, 61L)][InlineData(100000000L, 600L)]
    public void RoundsFractionalDurationUp(long ticks, long count) => Assert.Equal(count, new CfrTimeline(0, 10_000_000, TimeSpan.FromTicks(ticks)).FrameLimit);
    /// <summary>The 250 ms boundary remains precise at arbitrary QPC frequencies.</summary>
    [Fact] public void DistinguishesExactLagBoundary() {
        var timeline = new CfrTimeline(100, 10_000_000, null);
        Assert.Null(timeline.FrameLimit); Assert.Equal(TimeSpan.FromMilliseconds(250), timeline.GetLag(2_500_100, 0));
        Assert.True(timeline.GetLag(2_500_101, 0) > TimeSpan.FromMilliseconds(250));
        Assert.Equal(TimeSpan.Zero, timeline.GetLag(100, 1));
    }
}
