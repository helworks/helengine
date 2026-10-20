namespace helengine.capture.windows.Interop;
/// <summary>Owns a native capture session independently of every frame it has delivered.</summary>
sealed class CaptureSessionHandle : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid {
    /// <summary>Adopts one successful native session allocation.</summary>
    internal CaptureSessionHandle(IntPtr value) : base(true) { SetHandle(value); }
    /// <summary>Stops native capture and releases its unique owner exactly once.</summary>
    protected override bool ReleaseHandle() { CaptureNativeApi.Close(handle); return true; }
}
