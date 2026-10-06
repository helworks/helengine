namespace helengine.vfx.captions;

/// <summary>One Unicode glyph rectangle and baseline metrics shared by every atlas frame.</summary>
public sealed class CaptionAtlasGlyph {
    /// <summary>Gets or sets the source rectangle left edge in texture pixels.</summary>
    public int X { get; set; }
    /// <summary>Gets or sets the source rectangle top edge in texture pixels.</summary>
    public int Y { get; set; }
    /// <summary>Gets or sets the source rectangle width; zero is allowed for a space.</summary>
    public int Width { get; set; }
    /// <summary>Gets or sets the source rectangle height; zero is allowed for a space.</summary>
    public int Height { get; set; }
    /// <summary>Gets or sets the horizontal pen advance in texture pixels.</summary>
    public double Advance { get; set; }
    /// <summary>Gets or sets horizontal placement relative to the current pen.</summary>
    public double OffsetX { get; set; }
    /// <summary>Gets or sets vertical placement relative to the top of the text line.</summary>
    public double OffsetY { get; set; }
}
