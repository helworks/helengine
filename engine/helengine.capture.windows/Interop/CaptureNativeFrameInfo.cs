namespace helengine.capture.windows.Interop;
/// <summary>Matches borrowed GPU pointers and immutable source metadata for one frame lease.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
struct CaptureNativeFrameInfo {
    /// <summary>Complete x64 metadata structure size.</summary>
    public uint Size;
    /// <summary>Expected native metadata version.</summary>
    public uint Version;
    /// <summary>ID3D11Texture2D pointer borrowed until frame disposal.</summary>
    public IntPtr Texture;
    /// <summary>ID3D11Device pointer borrowed until frame disposal.</summary>
    public IntPtr Device;
    /// <summary>Valid captured pixel width.</summary>
    public uint Width;
    /// <summary>Valid captured pixel height.</summary>
    public uint Height;
    /// <summary>Capture-specific pixel enum, not a raw DXGI_FORMAT value.</summary>
    public uint PixelFormat;
    /// <summary>Reserved v1 field, emitted as zero.</summary>
    public uint Reserved;
    /// <summary>Original SystemRelativeTime expressed as 100 ns TimeSpan ticks.</summary>
    public long TimestampTicks;
    /// <summary>Sequence of source observations, including library-side discards.</summary>
    public ulong SequenceNumber;
    /// <summary>Creates a versioned output buffer for acquisition.</summary>
    public static CaptureNativeFrameInfo Create() => new() { Size = (uint)Marshal.SizeOf<CaptureNativeFrameInfo>(), Version = 1 };
}
