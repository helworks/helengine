namespace helengine.recording.windows.tests;
/// <summary>Tracks actual lease lifetime in deterministic selection tests, without a GPU dependency.</summary>
sealed class TestRecordingFrame : IRecordingFrame {
    /// <summary>Creates one source observation with independently controlled time and dimensions.</summary>
    internal TestRecordingFrame(long ticks, int width = 640, int height = 480) { Timestamp = TimeSpan.FromTicks(ticks); Width = width; Height = height; }
    /// <summary>Original observation time in SystemRelativeTime units.</summary>
    public TimeSpan Timestamp { get; }
    /// <summary>Source width used to exercise resize handling.</summary>
    public int Width { get; }
    /// <summary>Source height used to exercise resize handling.</summary>
    public int Height { get; }
    /// <summary>Sentinel accepted only by test encoders, never passed to native code.</summary>
    public IntPtr TextureHandle => (IntPtr)(Timestamp.Ticks + 1);
    /// <summary>Number of releases, allowing double-release detection.</summary>
    internal int DisposeCount { get; private set; }
    /// <summary>Records each transfer of ownership back to the source.</summary>
    public void Dispose() => DisposeCount++;
}
