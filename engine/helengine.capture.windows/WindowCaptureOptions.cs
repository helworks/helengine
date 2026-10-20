namespace helengine.capture.windows;
/// <summary>Configures one bounded window capture without requiring an engine or render loop.</summary>
public sealed class WindowCaptureOptions {
    /// <summary>Maximum combined number of pending and consumer-held frame textures; must be positive.</summary>
    public int BufferCapacity { get; init; } = 3;
    /// <summary>Whether Windows includes the pointer in captured frames; disabled by default.</summary>
    public bool CaptureCursor { get; init; }
    /// <summary>Requests borderless capture with Windows consent; the default keeps the system indicator.</summary>
    public bool RemoveBorder { get; init; }
    /// <summary>Rejects unusable capacity before allocating a device or calling native code.</summary>
    internal void Validate() { ArgumentOutOfRangeException.ThrowIfNegativeOrZero(BufferCapacity); }
}
