using helengine.media;

namespace helengine.video.tests {
    /// <summary>
    /// Verifies that plain overlays shown at the same time in the same place are stacked as one list instead of being
    /// drawn on top of each other.
    /// </summary>
    public class VideoOverlayStackingTests {
        /// <summary>
        /// Three terms that pile up until the scene end become one list graphic whose items appear at their own moments.
        /// </summary>
        [Fact]
        public void Compile_SimultaneousPlainOverlays_AreStackedAsAList() {
            VideoEdit edit = Plain(Overlay("a", "LEGALIZAÇÃO", 0.5, null), Overlay("b", "DESCRIMINALIZAÇÃO", 1.5, null), Overlay("c", "REDUÇÃO DE DANOS", 2.5, null));

            VideoCompileResult result = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Code + " " + diagnostic.Message)));
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "overlays_stacked");
            VisualLayer group = result.Composition.Layers.Single(layer => layer.Id == "contrast-overlay-a-stack");
            Assert.Equal("group", group.Kind);
            Assert.DoesNotContain(result.Composition.Layers, layer => layer.Id == "contrast-overlay-b" || layer.Id == "contrast-overlay-c");
            double[] starts = new[] { "item-0", "item-1", "item-2" }.Select(name => result.Composition.Layers.Single(layer => layer.Id == group.Id + "-" + name).Start.ToSeconds()).ToArray();
            Assert.Equal([0.5, 1.5, 2.5], starts);
            double[] y = new[] { "item-0", "item-1", "item-2" }.Select(name => result.Composition.Layers.Single(layer => layer.Id == group.Id + "-" + name).Transform.PositionY).ToArray();
            Assert.True(y[0] < y[1] && y[1] < y[2]);
        }

        /// <summary>
        /// Overlays that follow each other keep their own layers.
        /// </summary>
        [Fact]
        public void Compile_SequentialPlainOverlays_StayApart() {
            VideoEdit edit = Plain(Overlay("a", "QUAL LEI?", 0.5, 1.5), Overlay("b", "QUAL PROPOSTA?", 1.5, 2.5));

            VideoCompileResult result = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.False(result.HasErrors);
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "overlays_stacked");
            Assert.Contains(result.Composition.Layers, layer => layer.Id == "contrast-overlay-a" && layer.Kind == "text");
            Assert.Contains(result.Composition.Layers, layer => layer.Id == "contrast-overlay-b" && layer.Kind == "text");
        }

        /// <summary>
        /// Builds a plain overlay in the graphic style.
        /// </summary>
        /// <param name="id">Overlay id.</param>
        /// <param name="text">Overlay text.</param>
        /// <param name="at">Start second in the scene.</param>
        /// <param name="until">End second in the scene, or null for the scene end.</param>
        /// <returns>Overlay.</returns>
        static VideoOverlay Overlay(string id, string text, double at, double? until) {
            return new VideoOverlay { Id = id, Text = text, Style = "graphic", At = new VideoMoment { Sec = at }, Until = until.HasValue ? new VideoMoment { Sec = until.Value } : null };
        }

        /// <summary>
        /// The sample scene with its graphic replaced by plain overlays.
        /// </summary>
        /// <param name="overlays">Overlays of the scene.</param>
        /// <returns>Edit.</returns>
        static VideoEdit Plain(params VideoOverlay[] overlays) {
            VideoEdit edit = GraphicEditSamples.ContrastChain();
            edit.Scenes[0].Overlays = overlays.ToList();
            return edit;
        }
    }
}
