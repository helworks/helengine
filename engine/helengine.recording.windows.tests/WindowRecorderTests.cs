namespace helengine.recording.windows.tests;
/// <summary>Tests actual worker scheduling, finalization and cleanup against controlled external boundaries.</summary>
public sealed class WindowRecorderTests {
    /// <summary>An absent parent, or a file in its place, must fail before device, capture or work allocation.</summary>
    [Theory][InlineData(false)][InlineData(true)] public async Task InvalidOutputParentFailsBeforeRecording(bool parentIsFile) {
        var dependencies = new RecordingTestDependencies(); var options = RecordingTestDependencies.Options(); string parent = Path.GetDirectoryName(options.OutputPath);
        if (parentIsFile) { dependencies.Files.Files[parent] = [9, 8, 7]; } else { dependencies.Files.ParentDirectoryExists = false; }
        var result = await dependencies.Create().RecordAsync(options, (IntPtr)1, default);
        Assert.False(result.Succeeded); Assert.False(result.HasOutput); Assert.Equal(WindowRecordingStopReason.PublicationFailed, result.StopReason);
        Assert.Equal(IntPtr.Zero, dependencies.CaptureDevice); Assert.Equal(IntPtr.Zero, dependencies.EncoderDevice); Assert.Equal(0, dependencies.Device.DisposeCount);
        Assert.Empty(dependencies.Encoder.Indices); Assert.Equal(parentIsFile ? 1 : 0, dependencies.Files.Files.Count);
        if (parentIsFile) { Assert.Equal(new byte[] { 9, 8, 7 }, dependencies.Files.Files[parent]); }
    }
    /// <summary>A presentation timestamp slightly ahead of delivery is awaited before establishing the output origin.</summary>
    [Fact] public async Task WaitsForFutureFirstObservationBeforeFixingOrigin() {
        var dependencies = new RecordingTestDependencies(); dependencies.Source.Frames.Clear(); dependencies.Source.DeliverFutureFrames = true; dependencies.Source.Frames.Enqueue(new(10000));
        var result = await dependencies.Create().RecordAsync(RecordingTestDependencies.Options(TimeSpan.FromMilliseconds(1)), (IntPtr)1, default);
        Assert.True(result.Succeeded, result.ErrorMessage); Assert.Single(dependencies.Encoder.Indices); Assert.Equal((IntPtr)10001, dependencies.Encoder.Textures[0]);
        Assert.InRange(result.RecordingDuration.TotalMilliseconds, 16, 17);
    }
    /// <summary>Setup time must not be encoded as an initial freeze or charged to the requested duration.</summary>
    [Fact] public async Task StartsClockAfterEncoderPreparation() {
        var dependencies = new RecordingTestDependencies(); dependencies.OnEncoderOpen = () => { dependencies.Clock.Counter += 50_000_000; dependencies.Source.Frames.Enqueue(new(dependencies.Clock.Counter)); };
        var result = await dependencies.Create().RecordAsync(RecordingTestDependencies.Options(TimeSpan.FromMilliseconds(1)), (IntPtr)1, default);
        Assert.True(result.Succeeded, result.ErrorMessage); Assert.Single(dependencies.Encoder.Indices); Assert.Equal((IntPtr)50_000_001, dependencies.Encoder.Textures[0]);
        Assert.InRange(result.RecordingDuration.TotalSeconds, 0, 0.02); Assert.Equal("Test NVIDIA", result.AdapterName);
    }
    /// <summary>Duration creates exact consecutive slots and publishes completed encoder output.</summary>
    [Fact] public async Task StopsAtDurationAndDrains() {
        var dependencies = new RecordingTestDependencies(); var options = RecordingTestDependencies.Options();
        var result = await dependencies.Create().RecordAsync(options, (IntPtr)1, default);
        Assert.True(result.Succeeded, result.ErrorMessage); Assert.Equal(WindowRecordingStopReason.DurationReached, result.StopReason);
        Assert.Equal(Enumerable.Range(0, 60).Select(index => (long)index), dependencies.Encoder.Indices); Assert.Equal(59, result.RepeatedFrames);
        Assert.Equal(TimeSpan.FromSeconds(1), result.EncodedDuration); Assert.Equal(TimeSpan.FromSeconds(1), result.RecordingDuration);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, dependencies.Files.Files[options.OutputPath]); Assert.Equal(1, dependencies.Encoder.CompleteCount);
    }
    /// <summary>Neither a stopped session nor an already closed source may publish an empty video.</summary>
    [Theory][InlineData(true)][InlineData(false)] public async Task StopAndWindowClosureBeforeFirstFrameReturnNoFrames(bool cancel) {
        var dependencies = new RecordingTestDependencies(); dependencies.Source.Frames.Clear(); using var stop = new CancellationTokenSource();
        if (cancel) { stop.Cancel(); } else { dependencies.Source.CloseAt = 0; }
        var result = await dependencies.Create().RecordAsync(RecordingTestDependencies.Options(), (IntPtr)1, stop.Token);
        Assert.False(result.HasOutput); Assert.False(result.Succeeded); Assert.Equal(WindowRecordingStopReason.NoFrames, result.StopReason); Assert.Empty(dependencies.Files.Files);
    }
    /// <summary>An inactive source does not hold the recording setup indefinitely.</summary>
    [Fact] public async Task TimesOutFirstFrameAtTenSeconds() {
        var dependencies = new RecordingTestDependencies(); dependencies.Source.Frames.Clear();
        var result = await dependencies.Create().RecordAsync(RecordingTestDependencies.Options(), (IntPtr)1, default);
        Assert.Equal(WindowRecordingStopReason.NoFrames, result.StopReason); Assert.Equal(100_000_000, dependencies.Clock.Counter); Assert.False(result.HasOutput);
    }
    /// <summary>Resize and slow encoding fail explicitly while preserving a finalized prefix.</summary>
    [Theory][InlineData(true)][InlineData(false)] public async Task ResizeAndSlowEncoderPreserveOnlyFinalizedPrefix(bool resize) {
        var dependencies = new RecordingTestDependencies(); dependencies.Encoder.OnSubmit = () => {
            if (resize) { dependencies.Source.Frames.Enqueue(new(dependencies.Clock.Counter + 1, 642, 480)); }
            else { dependencies.Clock.Counter += 2_500_001; }
        };
        var result = await dependencies.Create().RecordAsync(RecordingTestDependencies.Options(), (IntPtr)1, default);
        Assert.False(result.Succeeded); Assert.True(result.HasOutput, result.ErrorMessage);
        Assert.Equal(resize ? WindowRecordingStopReason.Resized : WindowRecordingStopReason.EncoderTooSlow, result.StopReason); Assert.Single(dependencies.Encoder.Indices);
    }
    /// <summary>A trailer failure is never replaced by a secondary source cleanup exception.</summary>
    [Fact] public async Task TrailerFailurePreservesOriginalError() {
        var dependencies = new RecordingTestDependencies(); dependencies.Encoder.OnComplete = () => throw new IOException("original trailer failure"); dependencies.Source.FailDispose = true;
        var result = await dependencies.Create().RecordAsync(RecordingTestDependencies.Options(TimeSpan.FromMilliseconds(1)), (IntPtr)1, default);
        Assert.Equal(WindowRecordingStopReason.EncoderFailed, result.StopReason); Assert.Contains("original trailer", result.ErrorMessage); Assert.False(result.HasOutput); Assert.Empty(dependencies.Files.Files); Assert.NotEmpty(result.CleanupErrors);
    }
    /// <summary>Closure after a frame completes normally and retains no source leases.</summary>
    [Fact] public async Task WindowClosedAfterFramesIsSuccessful() {
        var dependencies = new RecordingTestDependencies(); dependencies.Source.CloseAt = 166666;
        var result = await dependencies.Create().RecordAsync(RecordingTestDependencies.Options(), (IntPtr)1, default);
        Assert.True(result.Succeeded, result.ErrorMessage); Assert.Equal(WindowRecordingStopReason.WindowClosed, result.StopReason); Assert.Single(dependencies.Encoder.Indices);
        Assert.Equal(1, dependencies.Source.DisposeCount); Assert.Equal(1, dependencies.Device.DisposeCount);
    }
    /// <summary>A stop raised while Submit finishes does not produce a second output slot.</summary>
    [Fact] public async Task GracefulStopDoesNotSubmitAnotherSlot() {
        var dependencies = new RecordingTestDependencies(); using var stop = new CancellationTokenSource(); dependencies.Encoder.OnSubmit = stop.Cancel;
        var result = await dependencies.Create().RecordAsync(RecordingTestDependencies.Options(), (IntPtr)1, stop.Token);
        Assert.True(result.Succeeded, result.ErrorMessage); Assert.Equal(WindowRecordingStopReason.StopRequested, result.StopReason); Assert.Single(dependencies.Encoder.Indices);
        Assert.Equal(dependencies.Device.Handle, dependencies.CaptureDevice); Assert.Equal(dependencies.CaptureDevice, dependencies.EncoderDevice);
    }
    /// <summary>A stop that arrives during a deadline wait is graceful and submits no extra frame.</summary>
    [Fact] public async Task StopDuringDeadlineWaitIsGraceful() {
        var dependencies = new RecordingTestDependencies(); using var stop = new CancellationTokenSource();
        dependencies.Clock.OnWait = () => { if (dependencies.Encoder.Indices.Count > 0) { stop.Cancel(); } };
        var result = await dependencies.Create().RecordAsync(RecordingTestDependencies.Options(), (IntPtr)1, stop.Token);
        Assert.True(result.Succeeded, result.ErrorMessage); Assert.Single(dependencies.Encoder.Indices); Assert.Equal(WindowRecordingStopReason.StopRequested, result.StopReason);
    }
    /// <summary>The exact lag ceiling is permitted, while the next counter tick is already tested as failure.</summary>
    [Fact] public async Task AllowsExactly250MillisecondsLag() {
        var dependencies = new RecordingTestDependencies(); dependencies.Encoder.OnSubmit = () => dependencies.Clock.Counter += 2_500_000;
        var result = await dependencies.Create().RecordAsync(RecordingTestDependencies.Options(TimeSpan.FromMilliseconds(1)), (IntPtr)1, default);
        Assert.True(result.Succeeded, result.ErrorMessage); Assert.Equal(TimeSpan.FromMilliseconds(250), result.MaxLag);
    }
    /// <summary>A preexisting destination fails before opening capture and is never cleaned up as work.</summary>
    [Fact] public async Task AbortRemovesOnlySessionWorkFile() {
        var dependencies = new RecordingTestDependencies(); var options = RecordingTestDependencies.Options(); dependencies.Files.Files[options.OutputPath] = [9, 8, 7];
        var result = await dependencies.Create().RecordAsync(options, (IntPtr)1, default);
        Assert.False(result.HasOutput); Assert.Equal(WindowRecordingStopReason.PublicationFailed, result.StopReason);
        Assert.Equal(new byte[] { 9, 8, 7 }, dependencies.Files.Files[options.OutputPath]); Assert.Equal(IntPtr.Zero, dependencies.CaptureDevice); Assert.Single(dependencies.Files.Files);
    }
}
