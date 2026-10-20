namespace helengine.capture.windows;
/// <summary>Describes capture availability independently of whether a new frame is waiting.</summary>
public enum WindowCaptureState {
    /// <summary>Capture is active; an empty acquisition means the source has no available new frame.</summary>
    Running = 0,
    /// <summary>The original source closed; the session never binds to a replacement window.</summary>
    WindowClosed = 1,
    /// <summary>A native failure terminated delivery and is reported on acquisition.</summary>
    Faulted = 2,
    /// <summary>The session stopped and released its native capture resources.</summary>
    Disposed = 3
}
