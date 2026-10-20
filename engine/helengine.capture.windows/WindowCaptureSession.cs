namespace helengine.capture.windows;
/// <summary>Captures one Windows HWND without an engine loop; callers serialize acquisition and disposal.</summary>
public sealed class WindowCaptureSession : IDisposable {
    /// <summary>Native capture owner; frames have separate handles and do not retain this session.</summary>
    readonly CaptureSessionHandle Handle;
    /// <summary>Preserves final accounting after native capture has been stopped.</summary>
    WindowCaptureStatistics FinalStatistics = new(CaptureNativeStatistics.Create());
    /// <summary>Prevents repeated stop requests and supports post-disposal state queries.</summary>
    bool Disposed;
    /// <summary>Adopts one successfully initialized native session.</summary>
    WindowCaptureSession(CaptureSessionHandle handle) { Handle = handle; }
    /// <summary>Opens a persistent session using a new hardware D3D11 device; call off the UI thread if waiting is undesirable.</summary>
    public static WindowCaptureSession Open(IntPtr windowHandle, WindowCaptureOptions options) => OpenCore(windowHandle, IntPtr.Zero, options);
    /// <summary>Opens a session on an existing valid ID3D11Device, retaining a separate native COM reference.</summary>
    public static WindowCaptureSession Open(IntPtr windowHandle, IntPtr deviceHandle, WindowCaptureOptions options) {
        if (deviceHandle == IntPtr.Zero) { throw new ArgumentException("A valid ID3D11Device pointer is required.", nameof(deviceHandle)); }
        return OpenCore(windowHandle, deviceHandle, options);
    }
    /// <summary>Distinguishes an inactive source from an active session temporarily without new content.</summary>
    public WindowCaptureState State {
        get {
            if (Disposed) { return WindowCaptureState.Disposed; }
            var error = CaptureNativeError.Create();
            if (CaptureNativeApi.GetState(Handle, out var state, ref error) != 0) { error.Throw(); }
            return state;
        }
    }
    /// <summary>Reports known library-side deliveries and discards, including the final shutdown snapshot.</summary>
    public WindowCaptureStatistics Statistics {
        get {
            if (Disposed) { return FinalStatistics; }
            var stats = CaptureNativeStatistics.Create(); var error = CaptureNativeError.Create();
            if (CaptureNativeApi.GetStatistics(Handle, ref stats, ref error) != 0) { error.Throw(); }
            return new(stats);
        }
    }
    /// <summary>Transfers each newly available frame once; closure and failures are explicit exceptions.</summary>
    public bool TryAcquireFrame(out CapturedWindowFrame frame) {
        ObjectDisposedException.ThrowIf(Disposed, this); frame = null;
        var info = CaptureNativeFrameInfo.Create(); var error = CaptureNativeError.Create();
        int result = CaptureNativeApi.TryAcquire(Handle, out var pointer, ref info, ref error);
        if (result == -1) { error.Throw(); }
        if (result == 2) { throw new WindowCaptureClosedException(); }
        if (result == 0) { return false; }
        if (result != 1 || pointer == IntPtr.Zero) { throw new InvalidDataException("Native acquisition returned an invalid result."); }
        var handle = new CaptureFrameHandle(pointer);
        try { frame = new(handle, info); return true; } catch { handle.Dispose(); throw; }
    }
    /// <summary>Stops capture and saves final accounting without invalidating frames already held by consumers.</summary>
    public void Dispose() {
        if (Disposed) { return; }
        var stats = CaptureNativeStatistics.Create(); var stopError = CaptureNativeError.Create(); var statsError = CaptureNativeError.Create();
        int stopResult; int statsResult;
        try {
            stopResult = CaptureNativeApi.Stop(Handle, ref stats, ref stopError);
            statsResult = CaptureNativeApi.GetStatistics(Handle, ref stats, ref statsError);
            if (statsResult == 0) { FinalStatistics = new(stats); }
        } finally { Disposed = true; Handle.Dispose(); }
        if (stopResult != 0) { stopError.Throw(); }
        if (statsResult != 0) { statsError.Throw(); }
    }
    /// <summary>Validates arguments before native allocation and adopts only a successfully initialized owner.</summary>
    static WindowCaptureSession OpenCore(IntPtr windowHandle, IntPtr deviceHandle, WindowCaptureOptions options) {
        ArgumentNullException.ThrowIfNull(options); options.Validate();
        if (windowHandle == IntPtr.Zero) { throw new ArgumentException("A nonzero HWND is required.", nameof(windowHandle)); }
        var nativeOptions = CaptureNativeOptions.Create(options); var error = CaptureNativeError.Create();
        if (CaptureNativeApi.Open(windowHandle, deviceHandle, ref nativeOptions, out var pointer, ref error) != 0) { error.Throw(); }
        if (pointer == IntPtr.Zero) { throw new InvalidDataException("Native capture returned an empty session handle."); }
        var handle = new CaptureSessionHandle(pointer);
        try { return new(handle); } catch { handle.Dispose(); throw; }
    }
}
