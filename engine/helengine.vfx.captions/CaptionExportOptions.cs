namespace helengine.vfx.captions;

/// <summary>Defines a full-canvas RGBA image sequence aligned to time zero of the source video.</summary>
public sealed class CaptionExportOptions {
    /// <summary>Gets or sets output frame width.</summary>
    public int Width { get; set; } = 1080;
    /// <summary>Gets or sets output frame height.</summary>
    public int Height { get; set; } = 1920;
    /// <summary>Gets or sets sequence frame rate, including fractional rates such as 29.97.</summary>
    public double Fps { get; set; } = 30;
    /// <summary>Gets or sets export duration; zero uses the final transcription timestamp.</summary>
    public double Duration { get; set; }

    /// <summary>Validates export allocation and computes frame count with exclusive end timing.</summary>
    public int GetFrameCount(CaptionDocument document) {
        ArgumentNullException.ThrowIfNull(document);
        CaptionRenderer.ValidateDimensions(Width, Height);
        if (!double.IsFinite(Fps) || Fps <= 0 || Fps > 120 || !double.IsFinite(Duration) || Duration < 0) {
            throw new ArgumentException("Sequence FPS must be 0..120 and duration must be finite and nonnegative.");
        }
        double count = Math.Ceiling((Duration == 0 ? document.Duration : Duration) * Fps);
        if (count < 1 || count > 2000000) {
            throw new ArgumentException("The requested sequence must contain 1..2,000,000 frames.");
        }
        return (int)count;
    }
}
