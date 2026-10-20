using helengine.capture.windows;
namespace helengine.recording.windows.tests;
/// <summary>Provides controlled source observations and window-closure boundaries.</summary>
sealed class RecordingTestSource : IRecordingSource {
    /// <summary>Clock shared with source availability.</summary>
    readonly RecordingTestClock Clock;
    /// <summary>Queued frames are emitted once they reach host time.</summary>
    internal readonly Queue<TestRecordingFrame> Frames = new();
    /// <summary>Optional source closure in QPC units.</summary>
    internal long CloseAt = long.MaxValue;
    /// <summary>Injects a cleanup failure after all encoding work.</summary>
    internal bool FailDispose;
    /// <summary>Allows presentation timestamps slightly ahead of delivery, as observed in real capture.</summary>
    internal bool DeliverFutureFrames;
    /// <summary>Counts source shutdown operations.</summary>
    internal int DisposeCount;
    /// <summary>Creates one controllable observation stream.</summary>
    internal RecordingTestSource(RecordingTestClock clock) { Clock = clock; }
    /// <summary>Returns the source state at the current simulated instant.</summary>
    public WindowCaptureState State => Clock.Counter >= CloseAt ? WindowCaptureState.WindowClosed : WindowCaptureState.Running;
    /// <summary>Native capture statistics are covered by integration; fake has no native observations.</summary>
    public WindowCaptureStatistics Statistics => null;
    /// <summary>Transfers an available lease and distinguishes closure from temporarily absent content.</summary>
    public bool TryAcquire(out IRecordingFrame frame) {
        if (State == WindowCaptureState.WindowClosed) { frame = null; return false; }
        if (Frames.Count > 0 && (DeliverFutureFrames || Frames.Peek().Timestamp.Ticks <= Clock.Counter)) { frame = Frames.Dequeue(); return true; }
        frame = null; return false;
    }
    /// <summary>Releases queued fixture observations and optionally reproduces a teardown failure.</summary>
    public void Dispose() { DisposeCount++; while (Frames.Count > 0) { Frames.Dequeue().Dispose(); } if (FailDispose) { throw new IOException("cleanup failure"); } }
}
