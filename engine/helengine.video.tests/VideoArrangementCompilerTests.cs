using System.Text.Json;
using helengine.media;

namespace helengine.video.tests {
    /// <summary>
    /// Verifies how the compiler honours scene arrangements: picture layers take their region as viewport (layout presets
    /// applied inside it, explicit viewports winning, clipped to it), graphics and plain overlays are laid out inside their
    /// region, and nothing reaches into the caption band.
    /// </summary>
    public class VideoArrangementCompilerTests {
        /// <summary>
        /// A picture layer in the stack's main region gets the region rectangle as its viewport and is clipped to it.
        /// </summary>
        [Fact]
        public void Compile_LayerInRegion_TakesRegionAsViewport() {
            VideoEdit edit = Stack();

            VideoCompileResult result = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.False(result.HasErrors, Describe(result));
            VisualLayer picture = result.Composition.Layers.Single(layer => layer.Id == "contrast-picture");
            VideoArrangementRegion main = VideoArrangementPresets.Resolve("stack", edit).Find("main");
            AssertViewport(main.X, main.Y, main.Width, main.Height, picture.Viewport);
            Assert.True(picture.ClipToViewport);
            Assert.Empty(CompositionValidator.Validate(result.Composition, GraphicEditSamples.Catalog()));
        }

        /// <summary>
        /// A layout preset applies inside the region as if the region were the frame, and an explicit viewport wins.
        /// </summary>
        [Fact]
        public void Compile_LayerLayoutInRegion_PresetInsideRegionAndViewportWins() {
            VideoEdit edit = Stack();
            edit.Scenes[0].Layers[0].Layout = new VideoLayout { Preset = "inset" };
            VideoArrangementRegion main = VideoArrangementPresets.Resolve("stack", edit).Find("main");

            VisualLayer inset = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer())).Composition.Layers.Single(layer => layer.Id == "contrast-picture");
            edit.Scenes[0].Layers[0].Layout = new VideoLayout { Viewport = new VideoViewport { X = 0.1, Y = 0.1, Width = 0.5, Height = 0.5 } };
            VisualLayer explicitViewport = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer())).Composition.Layers.Single(layer => layer.Id == "contrast-picture");

            AssertViewport(main.X + 0.06 * main.Width, main.Y + 0.2 * main.Height, 0.88 * main.Width, 0.48 * main.Height, inset.Viewport);
            AssertViewport(0.1, 0.1, 0.5, 0.5, explicitViewport.Viewport);
        }

        /// <summary>
        /// A graphic in the top region is laid out inside it, above the picture region and clear of the captions, instead of
        /// at the style's preferred center.
        /// </summary>
        [Fact]
        public void Compile_GraphicInRegion_LaysOutInsideRegion() {
            VideoEdit edit = Stack();
            VideoResolvedArrangement stack = VideoArrangementPresets.Resolve("stack", edit);
            VideoGraphicRectangle top = stack.Find("top").Rectangle(1080, 1920);

            VideoCompileResult result = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.False(result.HasErrors, Describe(result));
            List<VisualLayer> elements = Elements(result);
            Assert.Equal(7, elements.Count);
            foreach (VisualLayer element in elements) {
                double x = (0.5 + element.Transform.PositionX) * 1080, y = (0.5 + element.Transform.PositionY) * 1920;
                double half = element.Text.Style.GetProperty("FontSize").GetDouble() * VideoTextStyles.LineHeight / 2;
                Assert.InRange(x, top.Left, top.Right);
                Assert.InRange(y - half, top.Top - 1, top.Bottom + 1);
                Assert.InRange(y + half, top.Top - 1, top.Bottom + 1);
            }
            Assert.Equal((top.Top + top.Bottom) / 2, (0.5 + Element(result, "item-1").Transform.PositionY) * 1920, 0);
        }

        /// <summary>
        /// A graphic alone in a graphic_only region grows past the style font size to fill it, up to the region limit.
        /// </summary>
        [Fact]
        public void Compile_GraphicOnly_GrowsToFillRegion() {
            VideoEdit edit = GraphicEditSamples.ContrastChain();
            VideoScene scene = edit.Scenes[0];
            scene.Arrangement = new VideoArrangement { Preset = "graphic_only" };
            scene.Overlays[0].Region = "main";
            scene.Overlays[0].Graphic.Items = ["Lei", "Fato", "Voz"];

            VideoCompileResult result = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.False(result.HasErrors, Describe(result));
            double size = Element(result, "item-0").Text.Style.GetProperty("FontSize").GetDouble();
            Assert.True(size > 96 * 1.5, $"font size {size}");
            Assert.True(size <= 96 * VideoGraphicCompiler.RegionMaximumScale + 1e-6);
        }

        /// <summary>
        /// A plain overlay in a region is centered in it, wraps at its width and shrinks to fit its height.
        /// </summary>
        [Fact]
        public void Compile_PlainOverlayInRegion_IsFittedAndCentered() {
            VideoEdit edit = Stack();
            edit.Scenes[0].Overlays[0] = new VideoOverlay { Id = "label", Text = "UMA FRASE LONGA DEMAIS PARA CABER NUMA LINHA SÓ DENTRO DA FAIXA", At = new VideoMoment { Sec = 0.2 }, Region = "top" };
            VideoGraphicRectangle top = VideoArrangementPresets.Resolve("stack", edit).Find("top").Rectangle(1080, 1920);

            VideoCompileResult result = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.False(result.HasErrors, Describe(result));
            JsonElement style = result.Composition.Layers.Single(layer => layer.Id == "contrast-overlay-label").Text.Style;
            Assert.Equal(0.5, style.GetProperty("CenterX").GetDouble(), 6);
            Assert.Equal((top.Top + top.Bottom) / 2 / 1920, style.GetProperty("CenterY").GetDouble(), 4);
            Assert.Equal(top.Width / 1080, style.GetProperty("MaxWidth").GetDouble(), 6);
            Assert.True(style.GetProperty("FontSize").GetDouble() <= 96);
        }

        /// <summary>
        /// A scene without an arrangement compiles exactly as before: the graphic keeps its free-area placement and the
        /// layer its frame viewport.
        /// </summary>
        [Fact]
        public void Compile_WithoutRegions_IsUnchanged() {
            VideoEdit plain = GraphicEditSamples.ContrastChain();
            VideoEdit full = GraphicEditSamples.ContrastChain();
            full.Scenes[0].Arrangement = new VideoArrangement { Preset = "full" };

            VideoCompileResult before = VideoEditCompiler.Compile(plain, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));
            VideoCompileResult after = VideoEditCompiler.Compile(full, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.Equal(CompositionJson.Serialize(before.Composition), CompositionJson.Serialize(after.Composition));
        }

        /// <summary>
        /// Builds the portrait contrast-chain take scene arranged as a stack: a picture layer in main, the graphic in top.
        /// </summary>
        /// <returns>Valid edit.</returns>
        static VideoEdit Stack() {
            VideoEdit edit = GraphicEditSamples.ContrastChain();
            edit.Media.Add(new VideoMedia { Id = "pic", Kind = "image", Path = "pic.png", Sha256 = new string('b', 64), Width = 1024, Height = 768 });
            VideoScene scene = edit.Scenes[0];
            scene.Arrangement = new VideoArrangement { Preset = "stack" };
            scene.Layers = [new VideoLayer { Id = "picture", Kind = "media", Media = "pic", Fit = "cover", Region = "main" }];
            scene.Overlays[0].Region = "top";
            return edit;
        }

        /// <summary>
        /// Lists the element layers of the contrast chain graphic.
        /// </summary>
        /// <param name="result">Compile result.</param>
        /// <returns>Element layers.</returns>
        static List<VisualLayer> Elements(VideoCompileResult result) {
            return result.Composition.Layers.Where(layer => layer.Id.StartsWith("contrast-overlay-chain-", StringComparison.Ordinal)).ToList();
        }

        /// <summary>
        /// Finds one element layer of the contrast chain graphic.
        /// </summary>
        /// <param name="result">Compile result.</param>
        /// <param name="name">Element name and index, such as <c>item-0</c>.</param>
        /// <returns>Layer.</returns>
        static VisualLayer Element(VideoCompileResult result, string name) {
            return result.Composition.Layers.Single(layer => layer.Id == "contrast-overlay-chain-" + name);
        }

        /// <summary>
        /// Asserts a composition viewport.
        /// </summary>
        /// <param name="x">Expected left edge.</param>
        /// <param name="y">Expected top edge.</param>
        /// <param name="width">Expected width.</param>
        /// <param name="height">Expected height.</param>
        /// <param name="actual">Actual viewport.</param>
        static void AssertViewport(double x, double y, double width, double height, LayerViewport actual) {
            Assert.Equal(x, actual.X, 9);
            Assert.Equal(y, actual.Y, 9);
            Assert.Equal(width, actual.Width, 9);
            Assert.Equal(height, actual.Height, 9);
        }

        /// <summary>
        /// Formats diagnostics for assertion messages.
        /// </summary>
        /// <param name="result">Compile result.</param>
        /// <returns>One line per diagnostic.</returns>
        static string Describe(VideoCompileResult result) {
            return string.Join("; ", result.Diagnostics.Select(item => item.Severity + " " + item.Code + " " + item.Path + " " + item.Message));
        }
    }
}
