using helengine.directx11.video;
namespace helengine.recording.windows;
/// <summary>Connects the recording worker to the reusable texture encoder without scene dependencies.</summary>
sealed class RecordingEncoder : IRecordingEncoder {
    /// <summary>Owns the managed encoder facade.</summary>
    readonly HevcVideoEncoder Encoder;
    /// <summary>Creates the required hardware encoder before the recording origin is measured.</summary>
    internal RecordingEncoder(IntPtr device, string path, HevcVideoEncoderOptions options) { Encoder = HevcVideoEncoder.Open(device, path, options); }
    /// <summary>Copies borrowed input to encoder-owned GPU surfaces before returning.</summary>
    public void Submit(IntPtr texture, long index) => Encoder.Submit(texture, index);
    /// <summary>Drains and finalizes the MP4 work file.</summary>
    public void Complete() => Encoder.Complete();
    /// <summary>Releases or aborts without implicit publication.</summary>
    public void Dispose() => Encoder.Dispose();
}
