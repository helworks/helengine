namespace helengine.recording.windows;
/// <summary>Maps exact 60/1 slots to QPC counters without accumulated TimeSpan rounding.</summary>
sealed class CfrTimeline {
    /// <summary>Host counter at the end of preparation.</summary>
    readonly long Origin;
    /// <summary>Counter ticks per second, independent of capture's 100 ns representation.</summary>
    readonly long Frequency;
    /// <summary>Creates a fixed or indefinite timeline with a rational origin.</summary>
    internal CfrTimeline(long originCounter, long counterFrequency, TimeSpan? duration) {
        ArgumentOutOfRangeException.ThrowIfNegative(originCounter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(counterFrequency);
        if (duration.HasValue && duration.Value <= TimeSpan.Zero) { throw new ArgumentOutOfRangeException(nameof(duration)); }
        Origin = originCounter; Frequency = counterFrequency;
        FrameLimit = duration.HasValue ? checked((long)(((Int128)duration.Value.Ticks * 60 + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond)) : null;
    }
    /// <summary>Exact ceiling of requested seconds multiplied by 60, or null for manual stop.</summary>
    internal long? FrameLimit { get; }
    /// <summary>Returns floor(origin + frameIndex * frequency / 60) in host counter units.</summary>
    internal long GetDeadlineCounter(long frameIndex) {
        ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);
        return checked((long)(Origin + (Int128)frameIndex * Frequency / 60));
    }
    /// <summary>Compares the source time to the exact rational slot, without advancing future content.</summary>
    internal bool IsEligible(long sourceTimestampTicks, long frameIndex) {
        ArgumentOutOfRangeException.ThrowIfNegative(sourceTimestampTicks); ArgumentOutOfRangeException.ThrowIfNegative(frameIndex);
        return (Int128)sourceTimestampTicks * Frequency * 60 <= ((Int128)Origin * 60 + (Int128)frameIndex * Frequency) * TimeSpan.TicksPerSecond;
    }
    /// <summary>Rounds positive lateness upward to 100 ns so exceeding the limit cannot be hidden.</summary>
    internal TimeSpan GetLag(long currentCounter, long frameIndex) {
        Int128 late = ((Int128)currentCounter - Origin) * 60 - (Int128)frameIndex * Frequency;
        if (late <= 0) { return TimeSpan.Zero; }
        Int128 divisor = (Int128)Frequency * 60;
        return TimeSpan.FromTicks(checked((long)((late * TimeSpan.TicksPerSecond + divisor - 1) / divisor)));
    }
}
