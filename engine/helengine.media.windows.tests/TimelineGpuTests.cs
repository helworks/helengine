using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using helengine.media;
using helengine.vfx;
using helengine.video;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;

namespace helengine.media.windows.tests;

/// <summary>
/// Renders a compiled overlay timeline ("LEGALIZAR ≠ DESCRIMINALIZAR ≠ TRATAR") on the GPU in the <c>top</c> region of a
/// <c>stack</c> arrangement, mid-animation, and writes the frames to a visible build folder for inspection: the terms
/// appear on their cue moments inside the region, never over the picture, and the strike bar is drawn across the last term.
/// </summary>
public sealed class TimelineGpuTests {
    /// <summary>
    /// Visible build-owned assets root.
    /// </summary>
    const string Root = "C:/dev/helworks/builds/helengine/media-composition/fixtures";

    /// <summary>
    /// Visible folder the rendered frames are written to.
    /// </summary>
    const string Output = "C:/dev/helworks/builds/helengine/timeline-smoke";

    /// <summary>
    /// Fixture image the picture layer shows.
    /// </summary>
    const string PictureName = "timeline-blue.png";

    /// <summary>
    /// Output width of the test frames.
    /// </summary>
    const int Width = 540;

    /// <summary>
    /// Output height of the test frames.
    /// </summary>
    const int Height = 960;

    /// <summary>
    /// The timeline: three stacked terms that pop on cues a, b and c, ≠ separators fading in before the next term, a
    /// nested pop with a tilt on the last term and a red bar that strikes it.
    /// </summary>
    const string Definition = """
        {
          "id": "contrast_three_terms_video",
          "duration": 3.6,
          "slots": [
            { "name": "term_a", "kind": "text" },
            { "name": "sep_ab", "kind": "text" },
            { "name": "term_b", "kind": "text" },
            { "name": "sep_bc", "kind": "text" },
            { "name": "term_c", "kind": "text" },
            { "name": "strike", "kind": "rect" }
          ],
          "cues": [ { "name": "a", "time": 0.15 }, { "name": "b", "time": 1.2 }, { "name": "c", "time": 2.4 } ],
          "tracks": [
            { "kind": "activation", "slot": "term_a", "clips": [ { "start": { "cue": "a" }, "duration": 3.45 } ] },
            { "kind": "transform", "slot": "term_a", "clips": [ { "start": { "cue": "a" }, "duration": 3.45,
              "position": [ { "time": 0, "value": [0, -0.4, 0] } ],
              "scale": [ { "time": 0, "value": [0.6, 0.6, 1], "curve": "ease_out_back.v1" }, { "time": 0.3, "value": [1, 1, 1] } ] } ] },
            { "kind": "activation", "slot": "sep_ab", "clips": [ { "start": { "cue": "b", "offset": -0.2 }, "duration": 2.6 } ] },
            { "kind": "transform", "slot": "sep_ab", "clips": [ { "start": 0, "duration": 3.6, "position": [ { "time": 0, "value": [0, -0.2, 0] } ] } ] },
            { "kind": "value", "slot": "sep_ab", "channel": "opacity", "clips": [ { "start": { "cue": "b", "offset": -0.2 }, "duration": 0.2,
              "keyframes": [ { "time": 0, "value": 0, "curve": "smoothstep.v1" }, { "time": 0.2, "value": 1 } ] } ] },
            { "kind": "activation", "slot": "term_b", "clips": [ { "start": { "cue": "b" }, "duration": 2.4 } ] },
            { "kind": "transform", "slot": "term_b", "clips": [ { "start": { "cue": "b" }, "duration": 2.4,
              "scale": [ { "time": 0, "value": [0.6, 0.6, 1], "curve": "ease_out_back.v1" }, { "time": 0.3, "value": [1, 1, 1] } ] } ] },
            { "kind": "activation", "slot": "sep_bc", "clips": [ { "start": { "cue": "c", "offset": -0.2 }, "duration": 1.4 } ] },
            { "kind": "transform", "slot": "sep_bc", "clips": [ { "start": 0, "duration": 3.6, "position": [ { "time": 0, "value": [0, 0.2, 0] } ] } ] },
            { "kind": "value", "slot": "sep_bc", "channel": "opacity", "clips": [ { "start": { "cue": "c", "offset": -0.2 }, "duration": 0.2,
              "keyframes": [ { "time": 0, "value": 0, "curve": "smoothstep.v1" }, { "time": 0.2, "value": 1 } ] } ] },
            { "kind": "activation", "slot": "term_c", "clips": [ { "start": { "cue": "c" }, "duration": 1.2 } ] },
            { "kind": "transform", "slot": "term_c", "clips": [ { "start": 0, "duration": 3.6, "position": [ { "time": 0, "value": [0, 0.4, 0] } ] } ] },
            { "kind": "timeline", "clips": [ { "start": { "cue": "c" }, "duration": 0.45, "slots": { "target": "term_c" },
              "definition": {
                "id": "pop", "duration": 0.45,
                "slots": [ { "name": "target", "kind": "text" } ],
                "tracks": [ { "kind": "transform", "slot": "target", "mode": "offset", "clips": [ { "start": 0, "duration": 0.45,
                  "scale": [ { "time": 0, "value": [0.5, 0.5, 1], "curve": "ease_out_back.v1" }, { "time": 0.35, "value": [1, 1, 1] } ],
                  "rotation": [ { "time": 0, "value": [0, 0, -6], "curve": "ease_out_cubic.v1" }, { "time": 0.35, "value": [0, 0, 0] } ] } ] } ]
              } } ] },
            { "kind": "activation", "slot": "strike", "clips": [ { "start": 3, "duration": 0.6 } ] },
            { "kind": "transform", "slot": "strike", "clips": [ { "start": 0, "duration": 3.6, "position": [ { "time": 0, "value": [0, 0.4, 0] } ] } ] },
            { "kind": "value", "slot": "strike", "channel": "reveal", "clips": [ { "start": 3, "duration": 0.35,
              "keyframes": [ { "time": 0, "value": 0, "curve": "ease_out_cubic.v1" }, { "time": 0.35, "value": 1 } ] } ] }
          ]
        }
        """;

    /// <summary>
    /// Renders the timeline mid-animation and checks that the text stays in the top region, off the picture, and that
    /// the strike is drawn across the last term once revealed.
    /// </summary>
    [Fact]
    public void ContrastTimelineRendersInsideTheTopRegion() {
        Directory.CreateDirectory(Output);
        using (Bitmap bitmap = new Bitmap(32, 16)) {
            using (Graphics graphics = Graphics.FromImage(bitmap)) {
                graphics.Clear(Color.Blue);
            }
            bitmap.Save(Path.Combine(Root, PictureName), ImageFormat.Png);
        }
        VideoEdit edit = Edit();
        GraphicTemplateCatalog templates = GraphicTemplateCatalog.CreateBuiltIn();
        using WindowsVideoTextMeasurer measurer = new WindowsVideoTextMeasurer(Root);
        VideoCompileResult result = VideoEditCompiler.Compile(edit, new VideoCompileContext { Capabilities = WindowsMediaCapabilities.Describe(VfxEffectCatalog.CreateBuiltIn(), templates), GraphicTemplates = templates, TextMeasurer = measurer });
        Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics.Select(item => item.Code + " " + item.Path + " " + item.Message)));
        VideoArrangementRegion top = VideoArrangementPresets.Resolve("stack", edit).Find("top"), main = VideoArrangementPresets.Resolve("stack", edit).Find("main");
        int topEdge = (int)Math.Floor(top.Y * Height), topBottom = (int)Math.Ceiling((top.Y + top.Height) * Height);
        int mainTop = (int)Math.Ceiling(main.Y * Height), mainBottom = (int)Math.Floor((main.Y + main.Height) * Height);

        using Device device = new Device(DriverType.Warp, DeviceCreationFlags.BgraSupport);
        using WindowsMediaSourceResolver resolver = new WindowsMediaSourceResolver(Root, device);
        using DirectX11MediaCompositor compositor = new DirectX11MediaCompositor(device, resolver);
        foreach (double seconds in new[] { 0.62, 1.55, 2.95, 3.25, 3.55, 3.9 }) {
            using MediaVideoFrame frame = compositor.Render(result.Composition, MediaTime.FromSeconds(seconds), new RenderSize(Width, Height));
            byte[] pixels = frame.Surface.ReadRgba().ToArray();
            Save(pixels, Path.Combine(Output, $"contrast-{(int)Math.Round(seconds * 1000):D4}ms.png"));
            Assert.Contains(Rows(pixels, topEdge, topBottom), pixel => pixel[0] > 200 && pixel[1] > 200 && pixel[2] > 200);
            Assert.DoesNotContain(Rows(pixels, mainTop + 1, mainBottom - 1), pixel => pixel[0] > 200 && pixel[1] > 200);
            Assert.DoesNotContain(Rows(pixels, 0, topEdge - 2), pixel => pixel[0] > 200 && pixel[1] > 200);
            if (seconds >= 3.55) {
                Assert.Contains(Rows(pixels, topEdge, topBottom), pixel => pixel[0] > 200 && pixel[1] < 80 && pixel[2] < 80);
            } else {
                Assert.DoesNotContain(Rows(pixels, topEdge, topBottom), pixel => pixel[0] > 200 && pixel[1] < 80 && pixel[2] < 80);
            }
        }
    }

    /// <summary>
    /// Builds the edit: a four-second scene with a blue picture in <c>main</c> and the timeline in <c>top</c>, its cues
    /// at fixed seconds (a take would anchor them on spoken words).
    /// </summary>
    /// <returns>Edit.</returns>
    static VideoEdit Edit() {
        using JsonDocument definition = JsonDocument.Parse(Definition);
        return new VideoEdit {
            Id = "gpu-timeline",
            Format = new VideoFormat { Width = Width, Height = Height, BackgroundColor = "#000000FF" },
            TextStyles = new(StringComparer.Ordinal) {
                ["graphic"] = JsonSerializer.SerializeToElement(new { FontFamily = "Arial", FontSize = 48, CenterY = 0.4, TextColor = "#FFFFFF", HighlightColor = "#FFD400", OutlineWidth = 0, ShadowOffset = 0 })
            },
            Media = [new VideoMedia { Id = "picture", Kind = "image", Path = PictureName, Sha256 = Hash(PictureName), Width = 32, Height = 16 }],
            Scenes = [
                new VideoScene {
                    Id = "s", Duration = new VideoSceneDuration { Mode = "fixed", Sec = 4.2 },
                    Arrangement = new VideoArrangement { Preset = "stack" },
                    Layers = [new VideoLayer { Id = "picture", Kind = "media", Media = "picture", Fit = "cover", Region = "main" }],
                    Overlays = [
                        new VideoOverlay {
                            Id = "g", Region = "top", Text = "",
                            Timeline = new VideoOverlayTimeline {
                                Definition = definition.RootElement.Clone(),
                                Bindings = new(StringComparer.Ordinal) {
                                    ["term_a"] = new VideoTimelineBinding { Text = "LEGALIZAR" },
                                    ["sep_ab"] = new VideoTimelineBinding { Text = "≠", Size = 0.12, Color = JsonSerializer.SerializeToElement("#FFD400") },
                                    ["term_b"] = new VideoTimelineBinding { Text = "DESCRIMINALIZAR" },
                                    ["sep_bc"] = new VideoTimelineBinding { Text = "≠", Size = 0.12, Color = JsonSerializer.SerializeToElement("#FFD400") },
                                    ["term_c"] = new VideoTimelineBinding { Text = "TRATAR" },
                                    ["strike"] = new VideoTimelineBinding { Rect = new VideoTimelineRect { Color = JsonSerializer.SerializeToElement("#FF2020"), Height = 0.035, Match = "term_c" } }
                                },
                                Cues = new(StringComparer.Ordinal) {
                                    ["a"] = new VideoMoment { Sec = 0.5 },
                                    ["b"] = new VideoMoment { Sec = 1.4 },
                                    ["c"] = new VideoMoment { Sec = 2.8 }
                                }
                            }
                        }
                    ]
                }
            ]
        };
    }

    /// <summary>
    /// Writes a packed RGBA frame as a PNG.
    /// </summary>
    /// <param name="pixels">Packed RGBA frame.</param>
    /// <param name="path">Output file.</param>
    static void Save(byte[] pixels, string path) {
        using Bitmap bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        BitmapData data = bitmap.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try {
            byte[] row = new byte[Width * 4];
            for (int y = 0; y < Height; y++) {
                for (int x = 0; x < Width; x++) {
                    int index = (y * Width + x) * 4;
                    row[x * 4] = pixels[index + 2];
                    row[x * 4 + 1] = pixels[index + 1];
                    row[x * 4 + 2] = pixels[index];
                    row[x * 4 + 3] = pixels[index + 3];
                }
                Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * data.Stride), row.Length);
            }
        } finally {
            bitmap.UnlockBits(data);
        }
        bitmap.Save(path, ImageFormat.Png);
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
                rows.Add(pixels.AsSpan((y * Width + x) * 4, 4).ToArray());
            }
        }
        return rows;
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
