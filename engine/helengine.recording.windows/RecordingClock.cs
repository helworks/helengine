using System.Diagnostics;
namespace helengine.recording.windows;
/// <summary>Uses host QPC with coarse asynchronous waits and a bounded final yield.</summary>
sealed class RecordingClock : IRecordingClock {
    /// <summary>Host QPC frequency.</summary>
    public long CounterFrequency => Stopwatch.Frequency;
    /// <summary>Current host counter, on the same clock as capture SystemRelativeTime.</summary>
    public long GetCounter() => Stopwatch.GetTimestamp();
    /// <summary>Waits close to the deadline without rounding each frame period into a cumulative delay.</summary>
    public async Task WaitUntilAsync(long deadline, CancellationToken stopToken) {
        for (;;) {
            stopToken.ThrowIfCancellationRequested(); long remaining = deadline - GetCounter();
            if (remaining <= 0) { return; }
            double milliseconds = (double)remaining * 1000 / CounterFrequency;
            if (milliseconds > 2) { await Task.Delay(TimeSpan.FromMilliseconds(milliseconds - 1), stopToken).ConfigureAwait(false); }
            else { Thread.SpinWait(100); Thread.Yield(); }
        }
    }
}
