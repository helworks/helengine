namespace helengine.recording.windows;
/// <summary>Monotonic host time and deadline waits, independent from capture timestamps.</summary>
interface IRecordingClock {
    /// <summary>Counter ticks per second.</summary>
    long CounterFrequency { get; }
    /// <summary>Reads the current monotonic counter.</summary>
    long GetCounter();
    /// <summary>Waits until a counter deadline or requests graceful stop through cancellation.</summary>
    Task WaitUntilAsync(long deadline, CancellationToken stopToken);
}
