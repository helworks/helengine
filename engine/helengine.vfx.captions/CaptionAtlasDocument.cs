namespace helengine.vfx.captions;

/// <summary>Describes a bitmap font with one or more interchangeable texture frames.</summary>
public sealed class CaptionAtlasDocument {
    /// <summary>Gets or sets relative PNG texture paths in playback order.</summary>
    public List<string> Frames { get; set; } = new List<string>();
    /// <summary>Gets or sets native line height used to scale glyphs to the selected font size.</summary>
    public double LineHeight { get; set; }
    /// <summary>Gets or sets pen advance for a space when no space glyph is present.</summary>
    public double SpaceAdvance { get; set; }
    /// <summary>Gets or sets Unicode character keys and their shared rectangles/metrics.</summary>
    public Dictionary<string, CaptionAtlasGlyph> Glyphs { get; set; } = new Dictionary<string, CaptionAtlasGlyph>();
}
