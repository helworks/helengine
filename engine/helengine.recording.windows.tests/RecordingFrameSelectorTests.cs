namespace helengine.recording.windows.tests;
/// <summary>Verifies temporal resampling, future retention and explicit lease ownership.</summary>
public sealed class RecordingFrameSelectorTests {
    /// <summary>A change to slot eligibility or repeat accounting breaks these independent totals.</summary>
    [Theory][InlineData(30, 300, 0)][InlineData(60, 0, 0)][InlineData(120, 0, 599)][InlineData(0, 599, 0)]
    public void ResamplesTenSeconds(int sourceFps, long repeated, long discarded) {
        var frames = new List<TestRecordingFrame>();
        int observations = sourceFps == 0 ? 1 : sourceFps * 10;
        for (int index = 0; index < observations; index++) { frames.Add(new(sourceFps == 0 ? 0 : index * 10_000_000L / sourceFps)); }
        using (var selector = new RecordingFrameSelector(new(0, 10_000_000, TimeSpan.FromSeconds(10)))) {
            int next = 0;
            for (int slot = 0; slot < 600; slot++) {
                selector.BeginSlot(slot);
                while (selector.CanAcquire && next < frames.Count) { selector.Accept(frames[next++]); }
                var selected = selector.Select(); Assert.NotNull(selected);
                Assert.True(selected.Timestamp.Ticks * (Int128)60 <= slot * (Int128)10_000_000);
                Assert.InRange(frames.Count(frame => frame.DisposeCount == 0) - (frames.Count - next), 1, 2);
            }
            Assert.Equal(repeated, selector.RepeatedFrames); Assert.Equal(discarded, selector.ResampledSourceDiscards);
            for (; next < frames.Count; next++) { frames[next].Dispose(); }
        }
        Assert.All(frames, frame => Assert.Equal(1, frame.DisposeCount));
    }
    /// <summary>Future content cannot appear before its original observation timestamp.</summary>
    [Fact] public void DoesNotPresentFutureFrameEarly() {
        using var selector = new RecordingFrameSelector(new(0, 10_000_000, null));
        var first = new TestRecordingFrame(0); var future = new TestRecordingFrame(333333);
        selector.BeginSlot(0); selector.Accept(first); selector.Accept(future); Assert.False(selector.CanAcquire); Assert.Same(first, selector.Select());
        selector.BeginSlot(1); Assert.False(selector.CanAcquire); Assert.Same(first, selector.Select());
        selector.BeginSlot(2); Assert.True(selector.CanAcquire); Assert.Same(future, selector.Select()); Assert.Equal(1, first.DisposeCount);
    }
    /// <summary>A rejected regressing timestamp does not silently steal the caller's lease.</summary>
    [Fact] public void RejectsRegressingTimestamps() {
        using var selector = new RecordingFrameSelector(new(100, 10_000_000, null)); selector.BeginSlot(0);
        selector.Accept(new TestRecordingFrame(100)); var old = new TestRecordingFrame(99);
        Assert.Throws<InvalidDataException>(() => selector.Accept(old)); Assert.Equal(0, old.DisposeCount); old.Dispose();
    }
    /// <summary>A displayed frame is not a resampling discard when replaced in a later slot.</summary>
    [Fact] public void CountsResamplingWithoutCountingDisplayedFramesAsDrops() {
        using var selector = new RecordingFrameSelector(new(0, 10_000_000, null)); selector.BeginSlot(0); selector.Accept(new TestRecordingFrame(0)); selector.Select();
        selector.BeginSlot(1); selector.Accept(new TestRecordingFrame(100)); selector.Accept(new TestRecordingFrame(200)); selector.Select();
        Assert.Equal(1, selector.ResampledSourceDiscards); Assert.Equal(0, selector.RepeatedFrames);
    }
}
