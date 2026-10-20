namespace helengine.capture.windows.Interop;
/// <summary>Operation-owned UTF-16 error storage, with no thread-global diagnostic dependency.</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 8)]
struct CaptureNativeError {
    /// <summary>Byte size of the complete native error structure.</summary>
    public uint Size;
    /// <summary>Capture ABI version understood by this facade.</summary>
    public uint Version;
    /// <summary>Original signed Windows HRESULT.</summary>
    public int HResult;
    /// <summary>Null-terminated native operation name.</summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string Operation;
    /// <summary>Null-terminated native error description.</summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] public string Message;
    /// <summary>Allocates a valid versioned error buffer for one P/Invoke operation.</summary>
    public static CaptureNativeError Create() => new() { Size = (uint)Marshal.SizeOf<CaptureNativeError>(), Version = 1, Operation = string.Empty, Message = string.Empty };
    /// <summary>Preserves the native diagnostic instead of returning an empty frame on failure.</summary>
    public void Throw() => throw new WindowCaptureException(Operation, Message, HResult);
}
