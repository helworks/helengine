using System.Text.Json;
using System.Text.Json.Serialization;
using helengine.vfx.captions;
using helengine.video;

namespace helengine.media.windows;

/// <summary>
/// Measures text for graphic layout with the composition text renderer itself: the style snapshot is read the way
/// <see cref="CaptionLayerSource"/> reads it, the font file resolves against the same assets root, and widths come from
/// the renderer's own <see cref="CaptionFont"/>, so laid-out items match the drawn glyphs. Fonts are cached per style.
/// </summary>
public sealed class WindowsVideoTextMeasurer : IVideoTextMeasurer, IDisposable {
    /// <summary>
    /// Assets root style font files resolve against; null when styles may only use installed fonts.
    /// </summary>
    readonly string AssetsRoot;

    /// <summary>
    /// Validated styles by style snapshot text, read for their case conversion.
    /// </summary>
    readonly Dictionary<string, CaptionStyle> Styles = new Dictionary<string, CaptionStyle>(StringComparer.Ordinal);

    /// <summary>
    /// Loaded fonts by style snapshot text.
    /// </summary>
    readonly Dictionary<string, CaptionFont> Fonts = new Dictionary<string, CaptionFont>(StringComparer.Ordinal);

    /// <summary>
    /// Style reader options matching the text layer source.
    /// </summary>
    readonly JsonSerializerOptions Options;

    /// <summary>
    /// Creates a measurer.
    /// </summary>
    /// <param name="assetsRoot">Assets root that style font files are relative to, or null for installed fonts only.</param>
    public WindowsVideoTextMeasurer(string assetsRoot) {
        AssetsRoot = assetsRoot;
        Options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        Options.Converters.Add(new JsonStringEnumConverter());
    }

    /// <summary>
    /// Measures one line with the renderer's font and spacing.
    /// </summary>
    /// <param name="style">Text style snapshot.</param>
    /// <param name="text">Text as authored.</param>
    /// <param name="fontSize">Font size in output pixels.</param>
    /// <returns>Advance width and the renderer's line box height.</returns>
    public VideoTextExtent Measure(JsonElement style, string text, double fontSize) {
        string key = style.GetRawText();
        if (!Fonts.TryGetValue(key, out CaptionFont font)) {
            CaptionStyle snapshot = JsonSerializer.Deserialize<CaptionStyle>(key, Options) ?? throw new InvalidDataException("Text style is missing.");
            if (!string.IsNullOrWhiteSpace(snapshot.FontFile)) {
                snapshot.FontFile = CaptionFontPath.Resolve(AssetsRoot, snapshot.FontFile);
            }
            snapshot.Validate();
            font = new CaptionFont(snapshot);
            Styles.Add(key, snapshot);
            Fonts.Add(key, font);
        }
        string shown = Styles[key].Uppercase ? (text ?? "").ToUpperInvariant() : text ?? "";
        return new VideoTextExtent(font.MeasureLine(shown, fontSize), fontSize * VideoTextStyles.LineHeight);
    }

    /// <summary>
    /// Releases every cached font.
    /// </summary>
    public void Dispose() {
        foreach (CaptionFont font in Fonts.Values) {
            font.Dispose();
        }
        Fonts.Clear();
        Styles.Clear();
    }
}
