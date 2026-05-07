namespace helengine;

/// <summary>
/// Captures a raw input frame from a platform-specific backend.
/// </summary>
public interface IInputBackend {
    /// <summary>
    /// Gets or sets whether the backend should continue reporting keyboard and button input while its host window is not foreground active.
    /// </summary>
    bool ReceiveInputInBackground { get; set; }

    /// <summary>
    /// Collects the current raw input state.
    /// </summary>
    /// <returns>Captured input frame.</returns>
    InputFrameState CaptureFrame();
}
