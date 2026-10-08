using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Text.RegularExpressions;

namespace helengine.vfx.captions;

/// <summary>
/// The lettering source of one caption style: a texture atlas, a privately loaded font file or an installed family, with
/// the face actually available. Rendering and text measurement both go through it, so laid-out text matches drawn text.
/// </summary>
public sealed class CaptionFont : IDisposable {
    /// <summary>Optional privately loaded vector font collection.</summary>
    readonly PrivateFontCollection PrivateFonts;

    /// <summary>Loads the lettering source a validated style selects.</summary>
    /// <param name="style">Validated caption style; its font file path must already be resolved.</param>
    public CaptionFont(CaptionStyle style) {
        ArgumentNullException.ThrowIfNull(style);
        try {
            if (!string.IsNullOrWhiteSpace(style.AtlasFile)) {
                Atlas = new CaptionAtlas(style.AtlasFile);
            } else {
                if (!string.IsNullOrWhiteSpace(style.FontFile)) {
                    PrivateFonts = new PrivateFontCollection();
                    PrivateFonts.AddFontFile(style.FontFile);
                    if (PrivateFonts.Families.Length == 0) {
                        throw new FormatException("The selected font file contains no usable font family.");
                    }
                    Family = PrivateFonts.Families[0];
                } else {
                    Family = new FontFamily(style.FontFamily);
                }
                VectorStyle = style.Bold && Family.IsStyleAvailable(FontStyle.Bold) ? FontStyle.Bold : FontStyle.Regular;
                if (!Family.IsStyleAvailable(VectorStyle)) {
                    throw new FormatException("The selected font has no supported regular or bold face.");
                }
            }
        } catch {
            Dispose();
            throw;
        }
    }

    /// <summary>Gets the selected vector font family, or null for texture lettering.</summary>
    public FontFamily Family { get; }

    /// <summary>Gets the optional animated bitmap font.</summary>
    public CaptionAtlas Atlas { get; }

    /// <summary>Gets the actual supported vector font style.</summary>
    public FontStyle VectorStyle { get; }

    /// <summary>Measures the advance of one word or space with the font that will draw it.</summary>
    /// <param name="graphics">Graphics context the text is measured for.</param>
    /// <param name="text">Text to measure.</param>
    /// <param name="size">Glyph size in output pixels.</param>
    /// <returns>Advance width in pixels.</returns>
    public double Measure(Graphics graphics, string text, double size) {
        if (Atlas != null) {
            return Atlas.Measure(text, size);
        }
        using var font = new Font(Family, (float)size, VectorStyle, GraphicsUnit.Pixel);
        using StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
        return graphics.MeasureString(text, font, int.MaxValue, format).Width;
    }

    /// <summary>
    /// Measures one unwrapped line the way the caption renderer lays it out: word advances plus one measured space
    /// between consecutive words.
    /// </summary>
    /// <param name="text">Line text, already case-converted as it will be drawn.</param>
    /// <param name="size">Glyph size in output pixels.</param>
    /// <returns>Advance width of the line in pixels.</returns>
    public double MeasureLine(string text, double size) {
        if (!double.IsFinite(size) || size < 1 || size > 4096) {
            throw new ArgumentOutOfRangeException(nameof(size), "Glyph size must be between 1 and 4096 pixels.");
        }
        string[] words = Regex.Matches(text ?? "", @"[^\s]+").Select(match => match.Value).ToArray();
        using var bitmap = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        double width = words.Sum(word => Measure(graphics, word, size));
        return words.Length < 2 ? width : width + (words.Length - 1) * Measure(graphics, " ", size);
    }

    /// <summary>Releases vector and bitmap font resources.</summary>
    public void Dispose() {
        Atlas?.Dispose();
        Family?.Dispose();
        PrivateFonts?.Dispose();
    }
}
