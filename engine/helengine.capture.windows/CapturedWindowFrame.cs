namespace helengine.capture.windows;
/// <summary>Owns one independent BGRA GPU texture and its exact source timestamp until disposal.</summary>
public sealed class CapturedWindowFrame : IDisposable {
    /// <summary>Independent native lease retains device and texture after session teardown.</summary>
    readonly CaptureFrameHandle Handle;
    /// <summary>Native borrowed pointers remain valid only while this lease is alive.</summary>
    readonly CaptureNativeFrameInfo Info;
    /// <summary>Adopts a valid native frame; acquisition releases the handle if validation fails.</summary>
    internal CapturedWindowFrame(CaptureFrameHandle handle, CaptureNativeFrameInfo info) {
        if (info.Texture == IntPtr.Zero || info.Device == IntPtr.Zero || info.Width == 0 || info.Height == 0 || info.PixelFormat != 1) {
            throw new InvalidDataException("Native capture returned invalid frame metadata.");
        }
        Width = checked((int)info.Width); Height = checked((int)info.Height); Handle = handle; Info = info;
    }
    /// <summary>Original valid content width in pixels.</summary>
    public int Width { get; }
    /// <summary>Original valid content height in pixels.</summary>
    public int Height { get; }
    /// <summary>Four-channel BGRA8 SDR storage.</summary>
    public WindowCapturePixelFormat Format => (WindowCapturePixelFormat)Info.PixelFormat;
    /// <summary>Original QPC-based SystemRelativeTime, expressed as TimeSpan ticks.</summary>
    public TimeSpan Timestamp => TimeSpan.FromTicks(Info.TimestampTicks);
    /// <summary>Source observation sequence; gaps can represent explicitly counted discards.</summary>
    public ulong SequenceNumber => Info.SequenceNumber;
    /// <summary>Borrowed ID3D11Texture2D pointer; finish GPU submissions before disposing this frame.</summary>
    public IntPtr TextureHandle { get { ThrowIfDisposed(); return Info.Texture; } }
    /// <summary>Borrowed ID3D11Device pointer corresponding to this texture.</summary>
    public IntPtr DeviceHandle { get { ThrowIfDisposed(); return Info.Device; } }
    /// <summary>Explicitly downloads packed BGRA pixels; the returned array remains valid after frame disposal.</summary>
    public byte[] CopyPixels() {
        ThrowIfDisposed(); var bytes = new byte[CapturePixelBufferUtils.GetByteCount(Width, Height)]; var error = CaptureNativeError.Create();
        if (CaptureNativeApi.CopyPixels(Handle, bytes, (ulong)bytes.Length, ref error) != 0) { error.Throw(); }
        return bytes;
    }
    /// <summary>Returns native capacity and texture resources exactly once.</summary>
    public void Dispose() => Handle.Dispose();
    /// <summary>Rejects pointer access and readback after native ownership has ended.</summary>
    void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Handle.IsClosed, this);
}
