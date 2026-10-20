namespace helengine.capture.windows.Interop;
/// <summary>Defines the complete version-one capture ABI using operation-owned diagnostics.</summary>
static class CaptureNativeApi {
    /// <summary>Library filename resolved beside the managed assembly or by the host's native loader.</summary>
    const string Library = "helengine.capture.windows.native";
    /// <summary>Creates a native session, importing a device only when its pointer is nonzero.</summary>
    [DllImport(Library, EntryPoint = "he_capture_open", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int Open(IntPtr window, IntPtr device, ref CaptureNativeOptions options, out IntPtr session, ref CaptureNativeError error);
    /// <summary>Transfers an owned frame or returns an explicit empty/closed/error status.</summary>
    [DllImport(Library, EntryPoint = "he_capture_try_acquire", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int TryAcquire(CaptureSessionHandle session, out IntPtr frame, ref CaptureNativeFrameInfo info, ref CaptureNativeError error);
    /// <summary>Reads capture state without conflating closure with an empty poll.</summary>
    [DllImport(Library, EntryPoint = "he_capture_get_state", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int GetState(CaptureSessionHandle session, out WindowCaptureState state, ref CaptureNativeError error);
    /// <summary>Reads library-side capture accounting.</summary>
    [DllImport(Library, EntryPoint = "he_capture_get_statistics", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int GetStatistics(CaptureSessionHandle session, ref CaptureNativeStatistics stats, ref CaptureNativeError error);
    /// <summary>Copies packed BGRA bytes while keeping the native frame alive for the entire call.</summary>
    [DllImport(Library, EntryPoint = "he_capture_copy_pixels", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int CopyPixels(CaptureFrameHandle frame, [Out] byte[] destination, ulong length, ref CaptureNativeError error);
    /// <summary>Stops native capture and returns its post-cleanup accounting.</summary>
    [DllImport(Library, EntryPoint = "he_capture_stop", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern int Stop(CaptureSessionHandle session, ref CaptureNativeStatistics stats, ref CaptureNativeError error);
    /// <summary>Destroys the session owner from its SafeHandle release path.</summary>
    [DllImport(Library, EntryPoint = "he_capture_close", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void Close(IntPtr session);
    /// <summary>Destroys one native frame owner from its independent SafeHandle.</summary>
    [DllImport(Library, EntryPoint = "he_capture_release_frame", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    internal static extern void ReleaseFrame(IntPtr frame);
}
