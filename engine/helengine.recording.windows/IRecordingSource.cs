using helengine.capture.windows;
namespace helengine.recording.windows;
/// <summary>Transfers captured leases and distinguishes pending content from source termination.</summary>
interface IRecordingSource : IDisposable {
    /// <summary>Current window capture lifecycle state.</summary>
    WindowCaptureState State { get; }
    /// <summary>Known capture-side counters, retained after disposal.</summary>
    WindowCaptureStatistics Statistics { get; }
    /// <summary>Transfers ownership once when a new frame is available.</summary>
    bool TryAcquire(out IRecordingFrame frame);
}
