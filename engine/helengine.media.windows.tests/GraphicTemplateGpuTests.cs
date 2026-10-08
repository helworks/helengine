using System.Text.Json;
using helengine.media;
using helengine.vfx;
using helengine.video;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;

namespace helengine.media.windows.tests;

/// <summary>
/// Renders a compiled <c>contrast_chain</c> graphic on the GPU mid-build: the first term and its accent separator are
/// drawn where the measured layout put them, while the next term, not spoken yet, leaves its row empty.
/// </summary>
public sealed class GraphicTemplateGpuTests {
    /// <summary>
    /// Visible build-owned assets root; the edit uses installed fonts and no media.
    /// </summary>
    const string Root = "C:/dev/helworks/builds/helengine/media-composition/fixtures";

    /// <summary>
    /// Output width of the test frame.
    /// </summary>
    const int Width = 270;

    /// <summary>
    /// Output height of the test frame.
    /// </summary>
    const int Height = 480;

    /// <summary>
    /// Between the separator pop and the second term, only the first term and the separator are visible.
    /// </summary>
    [Fact]
    public void ContrastChainMidBuildShowsFirstTermAndSeparatorOnly() {
        VideoEdit edit = new VideoEdit {
            Id = "gpu-graphic",
            Format = new VideoFormat { Width = Width, Height = Height, BackgroundColor = "#000000FF" },
            TextStyles = new(StringComparer.Ordinal) {
                ["graphic"] = JsonSerializer.SerializeToElement(new { FontFamily = "Arial", FontSize = 30, CenterY = 0.4, TextColor = "#FFFFFF", HighlightColor = "#FFD400", OutlineWidth = 0, ShadowOffset = 0 })
            },
            Scenes = [
                new VideoScene {
                    Id = "s", Duration = new VideoSceneDuration { Mode = "fixed", Sec = 4 },
                    Overlays = [
                        new VideoOverlay {
                            Id = "g", Text = "LEGALIZAR ≠ DESCRIMINALIZAR ≠ TRATAR",
                            Graphic = new VideoGraphic {
                                Template = "contrast_chain", Items = ["Legalizar", "Descriminalizar", "Tratar"],
                                At = [new VideoMoment { Sec = 0.3 }, new VideoMoment { Sec = 1.5 }, new VideoMoment { Sec = 2.5 }]
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

        using Device device = new Device(DriverType.Warp, DeviceCreationFlags.BgraSupport);
        using WindowsMediaSourceResolver resolver = new WindowsMediaSourceResolver(Root, device);
        using DirectX11MediaCompositor compositor = new DirectX11MediaCompositor(device, resolver);
        using MediaVideoFrame frame = compositor.Render(result.Composition, MediaTime.FromSeconds(1.2), new RenderSize(Width, Height));
        byte[] pixels = frame.Surface.ReadRgba().ToArray();

        int first = Row(result, "item-0"), separator = Row(result, "separator-0"), second = Row(result, "item-1");
        Assert.True(first < separator && separator < second);
        Assert.Contains(Band(pixels, first, 2), pixel => pixel[0] > 200 && pixel[1] > 200 && pixel[2] > 200);
        Assert.Contains(Band(pixels, separator, 6), pixel => pixel[0] > 180 && pixel[1] > 150 && pixel[2] < 90);
        Assert.All(Band(pixels, second, 4), pixel => Assert.True(pixel[0] < 24 && pixel[1] < 24 && pixel[2] < 24));
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
    /// Collects the RGBA pixels of a band of rows around one row.
    /// </summary>
    /// <param name="pixels">Packed RGBA frame.</param>
    /// <param name="row">Center row.</param>
    /// <param name="reach">Rows included above and below the center row.</param>
    /// <returns>Pixels of the band.</returns>
    static List<byte[]> Band(byte[] pixels, int row, int reach) {
        List<byte[]> band = new List<byte[]>();
        for (int y = row - reach; y <= row + reach; y++) {
            for (int x = 0; x < Width; x++) {
                band.Add(pixels.AsSpan((y * Width + x) * 4, 4).ToArray());
            }
        }
        return band;
    }
}
