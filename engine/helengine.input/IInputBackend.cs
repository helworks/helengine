namespace helengine;

/// <summary>
/// Captures a raw input frame from a platform-specific backend.
/// </summary>
public interface IInputBackend {
    /// <summary>
    /// Collects the current raw input state.
    /// </summary>
    /// <returns>Captured input frame.</returns>
    InputFrameState CaptureFrame();
}
