namespace helengine.recording.windows;
/// <summary>Chooses one explicit window and output path for fixed 60 FPS HEVC recording.</summary>
public sealed class WindowRecordingOptions {
    /// <summary>Existing top-level HWND selected by the host, never inferred by title.</summary>
    public IntPtr WindowHandle { get; init; }
    /// <summary>Final MP4 destination which must not already exist.</summary>
    public string OutputPath { get; init; }
    /// <summary>Positive requested duration, or null to record until stop or window closure.</summary>
    public TimeSpan? Duration { get; init; }
    /// <summary>VBR target in bits per second.</summary>
    public long Bitrate { get; init; } = 40_000_000;
    /// <summary>VBR ceiling in bits per second.</summary>
    public long MaxBitrate { get; init; } = 60_000_000;
    /// <summary>Rejects malformed arguments before creating any GPU or capture resources.</summary>
    internal void Validate() {
        if (WindowHandle == IntPtr.Zero) { throw new ArgumentException("An explicit nonzero HWND is required.", nameof(WindowHandle)); }
        if (string.IsNullOrWhiteSpace(OutputPath)) { throw new ArgumentException("An MP4 destination is required.", nameof(OutputPath)); }
        _ = Path.GetFullPath(OutputPath);
        if (!string.Equals(Path.GetExtension(OutputPath), ".mp4", StringComparison.OrdinalIgnoreCase)) { throw new ArgumentException("The destination must use the .mp4 extension.", nameof(OutputPath)); }
        if (Duration.HasValue && Duration <= TimeSpan.Zero) { throw new ArgumentOutOfRangeException(nameof(Duration)); }
        if (Bitrate <= 0 || MaxBitrate < Bitrate) { throw new ArgumentOutOfRangeException(nameof(Bitrate), "Positive bitrates with max >= target are required."); }
    }
}
