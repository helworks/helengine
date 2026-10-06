using System.Drawing.Imaging;
using System.Text;
using System.Text.Json;

namespace helengine.vfx.captions;

/// <summary>Owns validated bitmap font frames and draws Unicode glyphs with preserved alpha.</summary>
public sealed class CaptionAtlas : IDisposable {
    /// <summary>Loaded frame bitmaps retained for the lifetime of one renderer.</summary>
    readonly List<Bitmap> Frames = new List<Bitmap>();
    /// <summary>Metrics validated against every loaded frame.</summary>
    readonly CaptionAtlasDocument Document;

    /// <summary>Loads an atlas and rejects missing glyph metrics or out-of-bounds rectangles.</summary>
    public CaptionAtlas(string path) {
        Document = JsonSerializer.Deserialize<CaptionAtlasDocument>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new FormatException("Bitmap font atlas is empty.");
        if (Document.Frames == null || Document.Frames.Count == 0 || Document.Glyphs == null || Document.Glyphs.Count == 0
            || !double.IsFinite(Document.LineHeight) || Document.LineHeight <= 0
            || !double.IsFinite(Document.SpaceAdvance) || Document.SpaceAdvance <= 0) {
            throw new FormatException("An atlas needs frames, glyphs, positive lineHeight and spaceAdvance.");
        }
        string directory = Path.GetDirectoryName(Path.GetFullPath(path));
        try {
            foreach (string frame in Document.Frames) {
                using var source = new Bitmap(Path.GetFullPath(frame, directory));
                var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(bitmap)) {
                    graphics.DrawImageUnscaled(source, 0, 0);
                }
                Frames.Add(bitmap);
            }
            ValidateGlyphs();
        } catch {
            Dispose();
            throw;
        }
    }

    /// <summary>Gets the count of interchangeable texture frames.</summary>
    public int FrameCount => Frames.Count;

    /// <summary>Measures a Unicode string using its authored pen advances.</summary>
    public double Measure(string text, double size) {
        double width = 0;
        foreach (Rune rune in text.EnumerateRunes()) {
            width += rune.Value == ' ' ? Document.SpaceAdvance : GetGlyph(rune.ToString()).Advance;
        }
        return width * size / Document.LineHeight;
    }

    /// <summary>Draws an atlas word, tinting its texture while retaining antialiased coverage.</summary>
    public void Draw(Graphics graphics, string text, double x, double y, double size, int frameIndex, Color tint) {
        double scale = size / Document.LineHeight;
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix(new float[][] {
            new float[] { tint.R / 255f, 0, 0, 0, 0 },
            new float[] { 0, tint.G / 255f, 0, 0, 0 },
            new float[] { 0, 0, tint.B / 255f, 0, 0 },
            new float[] { 0, 0, 0, tint.A / 255f, 0 },
            new float[] { 0, 0, 0, 0, 1 }
        }));
        Bitmap frame = Frames[frameIndex % Frames.Count];
        foreach (Rune rune in text.EnumerateRunes()) {
            if (rune.Value == ' ') {
                x += Document.SpaceAdvance * scale;
                continue;
            }
            CaptionAtlasGlyph glyph = GetGlyph(rune.ToString());
            if (glyph.Width > 0 && glyph.Height > 0) {
                Rectangle destination = Rectangle.Round(new RectangleF((float)(x + glyph.OffsetX * scale), (float)(y + glyph.OffsetY * scale), (float)(glyph.Width * scale), (float)(glyph.Height * scale)));
                graphics.DrawImage(frame, destination, glyph.X, glyph.Y, glyph.Width, glyph.Height, GraphicsUnit.Pixel, attributes);
            }
            x += glyph.Advance * scale;
        }
    }

    /// <summary>Releases every loaded texture frame.</summary>
    public void Dispose() {
        foreach (Bitmap frame in Frames) {
            frame.Dispose();
        }
        Frames.Clear();
    }

    /// <summary>Resolves an authored character and reports missing Unicode coverage explicitly.</summary>
    CaptionAtlasGlyph GetGlyph(string character) {
        if (!Document.Glyphs.TryGetValue(character, out CaptionAtlasGlyph glyph)) {
            throw new FormatException($"The font atlas has no glyph for '{character}'. Add it or change the font/uppercase setting.");
        }
        return glyph;
    }

    /// <summary>Validates metrics and each rectangle against all interchangeable frame textures.</summary>
    void ValidateGlyphs() {
        foreach (KeyValuePair<string, CaptionAtlasGlyph> entry in Document.Glyphs) {
            CaptionAtlasGlyph glyph = entry.Value;
            if (entry.Key.EnumerateRunes().Count() != 1 || glyph == null || glyph.X < 0 || glyph.Y < 0 || glyph.Width < 0 || glyph.Height < 0
                || !double.IsFinite(glyph.Advance) || glyph.Advance <= 0 || !double.IsFinite(glyph.OffsetX) || !double.IsFinite(glyph.OffsetY)
                || Frames.Any(frame => (long)glyph.X + glyph.Width > frame.Width || (long)glyph.Y + glyph.Height > frame.Height)) {
                throw new FormatException($"Invalid font atlas glyph '{entry.Key}'. Rectangles must fit every frame and advances must be positive.");
            }
        }
    }
}
