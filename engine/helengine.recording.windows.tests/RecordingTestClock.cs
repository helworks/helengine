namespace helengine.recording.windows.tests;
/// <summary>Advances deterministic QPC time without sleeping or interacting with user windows.</summary>
sealed class RecordingTestClock : IRecordingClock {
    /// <summary>Controllable current time in ten-millionths of a second.</summary>
    internal long Counter;
    /// <summary>Optional controlled event at a deadline.</summary>
    internal Action OnWait;
    /// <summary>Matches 100 ns fixture observation units.</summary>
    public long CounterFrequency => 10_000_000;
    /// <summary>Reads current simulated host time.</summary>
    public long GetCounter() => Counter;
    /// <summary>Moves forward to the deadline, then exposes stop/closure races deterministically.</summary>
    public Task WaitUntilAsync(long deadline, CancellationToken stopToken) {
        Counter = Math.Max(Counter, deadline); OnWait?.Invoke(); stopToken.ThrowIfCancellationRequested(); return Task.CompletedTask;
    }
}
