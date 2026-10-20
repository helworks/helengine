namespace helengine.capture.windows;
/// <summary>Immutable library-side accounting; it cannot count frames never observed from Windows.</summary>
public sealed class WindowCaptureStatistics {
    /// <summary>Copies an internally consistent native snapshot.</summary>
    internal WindowCaptureStatistics(CaptureNativeStatistics value) {
        ObservedFrames = value.ObservedFrames; DeliveredFrames = value.DeliveredFrames;
        OverflowDiscardedFrames = value.OverflowDiscardedFrames; ResizeDiscardedFrames = value.ResizeDiscardedFrames;
        ShutdownDiscardedFrames = value.ShutdownDiscardedFrames;
    }
    /// <summary>Source frames actually obtained from the Windows frame pool.</summary>
    public ulong ObservedFrames { get; }
    /// <summary>Frames transferred to callers exactly once.</summary>
    public ulong DeliveredFrames { get; }
    /// <summary>New source frames discarded because pending and held frames occupied every slot.</summary>
    public ulong OverflowDiscardedFrames { get; }
    /// <summary>Observed frames discarded because their content did not fit valid source dimensions.</summary>
    public ulong ResizeDiscardedFrames { get; }
    /// <summary>Pending frames released on source closure, failure or session disposal.</summary>
    public ulong ShutdownDiscardedFrames { get; }
}
