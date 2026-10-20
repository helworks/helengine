using helengine.directx11.video;
namespace helengine.recording.windows;
/// <summary>Records an explicit HWND as GPU HEVC at constant 60 FPS on a background worker.</summary>
public sealed class WindowRecorder {
    /// <summary>Creates or retains the shared recording device.</summary>
    readonly Func<IntPtr, IRecordingDevice> DeviceFactory;
    /// <summary>Creates the bounded source on the selected device.</summary>
    readonly Func<IntPtr, IntPtr, IRecordingSource> SourceFactory;
    /// <summary>Creates a texture encoder with no window dependency.</summary>
    readonly Func<IntPtr, string, HevcVideoEncoderOptions, IRecordingEncoder> EncoderFactory;
    /// <summary>Stateless monotonic clock shared by independent workers.</summary>
    readonly IRecordingClock Clock;
    /// <summary>Publishes only finalized work files.</summary>
    readonly RecordingOutputPublisher Publisher;
    /// <summary>Creates the native Windows/NVIDIA recording pipeline.</summary>
    public WindowRecorder() : this(RecordingDevice.Open, OpenSource, OpenEncoder, new RecordingClock(), new RecordingOutputPublisher(new RecordingFileSystem())) { }
    /// <summary>Supplies external boundaries for deterministic lifecycle and storage tests.</summary>
    internal WindowRecorder(Func<IntPtr, IRecordingDevice> deviceFactory, Func<IntPtr, IntPtr, IRecordingSource> sourceFactory, Func<IntPtr, string, HevcVideoEncoderOptions, IRecordingEncoder> encoderFactory, IRecordingClock clock, RecordingOutputPublisher publisher) {
        DeviceFactory = deviceFactory; SourceFactory = sourceFactory; EncoderFactory = encoderFactory; Clock = clock; Publisher = publisher;
    }
    /// <summary>Records using the first compatible NVIDIA hardware adapter; cancellation requests graceful completion.</summary>
    public Task<WindowRecordingResult> RecordAsync(WindowRecordingOptions options, CancellationToken stopToken = default) => Start(options, IntPtr.Zero, stopToken);
    /// <summary>Records on a valid host-owned D3D11 device, retaining an independent reference; cancellation is graceful stop.</summary>
    public Task<WindowRecordingResult> RecordAsync(WindowRecordingOptions options, IntPtr deviceHandle, CancellationToken stopToken = default) {
        if (deviceHandle == IntPtr.Zero) { throw new ArgumentException("A valid host D3D11 device is required.", nameof(deviceHandle)); }
        return Start(options, deviceHandle, stopToken);
    }
    /// <summary>Validates arguments synchronously and keeps GPU setup off the calling UI thread.</summary>
    Task<WindowRecordingResult> Start(WindowRecordingOptions options, IntPtr device, CancellationToken stopToken) {
        ArgumentNullException.ThrowIfNull(options); options.Validate();
        var worker = new WindowRecordingWorker(options, device, DeviceFactory, SourceFactory, EncoderFactory, Clock, Publisher);
        return Task.Run(() => worker.RunAsync(stopToken), CancellationToken.None);
    }
    /// <summary>Creates capture with its explicit hard budget and source HWND.</summary>
    static IRecordingSource OpenSource(IntPtr hwnd, IntPtr device) => new CaptureFrameSource(hwnd, device);
    /// <summary>Creates the reusable GPU texture encoder.</summary>
    static IRecordingEncoder OpenEncoder(IntPtr device, string path, HevcVideoEncoderOptions options) => new RecordingEncoder(device, path, options);
}
