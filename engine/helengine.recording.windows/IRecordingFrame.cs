namespace helengine.recording.windows;
/// <summary>Owns one captured texture lease and its unmodified source observation time.</summary>
interface IRecordingFrame : IDisposable {
    /// <summary>QPC SystemRelativeTime in 100 ns units.</summary>
    TimeSpan Timestamp { get; }
    /// <summary>Original unscaled content width.</summary>
    int Width { get; }
    /// <summary>Original unscaled content height.</summary>
    int Height { get; }
    /// <summary>Borrowed same-device BGRA texture valid until this lease is disposed.</summary>
    IntPtr TextureHandle { get; }
}
