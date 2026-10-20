namespace helengine.capture.windows.Interop;
/// <summary>Blittable snapshot shared with the native statistics ABI.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
struct CaptureNativeStatistics {
    /// <summary>Complete statistics byte size.</summary>
    public uint Size;
    /// <summary>Supported accounting ABI version.</summary>
    public uint Version;
    /// <summary>Source frames observed in the Windows pool.</summary>
    public ulong ObservedFrames;
    /// <summary>Frames transferred to the consumer.</summary>
    public ulong DeliveredFrames;
    /// <summary>Frames discarded by the allocation limit.</summary>
    public ulong OverflowDiscardedFrames;
    /// <summary>Frames discarded for invalid content dimensions.</summary>
    public ulong ResizeDiscardedFrames;
    /// <summary>Pending frames discarded at termination.</summary>
    public ulong ShutdownDiscardedFrames;
    /// <summary>Creates a valid snapshot output buffer before any native call.</summary>
    public static CaptureNativeStatistics Create() => new() { Size = (uint)Marshal.SizeOf<CaptureNativeStatistics>(), Version = 1 };
}
