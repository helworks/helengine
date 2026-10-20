namespace helengine.capture.windows.Interop;
/// <summary>Matches the five uint32 fields of the x64 capture options ABI.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
struct CaptureNativeOptions {
    /// <summary>Complete byte size required by the native version validator.</summary>
    public uint Size;
    /// <summary>Supported capture ABI version.</summary>
    public uint Version;
    /// <summary>Combined pending and held frame limit.</summary>
    public uint BufferCapacity;
    /// <summary>Explicit zero or one cursor flag.</summary>
    public uint CaptureCursor;
    /// <summary>Explicit zero or one border-removal flag.</summary>
    public uint RemoveBorder;
    /// <summary>Converts previously validated managed options without changing their defaults.</summary>
    public static CaptureNativeOptions Create(WindowCaptureOptions options) => new() {
        Size = (uint)Marshal.SizeOf<CaptureNativeOptions>(), Version = 1, BufferCapacity = (uint)options.BufferCapacity,
        CaptureCursor = options.CaptureCursor ? 1u : 0u, RemoveBorder = options.RemoveBorder ? 1u : 0u
    };
}
