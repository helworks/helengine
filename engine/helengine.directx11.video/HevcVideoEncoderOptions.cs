namespace helengine.directx11.video;
/// <summary>Configures fixed-size BGRA input for GPU HEVC Main 8-bit video at 60/1 FPS.</summary>
public sealed class HevcVideoEncoderOptions {
    /// <summary>Original content width; odd widths receive one black padding pixel.</summary>
    public int SourceWidth { get; init; }
    /// <summary>Original content height; odd heights receive one black padding row.</summary>
    public int SourceHeight { get; init; }
    /// <summary>VBR target in bits per second, not a promise of the measured average.</summary>
    public long Bitrate { get; init; } = 40_000_000;
    /// <summary>VBR ceiling in bits per second, at least the target bitrate.</summary>
    public long MaxBitrate { get; init; } = 60_000_000;
    /// <summary>Rejects invalid input before allocating native resources.</summary>
    internal void Validate() {
        if (SourceWidth <= 0 || SourceWidth > 16384) { throw new ArgumentOutOfRangeException(nameof(SourceWidth)); }
        if (SourceHeight <= 0 || SourceHeight > 16384) { throw new ArgumentOutOfRangeException(nameof(SourceHeight)); }
        if (Bitrate <= 0) { throw new ArgumentOutOfRangeException(nameof(Bitrate)); }
        if (MaxBitrate < Bitrate) { throw new ArgumentOutOfRangeException(nameof(MaxBitrate)); }
    }
}
