namespace helengine.recording.windows.tests;
/// <summary>Device metadata for worker tests; its pointer is never passed to graphics APIs.</summary>
sealed class RecordingTestDevice : IRecordingDevice {
    /// <summary>Identity observed at both factories.</summary>
    public IntPtr Handle => (IntPtr)123;
    /// <summary>Distinct adapter identity for the final report.</summary>
    public string Name => "Test NVIDIA";
    /// <summary>Deterministic adapter identifier.</summary>
    public long Luid => 42;
    /// <summary>Counts disposal independently from other resources.</summary>
    internal int DisposeCount;
    /// <summary>Releases the fixture owner.</summary>
    public void Dispose() => DisposeCount++;
}
