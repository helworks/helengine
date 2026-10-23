namespace helengine.tools.windowrecorderlinux;

/// <summary>Maps elapsed wall time to CFR60 slots while preserving missed intervals as explicit repeats.</summary>
public sealed class FrameSchedule {
    /// <summary>Constructs one positive, bounded CFR timeline.</summary>
    public FrameSchedule(long frameCount) {
        if (frameCount < 1 || frameCount > 18000) { throw new ArgumentOutOfRangeException(nameof(frameCount)); }
        FrameCount = frameCount;
    }
    /// <summary>Exact number of output slots that must be delivered before finalization.</summary>
    public long FrameCount { get; }
    /// <summary>Returns the most recent slot at elapsed time, or the end sentinel after the requested capture interval.</summary>
    public long SlotAt(TimeSpan elapsed) {
        if (elapsed < TimeSpan.Zero) { throw new ArgumentOutOfRangeException(nameof(elapsed)); }
        return (long)Math.Min(FrameCount, (decimal)elapsed.Ticks * 60 / TimeSpan.TicksPerSecond);
    }
    /// <summary>Returns the wall-clock deadline for a slot, using ticks only at the scheduling boundary.</summary>
    public TimeSpan Deadline(long index) {
        if (index < 0 || index > FrameCount) { throw new ArgumentOutOfRangeException(nameof(index)); }
        return TimeSpan.FromTicks((long)decimal.Ceiling((decimal)index * TimeSpan.TicksPerSecond / 60));
    }
}
