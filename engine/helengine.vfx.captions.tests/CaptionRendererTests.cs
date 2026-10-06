using System.Drawing.Imaging;
using System.Text.Json;

namespace helengine.vfx.captions.tests;

/// <summary>Verifies alpha output, real word emphasis, stable animation and texture-font coverage.</summary>
public sealed class CaptionRendererTests {
    /// <summary>Silent intervals are transparent and caption coverage includes partially transparent antialiasing.</summary>
    [Fact]
    public void FramesPreserveAlphaAndSilence() {
        var document = new CaptionDocument(new[] { new CaptionCue("Olá mundo", 1, 2) });
        using var renderer = new CaptionRenderer(document, SmallStyle());
        using Bitmap silent = renderer.Render(0.5, 320, 180);
        using Bitmap visible = renderer.Render(1.5, 320, 180);
        Assert.False(HasVisiblePixels(silent));
        Assert.True(HasVisiblePixels(visible));
        Assert.Equal(0, visible.GetPixel(0, 0).A);
        Assert.True(HasPartialAlpha(visible));
        using Bitmap ended = renderer.Render(2, 320, 180);
        Assert.False(HasVisiblePixels(ended));
    }

    /// <summary>Whisper emphasis moves between actual word intervals; untimed text never invents a highlight.</summary>
    [Fact]
    public void HighlightFollowsWordsAndRespectsGaps() {
        CaptionStyle style = SmallStyle();
        style.TextColor = "#FFFFFF";
        style.HighlightColor = "#FF0000";
        style.OutlineWidth = 0;
        style.ShadowOffset = 0;
        var cue = new CaptionCue("one two", 0, 2, new[] { new CaptionWord("one", 0, 0.7), new CaptionWord("two", 1, 2) });
        using var renderer = new CaptionRenderer(new CaptionDocument(new[] { cue }), style);
        using Bitmap first = renderer.Render(0.5, 320, 180);
        using Bitmap gap = renderer.Render(0.8, 320, 180);
        using Bitmap second = renderer.Render(1.5, 320, 180);
        Assert.True(CountRedPixels(first, 0, 160) > 0);
        Assert.Equal(0, CountRedPixels(first, 160, 320));
        Assert.Equal(0, CountRedPixels(gap, 0, 320));
        Assert.True(CountRedPixels(second, 160, 320) > 0);
        using var plain = new CaptionRenderer(new CaptionDocument(new[] { new CaptionCue("one two", 0, 2) }), style);
        using Bitmap untimed = plain.Render(0.5, 320, 180);
        Assert.Equal(0, CountRedPixels(untimed, 0, 320));
    }

    /// <summary>Repeated rendering of an animation time produces identical pixels and the second frame differs.</summary>
    [Fact]
    public void TwoFrameAnimationIsDeterministic() {
        CaptionStyle style = SmallStyle();
        style.Animation = CaptionAnimation.TwoFrame;
        style.TextureFps = 2;
        using var renderer = new CaptionRenderer(new CaptionDocument(new[] { new CaptionCue("two frames", 0, 2) }), style);
        using Bitmap first = renderer.Render(0.1, 320, 180);
        using Bitmap repeated = renderer.Render(0.1, 320, 180);
        using Bitmap alternate = renderer.Render(0.6, 320, 180);
        Assert.Equal(PngBytes(first), PngBytes(repeated));
        Assert.NotEqual(PngBytes(first), PngBytes(alternate));
    }

    /// <summary>Unicode atlas glyphs cycle between authored texture frames without using vector fonts.</summary>
    [Fact]
    public void TextureFontCyclesFramesAndRejectsMissingCoverage() {
        using var workspace = new CaptionTestWorkspace();
        CreateTexture(workspace.DirectoryPath, "a.png", Color.Red);
        CreateTexture(workspace.DirectoryPath, "b.png", Color.Blue);
        string atlasPath = Path.Combine(workspace.DirectoryPath, "font.json");
        var atlas = new CaptionAtlasDocument {
            Frames = new List<string> { "a.png", "b.png" }, LineHeight = 8, SpaceAdvance = 3,
            Glyphs = new Dictionary<string, CaptionAtlasGlyph> { ["é"] = new CaptionAtlasGlyph { Width = 8, Height = 8, Advance = 9 } }
        };
        File.WriteAllText(atlasPath, JsonSerializer.Serialize(atlas));
        CaptionStyle style = SmallStyle();
        style.AtlasFile = atlasPath;
        style.Uppercase = false;
        style.TextureFps = 2;
        style.OutlineWidth = 0;
        style.ShadowOffset = 0;
        using var renderer = new CaptionRenderer(new CaptionDocument(new[] { new CaptionCue("é", 0, 2) }), style);
        using Bitmap first = renderer.Render(0.1, 320, 180);
        using Bitmap second = renderer.Render(0.6, 320, 180);
        Assert.True(CountRedPixels(first, 0, 320) > 0);
        Assert.Equal(0, CountRedPixels(second, 0, 320));
        Assert.True(HasVisiblePixels(second));
        using var missing = new CaptionRenderer(new CaptionDocument(new[] { new CaptionCue("x", 0, 2) }), style);
        Assert.Throws<FormatException>(() => missing.Render(0.1, 320, 180));
    }

    /// <summary>Vector fonts can be loaded privately from an existing TTF file.</summary>
    [Fact]
    public void CustomFontFileRendersAccentedText() {
        CaptionStyle style = SmallStyle();
        style.FontFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
        Assert.True(File.Exists(style.FontFile));
        using var renderer = new CaptionRenderer(new CaptionDocument(new[] { new CaptionCue("ação coração", 0, 1) }), style);
        using Bitmap frame = renderer.Render(0.5, 320, 180);
        Assert.True(HasVisiblePixels(frame));
    }

    /// <summary>Creates an unanimated small style for inspecting specific renderer behavior.</summary>
    public static CaptionStyle SmallStyle() => new CaptionStyle { FontSize = 24, OutlineWidth = 2, ShadowOffset = 1, CenterY = 0.5, Uppercase = false, Animation = CaptionAnimation.None };

    /// <summary>Checks whether any frame sample has nonzero alpha.</summary>
    public static bool HasVisiblePixels(Bitmap image) {
        for (int y = 0; y < image.Height; y++) {
            for (int x = 0; x < image.Width; x++) {
                if (image.GetPixel(x, y).A > 0) {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>Checks antialiasing survives as partial alpha instead of an opaque background.</summary>
    static bool HasPartialAlpha(Bitmap image) {
        for (int y = 0; y < image.Height; y++) {
            for (int x = 0; x < image.Width; x++) {
                int alpha = image.GetPixel(x, y).A;
                if (alpha > 0 && alpha < 255) {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>Counts red caption coverage in a horizontal region.</summary>
    static int CountRedPixels(Bitmap image, int startX, int endX) {
        int count = 0;
        for (int y = 0; y < image.Height; y++) {
            for (int x = startX; x < endX; x++) {
                Color color = image.GetPixel(x, y);
                if (color.A > 100 && color.R > 180 && color.G < 80 && color.B < 80) {
                    count++;
                }
            }
        }
        return count;
    }

    /// <summary>Encodes a frame for deterministic pixel-output comparisons.</summary>
    static byte[] PngBytes(Bitmap image) {
        using var stream = new MemoryStream();
        image.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    /// <summary>Creates an authored atlas frame inside the test workspace.</summary>
    static void CreateTexture(string directory, string name, Color color) {
        using var texture = new Bitmap(8, 8);
        using Graphics graphics = Graphics.FromImage(texture);
        graphics.Clear(color);
        texture.Save(Path.Combine(directory, name), ImageFormat.Png);
    }
}
