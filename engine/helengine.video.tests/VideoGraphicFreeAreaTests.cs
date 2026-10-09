using System.Text.Json;
using helengine.media;

namespace helengine.video.tests {
    /// <summary>
    /// Verifies that graphics stay off the scene's pictures: the free boxes of the safe area, the presented picture bounds,
    /// a contrast chain moving above an inset picture and full-frame scenes keeping the style's area.
    /// </summary>
    public class VideoGraphicFreeAreaTests {
        /// <summary>
        /// Output frame width of the sample edit.
        /// </summary>
        const double Width = 1080;

        /// <summary>
        /// Output frame height of the sample edit.
        /// </summary>
        const double Height = 1920;

        /// <summary>
        /// An inset picture splits a tall safe area into the band above it and the band below it; the strips beside it are
        /// too narrow to count.
        /// </summary>
        [Fact]
        public void Find_InsetPicture_LeavesBandsAboveAndBelow() {
            VideoGraphicSafeArea area = new VideoGraphicSafeArea(Width, Height, 75.6, 928.8, 134.4, 1500, 768);
            VideoGraphicRectangle inset = new VideoGraphicRectangle(0.06 * Width, 0.2 * Height, 0.94 * Width, 0.68 * Height);

            List<VideoGraphicSafeArea> free = VideoGraphicFreeArea.Find(area, [inset]);

            double margin = VideoGraphicFreeArea.PictureMargin * Height;
            Assert.Equal(2, free.Count);
            VideoGraphicSafeArea above = free.Single(box => box.Top == area.Top);
            VideoGraphicSafeArea below = free.Single(box => box.Bottom == area.Bottom);
            Assert.Equal(inset.Top - margin, above.Bottom, 6);
            Assert.Equal(inset.Bottom + margin, below.Top, 6);
            Assert.All(free, box => Assert.Equal(area.Width, box.Width, 6));
        }

        /// <summary>
        /// A full-frame picture leaves no free box at all.
        /// </summary>
        [Fact]
        public void Find_FullFramePicture_LeavesNothing() {
            VideoGraphicSafeArea area = new VideoGraphicSafeArea(Width, Height, 75.6, 928.8, 134.4, 1500, 768);

            Assert.Empty(VideoGraphicFreeArea.Find(area, [new VideoGraphicRectangle(0, 0, Width, Height)]));
        }

        /// <summary>
        /// With the picture inset in the middle of the frame, the contrast chain is laid out in the band above it: every
        /// element sits between the top margin and the picture's top edge, and the short band turns it into one centered row.
        /// </summary>
        [Fact]
        public void Compile_InsetScene_PlacesGraphicAboveThePicture() {
            VideoEdit edit = GraphicEditSamples.ContrastChain();
            edit.Scenes[0].Layers = [new VideoLayer { Id = "take", Kind = "take", Fit = "contain", Layout = new VideoLayout { Preset = "inset" } }];

            VideoCompileResult result = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.False(result.HasErrors, Describe(result));
            double size = Style(Layer(result, "item-1")).GetProperty("FontSize").GetDouble();
            Assert.True(size >= 96 * VideoGraphicFreeArea.MinimumScale, $"font size {size}");
            foreach (string name in new[] { "item-0", "separator-0", "item-1", "separator-1", "item-2" }) {
                VisualLayer layer = Layer(result, name);
                double fontSize = Style(layer).GetProperty("FontSize").GetDouble();
                double centerY = (layer.Transform.PositionY + 0.5) * Height;
                Assert.True(centerY + fontSize * VideoTextStyles.LineHeight / 2 <= 0.2 * Height, $"{name} reaches {centerY + fontSize * VideoTextStyles.LineHeight / 2}");
                Assert.True(centerY - fontSize * VideoTextStyles.LineHeight / 2 >= 0.07 * Height - 1e-6, $"{name} starts at {centerY - fontSize * VideoTextStyles.LineHeight / 2}");
            }
            double first = (Layer(result, "item-0").Transform.PositionX + 0.5) * Width - "Legalizar".Length * FixedAdvanceTextMeasurer.Advance * size / 2;
            double last = (Layer(result, "item-2").Transform.PositionX + 0.5) * Width + "Tratar".Length * FixedAdvanceTextMeasurer.Advance * size / 2;
            Assert.Equal(Width / 2, (first + last) / 2, 3);
        }

        /// <summary>
        /// A full-frame picture leaves the layout exactly as on a plain take scene: the graphic stays in the style's area
        /// at full size, over the picture.
        /// </summary>
        [Fact]
        public void Compile_FullFrameScene_KeepsTheStyleArea() {
            VideoCompileResult take = VideoEditCompiler.Compile(GraphicEditSamples.ContrastChain(), GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));
            VideoEdit edit = GraphicEditSamples.ContrastChain();
            edit.Scenes[0].Layers = [new VideoLayer { Id = "take", Kind = "take", Fit = "cover", Layout = new VideoLayout { Preset = "full_frame" } }];

            VideoCompileResult full = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.False(full.HasErrors, Describe(full));
            foreach (string name in new[] { "item-0", "separator-0", "item-1", "separator-1", "item-2" }) {
                Assert.Equal(Layer(take, name).Transform.PositionY, Layer(full, name).Transform.PositionY, 9);
                Assert.Equal(Style(Layer(take, name)).GetProperty("FontSize").GetDouble(), Style(Layer(full, name)).GetProperty("FontSize").GetDouble(), 6);
            }
            Assert.Equal(96, Style(Layer(full, "item-1")).GetProperty("FontSize").GetDouble(), 3);
        }

        /// <summary>
        /// Picture bounds follow the compositor: a contained portrait source sits centered inside the inset viewport, a
        /// covered source that does not clip spills past the viewport, clipping or an opaque padding color makes the whole
        /// viewport count, and the static position moves the picture by viewport fractions.
        /// </summary>
        [Fact]
        public void PictureBounds_FollowFitClipPaddingAndTransform() {
            VideoMedia portrait = new VideoMedia { Id = "p", Kind = "image", Width = 1080, Height = 1920 };
            VideoLayout inset = new VideoLayout { Preset = "inset" };

            VideoGraphicRectangle contained = VideoPictureBounds.Resolve(new VideoLayer { Kind = "media", Fit = "contain", Layout = inset }, portrait, Width, Height);
            VideoGraphicRectangle spilled = VideoPictureBounds.Resolve(new VideoLayer { Kind = "media", Fit = "cover", Layout = inset }, portrait, Width, Height);
            VideoGraphicRectangle clipped = VideoPictureBounds.Resolve(new VideoLayer { Kind = "media", Fit = "cover", Layout = inset, ClipToViewport = true }, portrait, Width, Height);
            VideoGraphicRectangle padded = VideoPictureBounds.Resolve(new VideoLayer { Kind = "media", Fit = "contain", Layout = inset, PaddingColor = "#F7F5FAFF" }, portrait, Width, Height);
            VideoGraphicRectangle moved = VideoPictureBounds.Resolve(new VideoLayer { Kind = "media", Fit = "contain", Layout = inset, Transform = new VideoTransform { PositionY = 0.25 } }, portrait, Width, Height);

            Assert.Equal(0.2 * Height, contained.Top, 6);
            Assert.Equal(0.68 * Height, contained.Bottom, 6);
            Assert.Equal(Width / 2, (contained.Left + contained.Right) / 2, 6);
            Assert.True(contained.Width < 0.88 * Width);
            Assert.True(spilled.Top < 0.2 * Height && spilled.Bottom > 0.68 * Height);
            Assert.Equal(0.06 * Width, clipped.Left, 6);
            Assert.Equal(0.68 * Height, clipped.Bottom, 6);
            Assert.Equal(0.06 * Width, padded.Left, 6);
            Assert.Equal(0.94 * Width, padded.Right, 6);
            Assert.Equal(contained.Top + 0.25 * 0.48 * Height, moved.Top, 6);
        }

        /// <summary>
        /// Finds the element layer with a name suffix such as <c>item-0</c>.
        /// </summary>
        /// <param name="result">Compile result.</param>
        /// <param name="name">Element name and index.</param>
        /// <returns>Layer.</returns>
        static VisualLayer Layer(VideoCompileResult result, string name) {
            Assert.NotNull(result.Composition);
            return result.Composition.Layers.Single(layer => layer.Id == "contrast-overlay-chain-" + name);
        }

        /// <summary>
        /// Returns the text style snapshot of a layer.
        /// </summary>
        /// <param name="layer">Text layer.</param>
        /// <returns>Style snapshot.</returns>
        static JsonElement Style(VisualLayer layer) {
            return layer.Text.Style;
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
