using helengine.capture.windows;
using helengine.directx11.video;
namespace helengine.recording.windows;
/// <summary>Owns one recording lifecycle, including all leases, measured timing and safe publication.</summary>
sealed class WindowRecordingWorker {
    /// <summary>Immutable caller configuration.</summary>
    readonly WindowRecordingOptions Options;
    /// <summary>Optional borrowed device provided by the host.</summary>
    readonly IntPtr SuppliedDevice;
    /// <summary>Imports or creates one independently owned device.</summary>
    readonly Func<IntPtr, IRecordingDevice> DeviceFactory;
    /// <summary>Opens capture on that device.</summary>
    readonly Func<IntPtr, IntPtr, IRecordingSource> SourceFactory;
    /// <summary>Opens encoding on that device.</summary>
    readonly Func<IntPtr, string, HevcVideoEncoderOptions, IRecordingEncoder> EncoderFactory;
    /// <summary>Monotonic host timing and waits.</summary>
    readonly IRecordingClock Clock;
    /// <summary>Exclusive work file and output ownership.</summary>
    readonly RecordingOutputPublisher Publisher;
    /// <summary>Absolute caller destination, never treated as an owned work file.</summary>
    readonly string OutputPath;
    /// <summary>Result accumulates observed counters and primary/secondary diagnostics.</summary>
    readonly WindowRecordingResult Result;
    /// <summary>Retained shared device owner.</summary>
    IRecordingDevice Device;
    /// <summary>Bounded capture source.</summary>
    IRecordingSource Source;
    /// <summary>Bounded GPU encoder.</summary>
    IRecordingEncoder Encoder;
    /// <summary>Initial observation until transferred to the selector.</summary>
    IRecordingFrame Initial;
    /// <summary>Two-lease temporal selector.</summary>
    RecordingFrameSelector Selector;
    /// <summary>Rational timeline, absent until preparation has completed.</summary>
    CfrTimeline Timeline;
    /// <summary>Host counter origin outside preparation.</summary>
    long Origin;
    /// <summary>Session-created work path, never another session's recording.</summary>
    string WorkPath;
    /// <summary>Failure attribution for the current external operation.</summary>
    WindowRecordingStopReason Stage = WindowRecordingStopReason.CaptureFailed;
    /// <summary>Creates an isolated state owner for each API call.</summary>
    internal WindowRecordingWorker(WindowRecordingOptions options, IntPtr suppliedDevice, Func<IntPtr, IRecordingDevice> deviceFactory, Func<IntPtr, IntPtr, IRecordingSource> sourceFactory, Func<IntPtr, string, HevcVideoEncoderOptions, IRecordingEncoder> encoderFactory, IRecordingClock clock, RecordingOutputPublisher publisher) {
        Options = options; SuppliedDevice = suppliedDevice; DeviceFactory = deviceFactory; SourceFactory = sourceFactory; EncoderFactory = encoderFactory; Clock = clock; Publisher = publisher;
        OutputPath = Path.GetFullPath(options.OutputPath); Result = new() { RequestedDuration = options.Duration, Bitrate = options.Bitrate, MaxBitrate = options.MaxBitrate };
    }
    /// <summary>Runs the session and always closes every resource before returning its report.</summary>
    internal async Task<WindowRecordingResult> RunAsync(CancellationToken stopToken) {
        try {
            Stage = WindowRecordingStopReason.PublicationFailed;
            if (Publisher.Exists(OutputPath)) { throw new IOException("The output destination already exists: " + OutputPath); }
            string outputParent = Path.GetDirectoryName(OutputPath);
            if (!Publisher.DirectoryExists(outputParent)) { throw new DirectoryNotFoundException("The output parent directory must already exist: " + outputParent); }
            stopToken.ThrowIfCancellationRequested();
            Stage = WindowRecordingStopReason.CaptureFailed;
            Device = DeviceFactory(SuppliedDevice); Result.AdapterName = Device.Name; Result.AdapterLuid = Device.Luid;
            Source = SourceFactory(Options.WindowHandle, Device.Handle); Initial = await FirstFrameAsync(stopToken).ConfigureAwait(false);
            Result.SourceWidth = Initial.Width; Result.SourceHeight = Initial.Height; Result.OutputWidth = (Initial.Width + 1) & ~1; Result.OutputHeight = (Initial.Height + 1) & ~1;
            Stage = WindowRecordingStopReason.EncoderFailed; WorkPath = Publisher.CreateWorkFile();
            Encoder = EncoderFactory(Device.Handle, WorkPath, new() { SourceWidth = Initial.Width, SourceHeight = Initial.Height, Bitrate = Options.Bitrate, MaxBitrate = Options.MaxBitrate });
            Stage = WindowRecordingStopReason.CaptureFailed;
            // A bounded refresh cannot chase a continuously updating source forever.
            for (int refreshed = 0; refreshed < 3 && Source.TryAcquire(out var fresh); refreshed++) {
                if (!MatchesSize(fresh)) { fresh.Dispose(); SetFailure(WindowRecordingStopReason.Resized, "Window dimensions changed during encoder preparation."); break; }
                Initial.Dispose(); Initial = fresh; Result.PreparationDiscardedFrames++;
            }
            stopToken.ThrowIfCancellationRequested();
            if (Result.StopReason != WindowRecordingStopReason.Resized) {
                // WGC may deliver a compositor presentation timestamp just ahead of
                // host QPC. Await its eligibility as preparation, never present early.
                long eligibleCounter = checked((long)(((Int128)Initial.Timestamp.Ticks * Clock.CounterFrequency + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond));
                long beforeOrigin = Clock.GetCounter();
                if ((Int128)eligibleCounter - beforeOrigin > (Int128)Clock.CounterFrequency * 10) { throw new InvalidDataException("Capture timestamp is more than ten seconds ahead of the host clock."); }
                if (eligibleCounter > beforeOrigin) { await Clock.WaitUntilAsync(eligibleCounter, stopToken).ConfigureAwait(false); }
                Origin = Clock.GetCounter(); Timeline = new(Origin, Clock.CounterFrequency, Options.Duration); Selector = new(Timeline);
                await RecordSlotsAsync(stopToken).ConfigureAwait(false);
            }
        } catch (OperationCanceledException) when (stopToken.IsCancellationRequested) { SetStop(WindowRecordingStopReason.StopRequested); }
        catch (WindowCaptureClosedException) { SetStop(WindowRecordingStopReason.WindowClosed); }
        catch (TimeoutException error) { SetFailure(WindowRecordingStopReason.NoFrames, error.Message, error.HResult); }
        catch (Exception error) { SetFailure(Stage, error.Message, error.HResult); }
        if (Timeline != null) {
            Result.RecordingDuration = TimeSpan.FromTicks(checked((long)(((Int128)Clock.GetCounter() - Origin) * TimeSpan.TicksPerSecond / Clock.CounterFrequency)));
            Result.ResampledSourceDiscards = Selector.ResampledSourceDiscards;
        }
        if (Result.EncodedFrames > 0 && Result.StopReason is WindowRecordingStopReason.DurationReached or WindowRecordingStopReason.StopRequested or WindowRecordingStopReason.WindowClosed or WindowRecordingStopReason.Resized or WindowRecordingStopReason.EncoderTooSlow) {
            try {
                Stage = WindowRecordingStopReason.EncoderFailed; Encoder.Complete();
                Stage = WindowRecordingStopReason.PublicationFailed; Publisher.Publish(WorkPath, OutputPath); Result.HasOutput = true; Result.OutputPath = OutputPath;
            } catch (RecordingPublicationException error) { Result.HasOutput = true; Result.OutputPath = OutputPath; SetFailure(WindowRecordingStopReason.PublicationFailed, error.Message, error.HResult); }
            catch (Exception error) { SetFailure(Stage, error.Message, error.HResult); }
        }
        Release(Selector, "frame selection"); Release(Initial, "initial frame"); Release(Encoder, "encoder"); Release(Source, "capture");
        if (Source != null) { try { Result.CaptureStatistics = Source.Statistics; } catch (Exception error) { CleanupFailure("capture statistics", error); } }
        Release(Device, "device");
        if (WorkPath != null) { try { Publisher.DeleteWorkFile(WorkPath); } catch (Exception error) { CleanupFailure("work file", error); } }
        return Result;
    }
    /// <summary>Waits at most ten seconds for source dimensions without starting the recording clock.</summary>
    async Task<IRecordingFrame> FirstFrameAsync(CancellationToken stopToken) {
        long deadline = checked(Clock.GetCounter() + Clock.CounterFrequency * 10);
        for (;;) {
            stopToken.ThrowIfCancellationRequested();
            if (Source.State == WindowCaptureState.WindowClosed) { throw new TimeoutException("The source window closed before any output frame."); }
            if (Source.TryAcquire(out var frame)) { return frame; }
            long now = Clock.GetCounter(); if (now >= deadline) { throw new TimeoutException("No source frame arrived within ten seconds."); }
            await Clock.WaitUntilAsync(Math.Min(deadline, checked(now + Math.Max(1, Clock.CounterFrequency / 1000))), stopToken).ConfigureAwait(false);
        }
    }
    /// <summary>Emits one frame per rational slot, with no future content or unbounded backlog.</summary>
    async Task RecordSlotsAsync(CancellationToken stopToken) {
        for (long index = 0; ; index++) {
            stopToken.ThrowIfCancellationRequested(); Stage = WindowRecordingStopReason.CaptureFailed;
            await Clock.WaitUntilAsync(Timeline.GetDeadlineCounter(index), stopToken).ConfigureAwait(false);
            if (Source.State == WindowCaptureState.WindowClosed) { SetStop(WindowRecordingStopReason.WindowClosed); return; }
            if (Timeline.FrameLimit.HasValue && index >= Timeline.FrameLimit.Value) { SetStop(WindowRecordingStopReason.DurationReached); return; }
            if (CheckLag(index)) { return; }
            Selector.BeginSlot(index);
            if (Initial != null) { Selector.Accept(Initial); Initial = null; }
            while (Selector.CanAcquire && Source.TryAcquire(out var frame)) {
                if (!MatchesSize(frame)) { frame.Dispose(); SetFailure(WindowRecordingStopReason.Resized, "Window dimensions changed during recording."); return; }
                try { Selector.Accept(frame); } catch { frame.Dispose(); throw; }
            }
            stopToken.ThrowIfCancellationRequested(); var selected = Selector.Select();
            if (selected == null) { SetFailure(WindowRecordingStopReason.NoFrames, "No source observation is eligible for the recording origin."); return; }
            Stage = WindowRecordingStopReason.EncoderFailed; Encoder.Submit(selected.TextureHandle, index); Result.EncodedFrames++;
            Result.RepeatedFrames = Selector.RepeatedFrames;
            if (CheckLag(index)) { return; }
        }
    }
    /// <summary>Rejects changes without silently resizing, cropping or stretching the encoded stream.</summary>
    bool MatchesSize(IRecordingFrame frame) => frame.Width == Result.SourceWidth && frame.Height == Result.SourceHeight;
    /// <summary>Measures real output lag before and after submission, independently of static content.</summary>
    bool CheckLag(long index) {
        var lag = Timeline.GetLag(Clock.GetCounter(), index); if (lag > Result.MaxLag) { Result.MaxLag = lag; }
        if (lag <= TimeSpan.FromMilliseconds(250)) { return false; }
        SetFailure(WindowRecordingStopReason.EncoderTooSlow, "Output lag exceeded 250 ms."); return true;
    }
    /// <summary>Normalizes stops before any accepted output frame into an explicit NoFrames failure.</summary>
    void SetStop(WindowRecordingStopReason reason) {
        if (Result.EncodedFrames == 0) { SetFailure(WindowRecordingStopReason.NoFrames, "Recording stopped before any output frame was accepted."); }
        else { Result.StopReason = reason; }
    }
    /// <summary>Stores a primary operation diagnostic without converting a failure into success.</summary>
    void SetFailure(WindowRecordingStopReason reason, string message, int code = -1) { Result.StopReason = reason; Result.ErrorCode = code; Result.ErrorMessage = message; }
    /// <summary>Releases one owned component and records any secondary failure.</summary>
    void Release(IDisposable component, string name) { try { component?.Dispose(); } catch (Exception error) { CleanupFailure(name, error); } }
    /// <summary>Keeps original errors while exposing teardown problems instead of silently hiding them.</summary>
    void CleanupFailure(string name, Exception error) {
        Result.CleanupErrors.Add(name + ": " + error.Message);
        if (Result.ErrorCode == 0) { SetFailure(WindowRecordingStopReason.CaptureFailed, name + ": " + error.Message, error.HResult); }
    }
}
