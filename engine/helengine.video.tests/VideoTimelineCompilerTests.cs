using System.Text.Json;
using helengine.media;

namespace helengine.video.tests {
    /// <summary>
    /// Compiles the "LEGALIZAR ≠ DESCRIMINALIZAR ≠ TRATAR" overlay timeline and checks the composition it becomes: one
    /// group owning a layer per slot, clips moved to the spoken words without stretching, every element inside the region
    /// box, uniform shrinking for long texts, and the pop, tilt, fade and strike animations.
    /// </summary>
    public class VideoTimelineCompilerTests {
        /// <summary>
        /// Scene time of the spoken words (take words minus the take in point).
        /// </summary>
        const double Legalizar = 0.5, Descriminalizar = 1.5, Tratar = 2.9;

        /// <summary>
        /// The overlay becomes a group spanning the timeline with one layer per slot, and the composition is valid.
        /// </summary>
        [Fact]
        public void Compile_BuildsAGroupWithOneLayerPerSlot() {
            VideoCompileResult result = Compile(TimelineEditSamples.Contrast());

            VisualLayer group = result.Composition.Layers.Single(layer => layer.Id == "contrast-overlay-contrast");
            Assert.Equal("group", group.Kind);
            Assert.Equal(["contrast-overlay-contrast-term_a", "contrast-overlay-contrast-sep_ab", "contrast-overlay-contrast-term_b", "contrast-overlay-contrast-sep_bc", "contrast-overlay-contrast-term_c", "contrast-overlay-contrast-strike"], group.Members);
            Assert.Contains(group.Id, result.Composition.Layers.Single(layer => layer.Id == "scene-contrast").Members);
            Assert.Equal(Legalizar - 0.15, group.Start.ToSeconds(), 6);
            Assert.Equal(Legalizar - 0.15 + 3.6 + (Tratar - Legalizar - 2.25), group.End.ToSeconds(), 6);
            Assert.Equal("text", TimelineEditSamples.Slot(result.Composition, "strike").Kind);
            Assert.Empty(CompositionValidator.Validate(result.Composition, GraphicEditSamples.Catalog()));
        }

        /// <summary>
        /// Clips anchored on a cue start on its spoken word and keep their authored length.
        /// </summary>
        [Fact]
        public void Compile_MovesClipsToTheWordMomentsWithoutStretching() {
            VideoCompileResult result = Compile(TimelineEditSamples.Contrast());
            CompositionDocument composition = result.Composition;

            Assert.Equal(Legalizar, TimelineEditSamples.Slot(composition, "term_a").Start.ToSeconds(), 6);
            Assert.Equal(Descriminalizar - 0.2, TimelineEditSamples.Slot(composition, "sep_ab").Start.ToSeconds(), 6);
            Assert.Equal(Descriminalizar, TimelineEditSamples.Slot(composition, "term_b").Start.ToSeconds(), 6);
            Assert.Equal(Tratar - 0.2, TimelineEditSamples.Slot(composition, "sep_bc").Start.ToSeconds(), 6);
            Assert.Equal(Tratar, TimelineEditSamples.Slot(composition, "term_c").Start.ToSeconds(), 6);
            PropertyAnimation pop = Animation(TimelineEditSamples.Slot(composition, "term_b"), "scale_x");
            Assert.Equal(0, pop.Keyframes[0].Time.ToSeconds(), 6);
            Assert.Equal("ease_out_back.v1", pop.Keyframes[0].Curve);
            Assert.Equal(0.6, pop.Keyframes[0].Value, 6);
            Assert.Equal(0.3, pop.Keyframes[^1].Time.ToSeconds(), 6);
            Assert.Equal(1, pop.Keyframes[^1].Value, 6);
            PropertyAnimation fade = Animation(TimelineEditSamples.Slot(composition, "sep_bc"), "opacity");
            Assert.Equal("smoothstep.v1", fade.Keyframes[0].Curve);
            Assert.Equal(0.2, fade.Keyframes[^1].Time.ToSeconds(), 6);
        }

        /// <summary>
        /// Moving the words moves the clips: with the last word a second later, the last term starts a second later and
        /// its pop keeps its length.
        /// </summary>
        [Fact]
        public void Compile_FollowsTheCueMoments() {
            VideoEdit edit = TimelineEditSamples.Contrast();
            edit.Scenes[0].Overlays[0].Timeline.Cues["c"] = new VideoMoment { Sec = Tratar + 1 };

            CompositionDocument composition = Compile(edit).Composition;

            VisualLayer term = TimelineEditSamples.Slot(composition, "term_c");
            Assert.Equal(Tratar + 1, term.Start.ToSeconds(), 6);
            PropertyAnimation tilt = Animation(term, "rotation_deg");
            Assert.Equal(-6, tilt.Keyframes[0].Value, 6);
            Assert.Equal("ease_out_cubic.v1", tilt.Keyframes[0].Curve);
            Assert.Equal(0.35 / 1, tilt.Keyframes[^1].Time.ToSeconds(), 6);
            Assert.Equal(0, tilt.Keyframes[^1].Value, 6);
        }

        /// <summary>
        /// Every element stays inside the region box at rest, the terms are stacked in order, and the strike spans the last term.
        /// </summary>
        [Fact]
        public void Compile_KeepsEveryElementInsideTheRegion() {
            VideoEdit edit = TimelineEditSamples.Contrast();
            CompositionDocument composition = Compile(edit).Composition;
            VideoGraphicRectangle box = Box(edit);

            foreach (string slot in new[] { "term_a", "sep_ab", "term_b", "sep_bc", "term_c" }) {
                VideoGraphicRectangle bounds = TextBounds(edit, composition, slot);
                Assert.True(box.Contains(bounds), $"{slot} at {bounds.Left:0},{bounds.Top:0}-{bounds.Right:0},{bounds.Bottom:0} leaves the box {box.Left:0},{box.Top:0}-{box.Right:0},{box.Bottom:0}");
            }
            double a = Center(edit, composition, "term_a", "position_y"), b = Center(edit, composition, "term_b", "position_y"), c = Center(edit, composition, "term_c", "position_y");
            Assert.True(a < b && b < c);
            VisualLayer strike = TimelineEditSamples.Slot(composition, "strike");
            Assert.Equal(c, Center(edit, composition, "strike", "position_y"), 3);
            Assert.Equal(TextBounds(edit, composition, "term_c").Width, Final(strike, "scale_x") * PanelWidth(strike), 0);
            PropertyAnimation reveal = Animation(strike, "scale_x");
            Assert.True(reveal.Keyframes[0].Value < reveal.Keyframes[^1].Value / 10);
        }

        /// <summary>
        /// Longer texts shrink the whole timeline uniformly (every text by the same factor) so it still fits the box.
        /// </summary>
        [Fact]
        public void Compile_ShrinksLongTextsUniformly() {
            VideoEdit shortEdit = TimelineEditSamples.Contrast();
            VideoEdit longEdit = TimelineEditSamples.Contrast("LEGALIZAR TODAS AS DROGAS", "DESCRIMINALIZAR O USO PESSOAL", "TRATAR COMO SAÚDE PÚBLICA");
            CompositionDocument shortComposition = Compile(shortEdit).Composition, longComposition = Compile(longEdit).Composition;

            double shortTerm = FontSize(shortComposition, "term_a"), longTerm = FontSize(longComposition, "term_a");
            double shortSeparator = FontSize(shortComposition, "sep_ab"), longSeparator = FontSize(longComposition, "sep_ab");
            Assert.True(longTerm < shortTerm * 0.8, $"{longTerm} is not smaller than {shortTerm}");
            Assert.Equal(shortTerm / shortSeparator, longTerm / longSeparator, 3);
            VideoGraphicRectangle box = Box(longEdit);
            foreach (string slot in new[] { "term_a", "term_b", "term_c" }) {
                Assert.True(box.Contains(TextBounds(longEdit, longComposition, slot)), slot + " leaves the box");
            }
        }

        /// <summary>
        /// Without a region the timeline uses the free part of the frame, and without a measurer it reports the estimate.
        /// </summary>
        [Fact]
        public void Compile_WithoutRegion_UsesTheFreeAreaAndReportsEstimates() {
            VideoEdit edit = TimelineEditSamples.Contrast();
            edit.Scenes[0].Arrangement = null;
            edit.Scenes[0].Layers = [];
            edit.Scenes[0].Overlays[0].Region = null;
            VideoCompileContext context = TimelineEditSamples.Context(null);

            VideoCompileResult result = VideoEditCompiler.Compile(edit, context);

            Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics.Select(item => item.Code + " " + item.Path + " " + item.Message)));
            Assert.Contains(result.Diagnostics, item => item.Code == "text_measure_estimated");
            VisualLayer term = TimelineEditSamples.Slot(result.Composition, "term_b");
            Assert.InRange(term.Transform.PositionY, -0.5, 0.5);
        }

        /// <summary>
        /// An <c>until</c> before the timeline end cuts the overlay and fades the group out.
        /// </summary>
        [Fact]
        public void Compile_UntilCutsTheTimelineAndFadesOut() {
            VideoEdit edit = TimelineEditSamples.Contrast();
            edit.Scenes[0].Overlays[0].Until = new VideoMoment { Sec = 3.5 };

            CompositionDocument composition = Compile(edit).Composition;

            VisualLayer group = composition.Layers.Single(layer => layer.Id == "contrast-overlay-contrast");
            Assert.Equal(3.5, group.End.ToSeconds(), 6);
            Assert.Contains(group.Animations, animation => animation.Property == "opacity" && animation.Keyframes[^1].Value == 0);
            Assert.Equal(3.5, TimelineEditSamples.Slot(composition, "term_a").End.ToSeconds(), 6);
        }

        /// <summary>
        /// When the moments make two clips of one track overlap, the compiler reports it inside the definition.
        /// </summary>
        [Fact]
        public void Compile_ReportsOverlapsTheMomentsCreate() {
            VideoEdit edit = TimelineEditSamples.Contrast();
            TimelineEditSamples.ChangeDefinition(edit, node => node["tracks"][4]["clips"].AsArray().Add(System.Text.Json.Nodes.JsonNode.Parse("{\"start\":{\"cue\":\"c\"},\"duration\":0.3,\"keyframes\":[{\"time\":0,\"value\":0.5}]}")));
            edit.Scenes[0].Overlays[0].Timeline.Cues["c"] = new VideoMoment { Sec = Descriminalizar - 0.1 };

            VideoCompileResult result = VideoEditCompiler.Compile(edit, TimelineEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.Contains(result.Diagnostics, item => item.Code == "invalid_timeline" && item.Path.StartsWith("scenes[0].overlays[0].timeline.definition.tracks[4]", StringComparison.Ordinal) && item.Message.StartsWith("With the cues moved", StringComparison.Ordinal));
        }

        /// <summary>
        /// A media slot becomes a full-frame image layer scaled to the bound size (0.3 of the box height, shrunk a little by the fit) and moved into place.
        /// </summary>
        [Fact]
        public void Compile_BindsImagesToMediaLayers() {
            VideoEdit edit = TimelineEditSamples.Contrast();
            edit.Media.Add(new VideoMedia { Id = "logo", Kind = "image", Path = "logo.png", Sha256 = new string('b', 64), Width = 200, Height = 100 });
            TimelineEditSamples.ChangeDefinition(edit, node => node["slots"][0]["kind"] = "media");
            edit.Scenes[0].Overlays[0].Timeline.Bindings["term_a"] = new VideoTimelineBinding { Media = "logo", Size = 0.3 };

            CompositionDocument composition = Compile(edit).Composition;

            VisualLayer logo = TimelineEditSamples.Slot(composition, "term_a");
            Assert.Equal("media", logo.Kind);
            Assert.Equal("logo", logo.MediaId);
            Assert.Equal(1, logo.Viewport.Width);
            Assert.Equal(Final(logo, "scale_x"), Final(logo, "scale_y"), 6);
            Assert.InRange(Final(logo, "scale_y") * 540, 0.25 * Box(edit).Height, 0.3 * Box(edit).Height);
            Assert.True(Final(logo, "position_y") < 0);
        }

        /// <summary>
        /// Compiles an edit with the fixed-advance measurer and requires a clean result.
        /// </summary>
        /// <param name="edit">Edit.</param>
        /// <returns>Compile result with a composition.</returns>
        static VideoCompileResult Compile(VideoEdit edit) {
            VideoCompileResult result = VideoEditCompiler.Compile(edit, TimelineEditSamples.Context(new FixedAdvanceTextMeasurer()));
            Assert.False(result.HasErrors, string.Join("; ", result.Diagnostics.Select(item => item.Code + " " + item.Path + " " + item.Message)));
            Assert.NotNull(result.Composition);
            return result;
        }

        /// <summary>
        /// Finds one animation of a layer.
        /// </summary>
        /// <param name="layer">Layer.</param>
        /// <param name="property">Property.</param>
        /// <returns>Animation.</returns>
        static PropertyAnimation Animation(VisualLayer layer, string property) {
            return layer.Animations.Single(animation => animation.Property == property);
        }

        /// <summary>
        /// Reads the value a property settles on: its last keyframe, or the static transform value.
        /// </summary>
        /// <param name="layer">Layer.</param>
        /// <param name="property">Property.</param>
        /// <returns>Final value.</returns>
        static double Final(VisualLayer layer, string property) {
            PropertyAnimation animation = layer.Animations.SingleOrDefault(item => item.Property == property);
            if (animation != null) {
                return animation.Keyframes[^1].Value;
            }
            LayerTransform transform = layer.Transform;
            return property switch {
                "position_x" => transform.PositionX,
                "position_y" => transform.PositionY,
                "scale_x" => transform.ScaleX,
                "scale_y" => transform.ScaleY,
                _ => throw new ArgumentException(property)
            };
        }

        /// <summary>
        /// Computes the frame pixel coordinate a layer's center settles on.
        /// </summary>
        /// <param name="edit">Edit (frame size).</param>
        /// <param name="composition">Composition.</param>
        /// <param name="slot">Slot.</param>
        /// <param name="property">position_x or position_y.</param>
        /// <returns>Pixel coordinate.</returns>
        static double Center(VideoEdit edit, CompositionDocument composition, string slot, string property) {
            double size = property == "position_x" ? edit.Format.Width : edit.Format.Height;
            return (0.5 + Final(TimelineEditSamples.Slot(composition, slot), property)) * size;
        }

        /// <summary>
        /// Computes the frame rectangle a text layer settles on, measured with the fixed-advance measurer.
        /// </summary>
        /// <param name="edit">Edit.</param>
        /// <param name="composition">Composition.</param>
        /// <param name="slot">Slot.</param>
        /// <returns>Rectangle in pixels.</returns>
        static VideoGraphicRectangle TextBounds(VideoEdit edit, CompositionDocument composition, string slot) {
            VisualLayer layer = TimelineEditSamples.Slot(composition, slot);
            string text = layer.Text.Cues[0].Text;
            double font = FontSize(composition, slot);
            double width = text.Length * FixedAdvanceTextMeasurer.Advance * font * Final(layer, "scale_x"), height = font * VideoTextStyles.LineHeight * Final(layer, "scale_y");
            double x = Center(edit, composition, slot, "position_x"), y = Center(edit, composition, slot, "position_y");
            return new VideoGraphicRectangle(x - width / 2, y - height / 2, x + width / 2, y + height / 2);
        }

        /// <summary>
        /// Reads the font size of a text layer.
        /// </summary>
        /// <param name="composition">Composition.</param>
        /// <param name="slot">Slot.</param>
        /// <returns>Font size in pixels.</returns>
        static double FontSize(CompositionDocument composition, string slot) {
            return VideoTextStyles.Number(TimelineEditSamples.Slot(composition, slot).Text.Style, "FontSize", 0);
        }

        /// <summary>
        /// Computes the panel width a rect layer is drawn with at scale 1.
        /// </summary>
        /// <param name="layer">Rect layer.</param>
        /// <returns>Panel width in pixels.</returns>
        static double PanelWidth(VisualLayer layer) {
            double font = VideoTextStyles.Number(layer.Text.Style, "FontSize", 0);
            return layer.Text.Cues[0].Text.Length * FixedAdvanceTextMeasurer.Advance * font + 16;
        }

        /// <summary>
        /// Computes the overlay box: the top region of the stack arrangement, kept clear of the caption band.
        /// </summary>
        /// <param name="edit">Edit.</param>
        /// <returns>Box in pixels, with half a pixel of slack.</returns>
        static VideoGraphicRectangle Box(VideoEdit edit) {
            VideoGraphicRectangle region = VideoArrangementPresets.Region(edit, edit.Scenes[0], "top").Rectangle(edit.Format.Width, edit.Format.Height);
            return VideoGraphicSafeArea.ForRegion(edit, region).Bounds.Inflate(0.5);
        }
    }
}
