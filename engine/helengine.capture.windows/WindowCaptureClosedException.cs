namespace helengine.capture.windows;
/// <summary>Distinguishes source closure from a temporary absence of newly captured frames.</summary>
public sealed class WindowCaptureClosedException : InvalidOperationException {
    /// <summary>Reports termination of the original window capture identity.</summary>
    internal WindowCaptureClosedException() : base("The captured window has closed.") {}
}
