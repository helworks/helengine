namespace helengine.capture.windows.Interop;
/// <summary>Owns a GPU frame without retaining or depending on its capture session.</summary>
sealed class CaptureFrameHandle : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid {
    /// <summary>Adopts one native frame lease returned by acquisition.</summary>
    internal CaptureFrameHandle(IntPtr value) : base(true) { SetHandle(value); }
    /// <summary>Releases the texture and its capacity token exactly once.</summary>
    protected override bool ReleaseHandle() { CaptureNativeApi.ReleaseFrame(handle); return true; }
}
