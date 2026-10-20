using Microsoft.Win32.SafeHandles;
namespace helengine.directx11.video;
/// <summary>Aborts and releases an encoder exactly once; never implicitly publishes a video.</summary>
sealed class HevcEncoderHandle : SafeHandleZeroOrMinusOneIsInvalid {
    /// <summary>Adopts a successfully created native owner.</summary>
    internal HevcEncoderHandle(IntPtr pointer) : base(true) { SetHandle(pointer); }
    /// <summary>Releases resources even when the host forgot deterministic disposal.</summary>
    protected override bool ReleaseHandle() { HevcEncoderNativeApi.Destroy(handle); return true; }
}
