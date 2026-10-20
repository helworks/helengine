namespace helengine.recording.windows;
/// <summary>Observable normal stops and explicit failures; valid shortened output can accompany failure.</summary>
public enum WindowRecordingStopReason {
    /// <summary>All requested 60 FPS output slots completed.</summary>
    DurationReached,
    /// <summary>The host requested a graceful stop.</summary>
    StopRequested,
    /// <summary>The explicit source window closed after at least one accepted frame.</summary>
    WindowClosed,
    /// <summary>No output frame was accepted before stop, closure or initial timeout.</summary>
    NoFrames,
    /// <summary>The source changed dimensions, so only the accepted prefix may be finalized.</summary>
    Resized,
    /// <summary>Output lag exceeded 250 ms.</summary>
    EncoderTooSlow,
    /// <summary>Capture or device initialization failed.</summary>
    CaptureFailed,
    /// <summary>Codec setup, submission or finalization failed.</summary>
    EncoderFailed,
    /// <summary>The completed work file could not be published or cleaned up.</summary>
    PublicationFailed
}
