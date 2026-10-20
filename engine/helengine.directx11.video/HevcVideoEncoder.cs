namespace helengine.directx11.video;
/// <summary>Encodes same-device BGRA textures as 60 FPS HEVC MP4 without CPU pixel transfers.</summary>
/// <remarks>Serialize all calls. Texture pointers are borrowed only for Submit; the device is retained independently. Complete finalizes the work file; Dispose alone aborts. This class never publishes a final destination.</remarks>
public sealed class HevcVideoEncoder : IDisposable {
    /// <summary>Owns the native codec, muxer and bounded GPU resources.</summary>
    readonly HevcEncoderHandle Handle;
    /// <summary>Distinguishes a completed work file from an aborted one.</summary>
    bool Completed;
    /// <summary>Adopts a validated native encoder.</summary>
    HevcVideoEncoder(HevcEncoderHandle handle, HevcVideoEncoderOptions options) { Handle = handle; OutputWidth = (options.SourceWidth + 1) & ~1; OutputHeight = (options.SourceHeight + 1) & ~1; }
    /// <summary>Even encoded width, with at most one black padding column.</summary>
    public int OutputWidth { get; }
    /// <summary>Even encoded height, with at most one black padding row.</summary>
    public int OutputHeight { get; }
    /// <summary>Number of frames accepted with consecutive indices starting at zero.</summary>
    public long SubmittedFrames { get; private set; }
    /// <summary>Opens the required NVENC codec and an MP4 work file using a valid host device.</summary>
    public static HevcVideoEncoder Open(IntPtr deviceHandle, string workPath, HevcVideoEncoderOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        if (deviceHandle == IntPtr.Zero) { throw new ArgumentException("A valid D3D11 device is required.", nameof(deviceHandle)); }
        options.Validate();
        if (string.IsNullOrWhiteSpace(workPath)) { throw new ArgumentException("A work file path is required.", nameof(workPath)); }
        workPath = Path.GetFullPath(workPath);
        var native = HevcEncoderNativeOptions.Create(options); var error = HevcEncoderNativeError.Create();
        if (HevcEncoderNativeApi.Create(deviceHandle, workPath, ref native, out var pointer, ref error) != 0) { error.Throw(); }
        if (pointer == IntPtr.Zero) { throw new InvalidDataException("Native encoder returned an empty owner."); }
        var handle = new HevcEncoderHandle(pointer);
        try { return new(handle, options); } catch { handle.Dispose(); throw; }
    }
    /// <summary>Submits a same-device BGRA texture; its producer may dispose the lease on return.</summary>
    public void Submit(IntPtr textureHandle, long frameIndex) {
        ObjectDisposedException.ThrowIf(Handle.IsClosed, this);
        if (Completed) { throw new InvalidOperationException("Encoder has completed."); }
        if (textureHandle == IntPtr.Zero) { throw new ArgumentException("A valid BGRA texture is required.", nameof(textureHandle)); }
        if (frameIndex != SubmittedFrames) { throw new ArgumentOutOfRangeException(nameof(frameIndex), "Frame indices must be consecutive, starting at zero."); }
        var error = HevcEncoderNativeError.Create();
        if (HevcEncoderNativeApi.Submit(Handle, textureHandle, frameIndex, ref error) != 0) { error.Throw(); }
        SubmittedFrames++;
    }
    /// <summary>Idempotently drains the encoder and finalizes the MP4 work file; throws on an empty session.</summary>
    public void Complete() {
        ObjectDisposedException.ThrowIf(Handle.IsClosed, this); if (Completed) { return; }
        var error = HevcEncoderNativeError.Create();
        if (HevcEncoderNativeApi.Complete(Handle, ref error) != 0) { error.Throw(); }
        Completed = true;
    }
    /// <summary>Releases resources exactly once; unfinished output remains an unpublished work file.</summary>
    public void Dispose() => Handle.Dispose();
}
