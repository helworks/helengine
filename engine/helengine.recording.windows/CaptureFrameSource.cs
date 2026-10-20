using helengine.capture.windows;
namespace helengine.recording.windows;
/// <summary>Captures only the explicit HWND with the shared device and a hard budget of three.</summary>
sealed class CaptureFrameSource : IRecordingSource {
    /// <summary>Owns the native capture session.</summary>
    readonly WindowCaptureSession Session;
    /// <summary>Opens a source without cursor capture or an unsolicited border-removal request.</summary>
    internal CaptureFrameSource(IntPtr hwnd, IntPtr device) { Session = WindowCaptureSession.Open(hwnd, device, new() { BufferCapacity = 3 }); }
    /// <summary>Source identity/lifecycle state, never desktop fallback.</summary>
    public WindowCaptureState State => Session.State;
    /// <summary>Capture counters including final teardown snapshot.</summary>
    public WindowCaptureStatistics Statistics => Session.Statistics;
    /// <summary>Transfers one independent lease if content is pending.</summary>
    public bool TryAcquire(out IRecordingFrame frame) {
        frame = null;
        if (!Session.TryAcquireFrame(out var captured)) { return false; }
        try { frame = new CapturedRecordingFrame(captured); return true; } catch { captured.Dispose(); throw; }
    }
    /// <summary>Stops capture; outstanding frame leases remain independent.</summary>
    public void Dispose() => Session.Dispose();
}
