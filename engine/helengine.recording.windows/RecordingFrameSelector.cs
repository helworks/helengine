namespace helengine.recording.windows;
/// <summary>Selects the most recent eligible observation while owning at most current plus future.</summary>
sealed class RecordingFrameSelector : IDisposable {
    /// <summary>Exact comparison between host slots and source timestamps.</summary>
    readonly CfrTimeline Timeline;
    /// <summary>Eligible texture held for output or repetition.</summary>
    IRecordingFrame Current;
    /// <summary>First observation newer than the current slot, never presented early.</summary>
    IRecordingFrame Future;
    /// <summary>Distinguishes displayed frames from superseded intermediate observations.</summary>
    bool CurrentPresented;
    /// <summary>Prevents more than one selection for the same output slot.</summary>
    bool Selected = true;
    /// <summary>Expected consecutive output slot.</summary>
    long NextSlot;
    /// <summary>Active output index.</summary>
    long Slot = -1;
    /// <summary>Last accepted source time for order validation.</summary>
    long LastTimestamp = -1;
    /// <summary>Guards access after lease cleanup.</summary>
    bool Disposed;
    /// <summary>Creates a selector with no initial source observation.</summary>
    internal RecordingFrameSelector(CfrTimeline timeline) { Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline)); }
    /// <summary>Number of output slots reusing the same source observation.</summary>
    internal long RepeatedFrames { get; private set; }
    /// <summary>Eligible observations superseded before they were ever displayed.</summary>
    internal long ResampledSourceDiscards { get; private set; }
    /// <summary>Stops acquisition as soon as one future observation is retained.</summary>
    internal bool CanAcquire { get { ObjectDisposedException.ThrowIf(Disposed, this); return Slot >= 0 && !Selected && Future == null; } }
    /// <summary>Promotes an eligible future observation before further source acquisition.</summary>
    internal void BeginSlot(long frameIndex) {
        ObjectDisposedException.ThrowIf(Disposed, this);
        if (!Selected || frameIndex != NextSlot) { throw new InvalidOperationException("Output slots must be selected once in consecutive order."); }
        Slot = frameIndex; NextSlot++; Selected = false;
        if (Future != null && Timeline.IsEligible(Future.Timestamp.Ticks, Slot)) {
            var promoted = Future; Future = null; ReplaceCurrent(promoted);
        }
    }
    /// <summary>Adopts a successful source observation; the caller retains ownership on rejection.</summary>
    internal void Accept(IRecordingFrame frame) {
        ArgumentNullException.ThrowIfNull(frame);
        if (!CanAcquire) { throw new InvalidOperationException("A future frame is already retained or this slot has ended."); }
        long timestamp = frame.Timestamp.Ticks;
        if (timestamp < LastTimestamp || timestamp < 0) { throw new InvalidDataException("Capture timestamps regressed."); }
        if (Timeline.IsEligible(timestamp, Slot)) { ReplaceCurrent(frame); } else { Future = frame; }
        LastTimestamp = timestamp;
    }
    /// <summary>Returns a borrowed current lease, counting repetition only when content was already presented.</summary>
    internal IRecordingFrame Select() {
        ObjectDisposedException.ThrowIf(Disposed, this);
        if (Selected) { throw new InvalidOperationException("This output slot was already selected."); }
        Selected = true;
        if (Current != null) { if (CurrentPresented) { RepeatedFrames++; } CurrentPresented = true; }
        return Current;
    }
    /// <summary>Releases the old current lease and counts only intermediate unpresented observations.</summary>
    void ReplaceCurrent(IRecordingFrame frame) {
        if (Current != null) { if (!CurrentPresented) { ResampledSourceDiscards++; } Current.Dispose(); }
        Current = frame; CurrentPresented = false;
    }
    /// <summary>Returns both retained leases exactly once.</summary>
    public void Dispose() {
        if (Disposed) { return; } Disposed = true;
        try { Current?.Dispose(); } finally { Current = null; Future?.Dispose(); Future = null; }
    }
}
