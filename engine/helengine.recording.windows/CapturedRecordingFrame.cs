using helengine.capture.windows;
namespace helengine.recording.windows;
/// <summary>Adapts a capture lease without downloading or copying pixels through the CPU.</summary>
sealed class CapturedRecordingFrame : IRecordingFrame {
    /// <summary>Owns the transferred capture frame.</summary>
    readonly CapturedWindowFrame Frame;
    /// <summary>Adopts one successfully acquired frame.</summary>
    internal CapturedRecordingFrame(CapturedWindowFrame frame) { Frame = frame; }
    /// <summary>Unmodified QPC observation timestamp.</summary>
    public TimeSpan Timestamp => Frame.Timestamp;
    /// <summary>Unscaled source width.</summary>
    public int Width => Frame.Width;
    /// <summary>Unscaled source height.</summary>
    public int Height => Frame.Height;
    /// <summary>Borrowed BGRA texture until Dispose.</summary>
    public IntPtr TextureHandle => Frame.TextureHandle;
    /// <summary>Returns the capture budget and frame resources.</summary>
    public void Dispose() => Frame.Dispose();
}
