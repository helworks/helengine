using System.Runtime.InteropServices;
using helengine.capture.windows.Interop;

namespace helengine.capture.windows.tests;

/// <summary>Checks the managed/native boundary without capturing any windows.</summary>
[Trait("Category", "Contract")]
public sealed class CaptureContractTests {
    /// <summary>The defaults preserve the approved bounded allocation and cursor/border policy.</summary>
    [Fact]
    public void OptionsHaveApprovedDefaults() {
        var options = new WindowCaptureOptions();
        Assert.Equal(3, options.BufferCapacity); Assert.False(options.CaptureCursor); Assert.False(options.RemoveBorder);
    }
    /// <summary>Invalid managed arguments are rejected before loading or invoking native capture.</summary>
    [Fact]
    public void RejectsNullOptionsZeroWindowAndNonPositiveCapacity() {
        Assert.Throws<ArgumentNullException>(() => WindowCaptureSession.Open(new IntPtr(-1), null));
        Assert.Throws<ArgumentException>(() => WindowCaptureSession.Open(IntPtr.Zero, new()));
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowCaptureSession.Open(new IntPtr(-1), new() { BufferCapacity = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowCaptureSession.Open(new IntPtr(-1), new() { BufferCapacity = -1 }));
        Assert.Throws<ArgumentException>(() => WindowCaptureSession.Open(new IntPtr(-1), IntPtr.Zero, new()));
    }
    /// <summary>Sizes and offsets match the x64 version-one C ABI rather than relying on inferred packing.</summary>
    [Fact]
    public void NativeLayoutsMatchX64Abi() {
        Assert.Equal(20, Marshal.SizeOf<CaptureNativeOptions>()); Assert.Equal(1164, Marshal.SizeOf<CaptureNativeError>());
        Assert.Equal(56, Marshal.SizeOf<CaptureNativeFrameInfo>()); Assert.Equal(48, Marshal.SizeOf<CaptureNativeStatistics>());
        Assert.Equal(8, Marshal.OffsetOf<CaptureNativeFrameInfo>(nameof(CaptureNativeFrameInfo.Texture)).ToInt32());
        Assert.Equal(40, Marshal.OffsetOf<CaptureNativeFrameInfo>(nameof(CaptureNativeFrameInfo.TimestampTicks)).ToInt32());
    }
    /// <summary>The actual native failure remains distinguishable from an empty session poll.</summary>
    [Fact]
    public void InvalidWindowPreservesNativeDiagnostic() {
        var error = Assert.Throws<WindowCaptureException>(() => WindowCaptureSession.Open(new IntPtr(-1), new()));
        Assert.Equal("open", error.Operation); Assert.Equal(unchecked((int)0x80070578), error.HResult);
        Assert.Contains("window", error.Message, StringComparison.OrdinalIgnoreCase);
    }
    /// <summary>Pixel allocation is checked before multiplication can overflow a managed array size.</summary>
    [Fact]
    public void CannotReadPixelsIntoOverflowedAllocation() {
        Assert.Equal(60, CapturePixelBufferUtils.GetByteCount(5, 3));
        Assert.Throws<OverflowException>(() => CapturePixelBufferUtils.GetByteCount(int.MaxValue, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => CapturePixelBufferUtils.GetByteCount(0, 3));
    }
}
