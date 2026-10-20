namespace helengine.recording.windows;
/// <summary>Live texture encoding boundary, owning all input data needed after Submit.</summary>
interface IRecordingEncoder : IDisposable {
    /// <summary>Accepts one borrowed same-device texture at its consecutive output index.</summary>
    void Submit(IntPtr texture, long index);
    /// <summary>Drains and finalizes the work file, without publishing it.</summary>
    void Complete();
}
