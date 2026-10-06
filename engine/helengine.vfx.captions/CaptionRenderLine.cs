namespace helengine.vfx.captions;

/// <summary>A measured line used to center both vector and bitmap text consistently.</summary>
sealed class CaptionRenderLine {
    /// <summary>Gets the tokens assigned to this line.</summary>
    public List<CaptionRenderWord> Words { get; } = new List<CaptionRenderWord>();
    /// <summary>Gets or sets measured width, including interword advances.</summary>
    public double Width { get; set; }
}
