using helengine.directx11.video;
namespace helengine.recording.windows.tests;
/// <summary>Builds worker tests with controlled external operations and real orchestration/publication.</summary>
sealed class RecordingTestDependencies {
    /// <summary>Deterministic host clock.</summary>
    internal readonly RecordingTestClock Clock = new();
    /// <summary>Controlled device lifetime.</summary>
    internal readonly RecordingTestDevice Device = new();
    /// <summary>Controlled source and window state.</summary>
    internal readonly RecordingTestSource Source;
    /// <summary>Controlled external codec latency/failure.</summary>
    internal readonly RecordingTestEncoder Encoder = new();
    /// <summary>Real publisher uses this storage model.</summary>
    internal readonly RecordingTestFileSystem Files = new();
    /// <summary>Setup event outside the measured timeline.</summary>
    internal Action OnEncoderOpen;
    /// <summary>Device observed by the capture factory.</summary>
    internal IntPtr CaptureDevice;
    /// <summary>Device observed by the encoder factory.</summary>
    internal IntPtr EncoderDevice;
    /// <summary>Creates one initial source observation unless the test removes it.</summary>
    internal RecordingTestDependencies() { Source = new(Clock); Source.Frames.Enqueue(new(0)); }
    /// <summary>Creates a recorder without any test pointer reaching native APIs.</summary>
    internal WindowRecorder Create() => new(_ => Device, OpenSource, OpenEncoder, Clock, new RecordingOutputPublisher(Files));
    /// <summary>Records source-device identity.</summary>
    IRecordingSource OpenSource(IntPtr hwnd, IntPtr device) { CaptureDevice = device; return Source; }
    /// <summary>Models expensive encoder setup and explicit finalization.</summary>
    IRecordingEncoder OpenEncoder(IntPtr device, string path, HevcVideoEncoderOptions options) {
        EncoderDevice = device; OnEncoderOpen?.Invoke(); Encoder.OnComplete ??= () => Files.Files[path] = [1, 2, 3, 4]; return Encoder;
    }
    /// <summary>Valid options use a fixture-only HWND and explicit destination.</summary>
    internal static WindowRecordingOptions Options(TimeSpan? duration = null) => new() { WindowHandle = (IntPtr)1, OutputPath = "C:\\temp\\helengine-window-recording\\recorded-test.mp4", Duration = duration ?? TimeSpan.FromSeconds(1) };
}
