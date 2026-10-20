namespace helengine.recording.windows.tests;
/// <summary>Controls external encoding latency while the real recorder runs its scheduling and lifecycle.</summary>
sealed class RecordingTestEncoder : IRecordingEncoder {
    /// <summary>Submitted indices reveal skipped or extra slots.</summary>
    internal readonly List<long> Indices = new();
    /// <summary>Submitted source identities reveal a stale initial observation.</summary>
    internal readonly List<IntPtr> Textures = new();
    /// <summary>Injects latency or cancellation after a frame was accepted.</summary>
    internal Action OnSubmit;
    /// <summary>Completes the fixture workfile or throws its original mux error.</summary>
    internal Action OnComplete;
    /// <summary>Counts resource releases.</summary>
    internal int DisposeCount;
    /// <summary>Number of successful drain requests.</summary>
    internal int CompleteCount;
    /// <summary>Records real scheduler input at the encoder boundary.</summary>
    public void Submit(IntPtr texture, long index) { Indices.Add(index); Textures.Add(texture); OnSubmit?.Invoke(); }
    /// <summary>Produces a completed fixture only through explicit finalization.</summary>
    public void Complete() { OnComplete?.Invoke(); CompleteCount++; }
    /// <summary>Records abort/release independently from completion.</summary>
    public void Dispose() => DisposeCount++;
}
