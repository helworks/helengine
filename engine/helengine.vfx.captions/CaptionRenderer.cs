using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Text.RegularExpressions;

namespace helengine.vfx.captions;

/// <summary>Renders deterministic transparent frames for both preview and offline export on Windows.</summary>
public sealed class CaptionRenderer : IDisposable {
    /// <summary>Immutable transcription shared by every frame.</summary>
    readonly CaptionDocument Document;
    /// <summary>Snapshot of style settings retained throughout this render session.</summary>
    readonly CaptionStyle Style;
    /// <summary>Optional privately loaded vector font collection.</summary>
    readonly PrivateFontCollection PrivateFonts;
    /// <summary>Selected vector font family, unused for texture lettering.</summary>
    readonly FontFamily Family;
    /// <summary>Optional animated bitmap font.</summary>
    readonly CaptionAtlas Atlas;
    /// <summary>Actual supported vector font style.</summary>
    readonly FontStyle VectorStyle;

    /// <summary>Validates and loads font assets once, keeping preview and exports identical.</summary>
    public CaptionRenderer(CaptionDocument document, CaptionStyle style) {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Style = (style ?? throw new ArgumentNullException(nameof(style))).Copy();
        Style.Validate();
        try {
            if (!string.IsNullOrWhiteSpace(Style.AtlasFile)) {
                Atlas = new CaptionAtlas(Style.AtlasFile);
            } else {
                if (!string.IsNullOrWhiteSpace(Style.FontFile)) {
                    PrivateFonts = new PrivateFontCollection();
                    PrivateFonts.AddFontFile(Style.FontFile);
                    if (PrivateFonts.Families.Length == 0) {
                        throw new FormatException("The selected font file contains no usable font family.");
                    }
                    Family = PrivateFonts.Families[0];
                } else {
                    Family = new FontFamily(Style.FontFamily);
                }
                VectorStyle = Style.Bold && Family.IsStyleAvailable(FontStyle.Bold) ? FontStyle.Bold : FontStyle.Regular;
                if (!Family.IsStyleAvailable(VectorStyle)) {
                    throw new FormatException("The selected font has no supported regular or bold face.");
                }
            }
        } catch {
            Dispose();
            throw;
        }
    }

    /// <summary>Creates an RGBA frame at an absolute transcription time; silence stays transparent.</summary>
    public Bitmap Render(double time, int width, int height) {
        ValidateDimensions(width, height);
        CaptionCue cue = Document.FindCue(time);
        var frame = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        try {
            using Graphics graphics = Graphics.FromImage(frame);
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            if (cue != null) {
                DrawCue(graphics, cue, time, width, height);
            }
            return frame;
        } catch {
            frame.Dispose();
            throw;
        }
    }

    /// <summary>Limits frame allocation and rejects unusable export sizes.</summary>
    public static void ValidateDimensions(int width, int height) {
        if (width < 16 || height < 16 || width > 8192 || height > 8192 || (long)width * height > 33554432) {
            throw new ArgumentOutOfRangeException(nameof(width), "Caption dimensions must be 16..8192 with at most 32 megapixels.");
        }
    }

    /// <summary>Releases vector and bitmap font resources retained by this renderer.</summary>
    public void Dispose() {
        Atlas?.Dispose();
        Family?.Dispose();
        PrivateFonts?.Dispose();
    }

    /// <summary>Selects aligned word groups while keeping unaligned phrase text and line breaks intact.</summary>
    CaptionRenderWord[] SelectWords(CaptionCue cue, double time) {
        if (cue.Words.Count == 0) {
            string text = Style.Uppercase ? cue.Text.ToUpperInvariant() : cue.Text;
            return Regex.Matches(text, @"\n|[^\s]+").Select(match => new CaptionRenderWord(match.Value)).ToArray();
        }
        int groupStart = 0;
        for (int index = Style.WordsPerGroup; index < cue.Words.Count; index += Style.WordsPerGroup) {
            if (time >= cue.Words[index].Start) {
                groupStart = index;
            }
        }
        return cue.Words.Skip(groupStart).Take(Style.WordsPerGroup)
            .Select(word => new CaptionRenderWord(Style.Uppercase ? word.Text.ToUpperInvariant() : word.Text, word.Start, word.End)).ToArray();
    }

    /// <summary>Measures word advance using the same font source that will draw it.</summary>
    double Measure(Graphics graphics, string text, double size) {
        if (Atlas != null) {
            return Atlas.Measure(text, size);
        }
        using var font = new Font(Family, (float)size, VectorStyle, GraphicsUnit.Pixel);
        using StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
        return graphics.MeasureString(text, font, int.MaxValue, format).Width;
    }

    /// <summary>Wraps text at word boundaries and preserves explicit SubRip line breaks.</summary>
    List<CaptionRenderLine> Layout(Graphics graphics, CaptionRenderWord[] words, double size, double maxWidth) {
        double space = Measure(graphics, " ", size);
        var lines = new List<CaptionRenderLine>();
        var line = new CaptionRenderLine();
        foreach (CaptionRenderWord word in words) {
            if (word.Text == "\n") {
                if (line.Words.Count > 0) {
                    lines.Add(line);
                    line = new CaptionRenderLine();
                }
                continue;
            }
            double advance = Measure(graphics, word.Text, size);
            if (line.Words.Count > 0 && (line.Words.Count >= Style.WordsPerLine || line.Width + space + advance > maxWidth)) {
                lines.Add(line);
                line = new CaptionRenderLine();
            }
            line.Width += (line.Words.Count == 0 ? 0 : space) + advance;
            line.Words.Add(word);
        }
        if (line.Words.Count > 0) {
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>Fits and centers caption lines inside the canvas before applying bounded motion.</summary>
    void DrawCue(Graphics graphics, CaptionCue cue, double time, int width, int height) {
        CaptionRenderWord[] words = SelectWords(cue, time);
        double margin = Math.Max(8, Style.OutlineWidth + Style.ShadowOffset + 8);
        if (width <= 2 * margin + 2 || height <= 2 * margin + 2) {
            throw new ArgumentException("The canvas is too small for this caption outline and shadow. Increase its dimensions or reduce those effects.");
        }
        double centerX = Math.Clamp(width * Style.CenterX, margin + 1, width - margin - 1);
        double centerY = Math.Clamp(height * Style.CenterY, margin + 1, height - margin - 1);
        double maxWidth = Math.Min(width * Style.MaxWidth, 2 * Math.Min(centerX - margin, width - margin - centerX));
        double maxHeight = 2 * Math.Min(centerY - margin, height - margin - centerY);
        double size = Style.FontSize;
        List<CaptionRenderLine> lines = Layout(graphics, words, size, maxWidth);
        for (int attempt = 0; attempt < 20; attempt++) {
            double actualWidth = lines.Count == 0 ? 0 : lines.Max(line => line.Width);
            double actualHeight = lines.Count * size * 1.35;
            if (actualWidth <= maxWidth && actualHeight <= maxHeight) {
                break;
            }
            size *= Math.Min(0.9, Math.Min(maxWidth / Math.Max(1, actualWidth), maxHeight / Math.Max(1, actualHeight)));
            lines = Layout(graphics, words, size, maxWidth);
        }
        if (size < 1 || lines.Count == 0) {
            throw new ArgumentException("Caption text cannot fit at the selected position and canvas size.");
        }
        CaptionRenderWord active = words.FirstOrDefault(word => word.Start <= time && time < word.End);
        double animationStart = active == null ? Math.Max(cue.Start, words[0].Start) : active.Start;
        double elapsed = Math.Max(0, time - animationStart);
        double transition = Math.Clamp(elapsed / Style.AnimationDuration, 0, 1);
        int textureFrame = (int)Math.Floor(Math.Max(0, time - cue.Start) * Style.TextureFps);
        GraphicsState state = graphics.Save();
        graphics.TranslateTransform((float)centerX, (float)centerY);
        if (Style.Animation == CaptionAnimation.Pop) {
            double scale = 0.78 + 0.22 * (1 - Math.Pow(1 - transition, 3));
            graphics.ScaleTransform((float)scale, (float)scale);
        } else if (Style.Animation == CaptionAnimation.Bounce) {
            graphics.TranslateTransform(0, (float)(-Math.Sin(transition * Math.PI) * size * 0.12));
        } else if (Style.Animation == CaptionAnimation.TwoFrame) {
            graphics.TranslateTransform(textureFrame % 2 == 0 ? -2 : 2, textureFrame % 2 == 0 ? 1 : -1);
        }
        double blockWidth = lines.Max(line => line.Width);
        double blockHeight = lines.Count * size * 1.35;
        using (var panel = new SolidBrush(CaptionColor.Parse(Style.PanelColor))) {
            if (panel.Color.A > 0) {
                graphics.FillRectangle(panel, (float)(-blockWidth / 2 - 8), (float)(-blockHeight / 2 - 4), (float)(blockWidth + 16), (float)(blockHeight + 8));
            }
        }
        double y = -blockHeight / 2;
        double space = Measure(graphics, " ", size);
        foreach (CaptionRenderLine line in lines) {
            double x = -line.Width / 2;
            foreach (CaptionRenderWord word in line.Words) {
                Color color = CaptionColor.Parse(Style.HighlightWords && word == active ? Style.HighlightColor : Style.TextColor);
                DrawWord(graphics, word.Text, x, y, size, textureFrame, color);
                x += Measure(graphics, word.Text, size) + space;
            }
            y += size * 1.35;
        }
        graphics.Restore(state);
    }

    /// <summary>Draws a vector outline or the corresponding bitmap coverage, shadow and tint.</summary>
    void DrawWord(Graphics graphics, string text, double x, double y, double size, int textureFrame, Color color) {
        Color shadow = CaptionColor.Parse(Style.ShadowColor);
        Color outline = CaptionColor.Parse(Style.OutlineColor);
        if (Atlas != null) {
            if (shadow.A > 0 && Style.ShadowOffset > 0) {
                Atlas.Draw(graphics, text, x + Style.ShadowOffset, y + Style.ShadowOffset, size, textureFrame, shadow);
            }
            if (Style.OutlineWidth > 0) {
                for (int index = 0; index < 8; index++) {
                    double angle = index * Math.PI / 4;
                    Atlas.Draw(graphics, text, x + Math.Cos(angle) * Style.OutlineWidth / 2, y + Math.Sin(angle) * Style.OutlineWidth / 2, size, textureFrame, outline);
                }
            }
            Atlas.Draw(graphics, text, x, y, size, textureFrame, color);
            return;
        }
        using var path = new GraphicsPath();
        path.AddString(text, Family, (int)VectorStyle, (float)size, new PointF((float)x, (float)y), StringFormat.GenericTypographic);
        if (shadow.A > 0 && Style.ShadowOffset > 0) {
            using var shadowPath = (GraphicsPath)path.Clone();
            using var translation = new Matrix();
            translation.Translate((float)Style.ShadowOffset, (float)Style.ShadowOffset);
            shadowPath.Transform(translation);
            using var shadowBrush = new SolidBrush(shadow);
            graphics.FillPath(shadowBrush, shadowPath);
        }
        if (Style.OutlineWidth > 0) {
            using var pen = new Pen(outline, (float)Style.OutlineWidth) { LineJoin = LineJoin.Round };
            graphics.DrawPath(pen, path);
        }
        using var brush = new SolidBrush(color);
        graphics.FillPath(brush, path);
    }
}
