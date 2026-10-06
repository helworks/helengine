using System.Text.Json;

namespace helengine.vfx.captions;

/// <summary>A portable caption preset; sizes are pixels and placement is normalized.</summary>
public sealed class CaptionStyle {
    /// <summary>Gets or sets the human-readable preset name.</summary>
    public string Name { get; set; } = "Bold highlight";
    /// <summary>Gets or sets the installed font family when no font file or atlas is selected.</summary>
    public string FontFamily { get; set; } = "Arial";
    /// <summary>Gets or sets an optional TTF/OTF file loaded privately without installing it.</summary>
    public string FontFile { get; set; }
    /// <summary>Gets or sets an optional texture atlas JSON, taking precedence over vector fonts.</summary>
    public string AtlasFile { get; set; }
    /// <summary>Gets or sets the requested glyph size in output pixels.</summary>
    public double FontSize { get; set; } = 76;
    /// <summary>Gets or sets whether vector lettering is bold.</summary>
    public bool Bold { get; set; } = true;
    /// <summary>Gets or sets whether displayed text is converted to uppercase.</summary>
    public bool Uppercase { get; set; } = true;
    /// <summary>Gets or sets the normal text color as #RRGGBB or #AARRGGBB.</summary>
    public string TextColor { get; set; } = "#FFFFFF";
    /// <summary>Gets or sets the currently spoken word color.</summary>
    public string HighlightColor { get; set; } = "#FFE600";
    /// <summary>Gets or sets the outline color.</summary>
    public string OutlineColor { get; set; } = "#111111";
    /// <summary>Gets or sets outline thickness in output pixels.</summary>
    public double OutlineWidth { get; set; } = 7;
    /// <summary>Gets or sets a translucent text panel; alpha zero disables it.</summary>
    public string PanelColor { get; set; } = "#00000000";
    /// <summary>Gets or sets the shadow color, including alpha.</summary>
    public string ShadowColor { get; set; } = "#99000000";
    /// <summary>Gets or sets the diagonal shadow offset in output pixels.</summary>
    public double ShadowOffset { get; set; } = 5;
    /// <summary>Gets or sets the horizontal center from zero to one.</summary>
    public double CenterX { get; set; } = 0.5;
    /// <summary>Gets or sets the vertical center from zero to one.</summary>
    public double CenterY { get; set; } = 0.72;
    /// <summary>Gets or sets the fraction of the canvas available for text wrapping.</summary>
    public double MaxWidth { get; set; } = 0.84;
    /// <summary>Gets or sets the maximum number of words per wrapped line.</summary>
    public int WordsPerLine { get; set; } = 3;
    /// <summary>Gets or sets aligned words per displayed group; SRT text stays intact.</summary>
    public int WordsPerGroup { get; set; } = 6;
    /// <summary>Gets or sets whether genuine word alignment drives color emphasis.</summary>
    public bool HighlightWords { get; set; } = true;
    /// <summary>Gets or sets caption motion independent of texture frame cycling.</summary>
    public CaptionAnimation Animation { get; set; } = CaptionAnimation.Pop;
    /// <summary>Gets or sets the duration of a pop or bounce transition, in seconds.</summary>
    public double AnimationDuration { get; set; } = 0.12;
    /// <summary>Gets or sets the texture/wobble cycle rate in frames per second.</summary>
    public double TextureFps { get; set; } = 8;

    /// <summary>Copies mutable settings so preview/export can own stable snapshots.</summary>
    public CaptionStyle Copy() => JsonSerializer.Deserialize<CaptionStyle>(JsonSerializer.Serialize(this));

    /// <summary>Rejects invalid layout, motion or color settings before rendering.</summary>
    public void Validate() {
        if (string.IsNullOrWhiteSpace(FontFamily) || !double.IsFinite(FontSize) || FontSize < 1 || FontSize > 4096
            || !double.IsFinite(CenterX) || CenterX <= 0 || CenterX >= 1 || !double.IsFinite(CenterY) || CenterY <= 0 || CenterY >= 1
            || !double.IsFinite(MaxWidth) || MaxWidth <= 0 || MaxWidth > 1 || WordsPerLine < 1 || WordsPerGroup < 1
            || !double.IsFinite(OutlineWidth) || OutlineWidth < 0 || OutlineWidth > 100
            || !double.IsFinite(ShadowOffset) || ShadowOffset < 0 || ShadowOffset > 100
            || !double.IsFinite(AnimationDuration) || AnimationDuration <= 0 || AnimationDuration > 10
            || !double.IsFinite(TextureFps) || TextureFps <= 0 || TextureFps > 120 || !Enum.IsDefined(Animation)) {
            throw new ArgumentException("Caption style contains invalid font, placement, wrapping or animation settings.");
        }
        CaptionColor.Parse(TextColor);
        CaptionColor.Parse(HighlightColor);
        CaptionColor.Parse(OutlineColor);
        CaptionColor.Parse(PanelColor);
        CaptionColor.Parse(ShadowColor);
    }
}
