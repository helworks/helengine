using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;
using helengine.media;
using helengine.vfx;
using helengine.video;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;

namespace helengine.media.windows.tests;

/// <summary>
/// Renders a compiled <c>stack</c> arrangement on the GPU: a blue picture covering the <c>main</c> region stays inside it,
/// and a <c>contrast_chain</c> graphic in the <c>top</c> region is drawn above the picture without touching it.
/// </summary>
public sealed class ArrangementGpuTests {
    /// <summary>
    /// Visible build-owned assets root.
    /// </summary>
    const string Root = "C:/dev/helworks/builds/helengine/media-composition/fixtures";

    /// <summary>
    /// Fixture image the picture layer shows: a wide solid blue image that overflows the region when covering it.
    /// </summary>
    const string PictureName = "arrangement-blue.png";

    /// <summary>
    /// Output width of the test frame.
    /// </summary>
    const int Width = 270;

    /// <summary>
    /// Output height of the test frame.
    /// </summary>
    const int Height = 480;

    /// <summary>
    /// With every item shown, the picture fills its region (and only its region) and the graphic text sits in the band
    /// above it.
    /// </summary>
    [Fact]
    public void StackDrawsPictureInMainAndGraphicInTop() {
        using (Bitmap bitmap = new Bitmap(32, 16)) {
            using (Graphics graphics = Graphics.FromImage(bitmap)) {
                graphics.Clear(Color.Blue);
            }
            bitmap.Save(Path.Combine(Root, PictureName), ImageFormat.Png);
        }
        VideoEdit edit = new VideoEdit {
            Id = "gpu-arrangement",
            Format = new VideoFormat { Width = Width, Height = Height, BackgroundColor = "#000000FF" },
            TextStyles = new(StringComparer.Ordinal) {
                ["graphic"] = JsonSerializer.SerializeToElement(new { FontFamily = "Arial", FontSize = 30, CenterY = 0.4, TextColor = "#FFFFFF", HighlightColor = "#FFD400", OutlineWidth = 0, ShadowOffset = 0 })
            },
            Media = [new VideoMedia { Id = "picture", Kind = "image", Path = PictureName, Sha256 = Hash(PictureName), Width = 32, Height = 16 }],
            Scenes = [
                new VideoScene {
                    Id = "s", Duration = new VideoSceneDuration { Mode = "fixed", Sec = 4 },
                    Arrangement = new VideoArrangement { Preset = "stack" },
                    Layers = [new VideoLayer { Id = "picture", Kind = "media", Media = "picture", Fit = "cover", Region = "main" }],
                    Overlays = [
                        new VideoOverlay {
                            Id = "g", Text = "LEGALIZAR ≠ DESCRIMINALIZAR ≠ TRATAR", Region = "top",
                            Graphic = new VideoGraphic {
                                Template = "contrast_chain", Items = ["Legalizar", "Descriminalizar", "Tratar"],
                                At = [new VideoMoment { Sec = 0.3 }, new VideoMoment { Sec = 0.8 }, new VideoMoment { Sec = 1.3 }]
                            }
                        }
                    ]
                }
            ]
        };
        GraphicTemplateCatalog templates = GraphicTemplateCatalog.CreateBuiltIn();
        using WindowsVideoTextMeasurer measurer = new WindowsVideoTextMeasurer(Root);
        VideoCompileResult result = VideoEditCompiler.Compile(edit, new VideoCompileContext { Capabilities = WindowsMediaCapabilities.Describe(VfxEffectCatalog.CreateBuiltIn(), templates), GraphicTemplates = templates, TextMeasurer = measurer });
        Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics.Select(item => item.Code + " " + item.Path + " " + item.Message)));
        VideoResolvedArrangement stack = VideoArrangementPresets.Resolve("stack", edit);
        VideoArrangementRegion top = stack.Find("top"), main = stack.Find("main");

        using Device device = new Device(DriverType.Warp, DeviceCreationFlags.BgraSupport);
        using WindowsMediaSourceResolver resolver = new WindowsMediaSourceResolver(Root, device);
        using DirectX11MediaCompositor compositor = new DirectX11MediaCompositor(device, resolver);
        using MediaVideoFrame frame = compositor.Render(result.Composition, MediaTime.FromSeconds(3), new RenderSize(Width, Height));
        byte[] pixels = frame.Surface.ReadRgba().ToArray();

        int mainTop = (int)Math.Ceiling(main.Y * Height), mainBottom = (int)Math.Floor((main.Y + main.Height) * Height);
        int mainLeft = (int)Math.Ceiling(main.X * Width), mainRight = (int)Math.Floor((main.X + main.Width) * Width);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, Pixel(pixels, Width / 2, (mainTop + mainBottom) / 2));
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, Pixel(pixels, mainLeft + 2, mainTop + 2));
        Assert.True(Pixel(pixels, mainLeft - 3, (mainTop + mainBottom) / 2)[2] < 24, "the covering picture spilled left of its region");
        Assert.True(Pixel(pixels, mainRight + 3, (mainTop + mainBottom) / 2)[2] < 24, "the covering picture spilled right of its region");
        Assert.True(Pixel(pixels, Width / 2, mainTop - 3)[2] < 24, "the covering picture spilled above its region");

        int topEdge = (int)Math.Floor(top.Y * Height), topBottom = (int)Math.Ceiling((top.Y + top.Height) * Height);
        foreach (string name in new[] { "item-0", "item-1", "item-2" }) {
            int row = Row(result, name);
            Assert.InRange(row, topEdge, topBottom);
        }
        Assert.Contains(Rows(pixels, topEdge, topBottom), pixel => pixel[0] > 200 && pixel[1] > 200 && pixel[2] > 200);
        Assert.DoesNotContain(Rows(pixels, mainTop + 1, mainBottom - 1), pixel => pixel[0] > 200 && pixel[1] > 200);
    }

    /// <summary>
    /// Finds the output row an element layer is centered on.
    /// </summary>
    /// <param name="result">Compile result.</param>
    /// <param name="name">Element name and index.</param>
    /// <returns>Pixel row.</returns>
    static int Row(VideoCompileResult result, string name) {
        VisualLayer layer = result.Composition.Layers.Single(item => item.Id == "s-overlay-g-" + name);
        return (int)Math.Round((0.5 + layer.Transform.PositionY) * Height);
    }

    /// <summary>
    /// Collects the RGBA pixels of a range of rows.
    /// </summary>
    /// <param name="pixels">Packed RGBA frame.</param>
    /// <param name="first">First row included.</param>
    /// <param name="last">Last row included.</param>
    /// <returns>Pixels of the rows.</returns>
    static List<byte[]> Rows(byte[] pixels, int first, int last) {
        List<byte[]> rows = new List<byte[]>();
        for (int y = Math.Max(0, first); y <= Math.Min(Height - 1, last); y++) {
            for (int x = 0; x < Width; x++) {
                rows.Add(Pixel(pixels, x, y));
            }
        }
        return rows;
    }

    /// <summary>
    /// Reads one packed RGBA pixel.
    /// </summary>
    /// <param name="pixels">Packed RGBA frame.</param>
    /// <param name="x">Column.</param>
    /// <param name="y">Row.</param>
    /// <returns>RGBA bytes.</returns>
    static byte[] Pixel(byte[] pixels, int x, int y) {
        return pixels.AsSpan((y * Width + x) * 4, 4).ToArray();
    }

    /// <summary>
    /// Hashes one fixture.
    /// </summary>
    /// <param name="name">Fixture file name under the root.</param>
    /// <returns>Lowercase SHA-256.</returns>
    static string Hash(string name) {
        return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(Root, name)))).ToLowerInvariant();
    }
}
