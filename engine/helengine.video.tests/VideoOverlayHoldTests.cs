using System.Text.Json;
using helengine.media;

namespace helengine.video.tests {
    /// <summary>
    /// Verifies the overlay reading time (<c>hold_sec</c>): a graphic, timeline or plain overlay of a short scene stays on
    /// screen after its last item for its reading time, continuing over the next scene as a document-level layer, a
    /// timeline fades out at the held end, the hold is capped at the end of the video, a held overlay that meets another
    /// overlay of the next scene in the same place is reported, and the field is validated, exported and merged.
    /// </summary>
    public class VideoOverlayHoldTests {
        /// <summary>
        /// The contrast graphic's last term is spoken at 2.9 s in a 3.2 s scene: two seconds of reading time keep the
        /// whole graphic until 4.9 s, over the next scene, owned by no scene group.
        /// </summary>
        [Fact]
        public void Compile_GraphicHeldPastShortScene_ContinuesOverNextScene() {
            VideoEdit edit = ShortScenes();
            edit.Scenes[0].Overlays[0].HoldSec = 2;

            VideoCompileResult result = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.False(result.HasErrors, Describe(result));
            VisualLayer group = Layer(result, "contrast-overlay-chain");
            Assert.Equal(0.5, group.Start.ToSeconds(), 6);
            Assert.Equal(4.9, group.End.ToSeconds(), 6);
            Assert.Equal(4.9, Layer(result, "contrast-overlay-chain-item-2").End.ToSeconds(), 6);
            Assert.All(result.Composition.Layers.Where(layer => layer.Kind == "group"), layer => Assert.DoesNotContain(group.Id, layer.Members));
            Assert.Equal(VideoSceneCompiler.OverlayOrder, group.Order);
            PropertyAnimation fade = group.Animations.Single(animation => animation.Property == "opacity");
            Assert.Equal(4.4, fade.Keyframes[^1].Time.ToSeconds(), 6);
            VideoOverlayPlacement placement = Assert.Single(result.Overlays);
            Assert.Same(group, placement.Layer);
            Assert.Equal("chain", placement.Overlay.Id);
            Assert.NotNull(placement.Bounds);
            Assert.Empty(CompositionValidator.Validate(result.Composition, GraphicEditSamples.Catalog()));
        }

        /// <summary>
        /// Without a reading time the graphic still ends with its scene inside the scene group.
        /// </summary>
        [Fact]
        public void Compile_GraphicWithoutHold_EndsWithScene() {
            VideoCompileResult result = VideoEditCompiler.Compile(ShortScenes(), GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            VisualLayer group = Layer(result, "contrast-overlay-chain");
            Assert.Equal(3.2, group.End.ToSeconds(), 6);
            Assert.Contains(group.Id, Layer(result, "scene-contrast").Members);
        }

        /// <summary>
        /// A reading time longer than the rest of the video stops at the end of the video.
        /// </summary>
        [Fact]
        public void Compile_HoldPastVideoEnd_IsCappedAtVideoEnd() {
            VideoEdit edit = ShortScenes();
            edit.Scenes[0].Overlays[0].HoldSec = 10;

            VideoCompileResult result = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.False(result.HasErrors, Describe(result));
            Assert.Equal(result.Composition.Duration.ToSeconds(), Layer(result, "contrast-overlay-chain").End.ToSeconds(), 6);
        }

        /// <summary>
        /// The contrast timeline's strike appears at 3.35 s in a 3.2 s scene: 0.3 s of reading time keep it until 3.65 s
        /// (before the timeline's own end) and the fade-out moves to that held end.
        /// </summary>
        [Fact]
        public void Compile_TimelineHeldPastScene_FadesAtHeldEnd() {
            VideoEdit edit = ShortTimeline();
            edit.Scenes[0].Overlays[0].HoldSec = 0.3;

            VideoCompileResult result = VideoEditCompiler.Compile(edit, TimelineEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.False(result.HasErrors, Describe(result));
            VisualLayer group = Layer(result, "contrast-overlay-contrast");
            Assert.Equal(3.65, group.End.ToSeconds(), 6);
            Assert.DoesNotContain(group.Id, Layer(result, "scene-contrast").Members);
            PropertyAnimation fade = group.Animations.Single(animation => animation.Property == "opacity");
            Assert.Equal((group.End - group.Start).ToSeconds(), fade.Keyframes[^1].Time.ToSeconds(), 6);
            Assert.Equal(0, fade.Keyframes[^1].Value, 6);
            Assert.Equal(3.65, TimelineEditSamples.Slot(result.Composition, "strike").End.ToSeconds(), 6);
        }

        /// <summary>
        /// A reading time reaching past the timeline's own end (4.1 s, the cues having moved) ends with the timeline, without the
        /// cut-short fade.
        /// </summary>
        [Fact]
        public void Compile_TimelineHeldPastItsDuration_EndsWithTimeline() {
            VideoEdit edit = ShortTimeline();
            edit.Scenes[0].Overlays[0].HoldSec = 3;

            VideoCompileResult result = VideoEditCompiler.Compile(edit, TimelineEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.False(result.HasErrors, Describe(result));
            VisualLayer group = Layer(result, "contrast-overlay-contrast");
            Assert.Equal(4.1, group.End.ToSeconds(), 6);
            Assert.DoesNotContain(group.Animations, animation => animation.Property == "opacity");
        }

        /// <summary>
        /// A plain overlay with an until earlier than its reading time stays for the reading time, past its scene.
        /// </summary>
        [Fact]
        public void Compile_PlainHoldBeatsEarlierUntil() {
            VideoEdit edit = ShortPlain(2.5, 2.8, 1.5);

            VideoCompileResult result = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.False(result.HasErrors, Describe(result));
            VisualLayer layer = Layer(result, "contrast-overlay-note");
            Assert.Equal(2.5, layer.Start.ToSeconds(), 6);
            Assert.Equal(4.0, layer.End.ToSeconds(), 6);
            Assert.Equal(4.0, layer.Text.Cues[^1].End.ToSeconds(), 6);
            Assert.DoesNotContain(layer.Id, Layer(result, "scene-contrast").Members);
        }

        /// <summary>
        /// Without a reading time the plain overlay keeps its until.
        /// </summary>
        [Fact]
        public void Compile_PlainWithoutHold_KeepsUntil() {
            VideoCompileResult result = VideoEditCompiler.Compile(ShortPlain(2.5, 2.8, null), GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            VisualLayer layer = Layer(result, "contrast-overlay-note");
            Assert.Equal(2.8, layer.End.ToSeconds(), 6);
            Assert.Contains(layer.Id, Layer(result, "scene-contrast").Members);
        }

        /// <summary>
        /// A plain overlay held into the next scene while that scene shows its own text in the same place is reported;
        /// a later text that starts after the held one ends is not.
        /// </summary>
        [Theory]
        [InlineData(0.2, true)]
        [InlineData(1.0, false)]
        public void Compile_HeldOverlayMeetsNextSceneOverlay_Warns(double nextAt, bool overlaps) {
            VideoEdit edit = ShortPlain(2.5, null, 1.5);
            edit.Scenes[1].Overlays = [new VideoOverlay { Id = "next-note", Text = "OUTRA COISA", At = new VideoMoment { Sec = nextAt } }];

            VideoCompileResult result = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.False(result.HasErrors, Describe(result));
            VideoDiagnostic warning = result.Diagnostics.SingleOrDefault(diagnostic => diagnostic.Code == "overlay_hold_overlap");
            Assert.Equal(overlaps, warning != null);
            if (overlaps) {
                Assert.Equal(VideoDiagnosticSeverity.Warning, warning.Severity);
                Assert.Equal("next", warning.Scene);
                Assert.Equal("scenes[1].overlays[0]", warning.Path);
            }
        }

        /// <summary>
        /// Simultaneous plain overlays compiled as one stacked list are reported one by one, each with its own interval
        /// (its reading time included) and the list's box.
        /// </summary>
        [Fact]
        public void Compile_StackedOverlays_ArePlacedOneByOne() {
            VideoEdit edit = ShortPlain(0.5, null, null);
            edit.Scenes[0].Overlays.Add(new VideoOverlay { Id = "second", Text = "OUTRA", At = new VideoMoment { Sec = 1 }, Until = new VideoMoment { Sec = 1.5 }, HoldSec = 2 });

            VideoCompileResult result = VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));

            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "overlays_stacked");
            Assert.Equal(["note", "second"], result.Overlays.Select(placement => placement.Overlay.Id));
            Assert.Equal([0.5, 1.0], result.Overlays.Select(placement => Math.Round(placement.Start.ToSeconds(), 6)));
            Assert.Equal([3.2, 3.0], result.Overlays.Select(placement => Math.Round(placement.End.ToSeconds(), 6)));
            Assert.Same(result.Overlays[0].Layer, result.Overlays[1].Layer);
        }

        /// <summary>
        /// Reading times outside 0 to 10 seconds are rejected.
        /// </summary>
        [Theory]
        [InlineData(-1, true)]
        [InlineData(10.5, true)]
        [InlineData(double.NaN, true)]
        [InlineData(0, false)]
        [InlineData(10, false)]
        public void Validate_HoldSecRange(double hold, bool rejected) {
            VideoEdit edit = ShortScenes();
            edit.Scenes[0].Overlays[0].HoldSec = hold;

            IReadOnlyList<VideoDiagnostic> diagnostics = VideoEditValidator.Validate(edit, GraphicEditSamples.Catalog());

            Assert.Equal(rejected, diagnostics.Any(diagnostic => diagnostic.Code == "invalid_overlay" && diagnostic.Path == "scenes[0].overlays[0].hold_sec"));
        }

        /// <summary>
        /// The field is written as hold_sec, read back, omitted when absent, and bounded in the schema.
        /// </summary>
        [Fact]
        public void Json_HoldSecRoundTripsAndIsBoundedInSchema() {
            VideoEdit edit = ShortScenes();
            edit.Scenes[0].Overlays[0].HoldSec = 2.5;

            string json = VideoEditJson.Serialize(edit);

            Assert.Contains("\"hold_sec\": 2.5", json);
            Assert.Equal(2.5, VideoEditJson.Parse(json).Scenes[0].Overlays[0].HoldSec);
            Assert.DoesNotContain("hold_sec", VideoEditJson.Serialize(ShortScenes()));
            JsonElement hold = VideoEditSchema.Create().GetProperty("properties").GetProperty("scenes").GetProperty("items").GetProperty("properties")
                .GetProperty("overlays").GetProperty("items").GetProperty("properties").GetProperty("hold_sec");
            Assert.Equal(0, hold.GetProperty("minimum").GetDouble());
            Assert.Equal(VideoOverlayHold.MaximumHoldSec, hold.GetProperty("maximum").GetDouble());
        }

        /// <summary>
        /// A human-locked overlay keeps its reading time through a replan that drops it.
        /// </summary>
        [Fact]
        public void Replan_LockedOverlayKeepsHold() {
            VideoEdit current = ShortScenes();
            current.Scenes[0].Overlays[0].By = "human";
            current.Scenes[0].Overlays[0].HoldSec = 3;
            VideoEdit proposal = ShortScenes();

            VideoEdit merged = VideoEditMerge.Replan(current, proposal);

            Assert.Equal(3, merged.Scenes[0].Overlays[0].HoldSec);
        }

        /// <summary>
        /// Builds the contrast chain edit cut to a 3.2 s scene, followed by a 4 s scene of the same take.
        /// </summary>
        /// <returns>Valid edit.</returns>
        static VideoEdit ShortScenes() {
            VideoEdit edit = GraphicEditSamples.ContrastChain();
            edit.Scenes[0].Take.OutSec = GraphicEditSamples.TakeIn + 3.2;
            edit.Scenes.Add(new VideoScene { Id = "next", Duration = new VideoSceneDuration { Mode = "from_take" }, Take = new VideoTake { Media = "take", InSec = 15, OutSec = 19 } });
            return edit;
        }

        /// <summary>
        /// Builds the contrast timeline edit cut to a 3.2 s scene, followed by a 4 s scene.
        /// </summary>
        /// <returns>Valid edit.</returns>
        static VideoEdit ShortTimeline() {
            VideoEdit edit = TimelineEditSamples.Contrast();
            edit.Scenes[0].Take.OutSec = GraphicEditSamples.TakeIn + 3.2;
            edit.Scenes.Add(new VideoScene { Id = "next", Duration = new VideoSceneDuration { Mode = "from_take" }, Take = new VideoTake { Media = "take", InSec = 15, OutSec = 19 } });
            return edit;
        }

        /// <summary>
        /// Builds the short scenes with one plain overlay in the first scene instead of the graphic.
        /// </summary>
        /// <param name="at">Start in scene seconds.</param>
        /// <param name="until">End in scene seconds, or null for the scene end.</param>
        /// <param name="hold">Reading time, or null.</param>
        /// <returns>Valid edit.</returns>
        static VideoEdit ShortPlain(double at, double? until, double? hold) {
            VideoEdit edit = ShortScenes();
            edit.Scenes[0].Overlays = [new VideoOverlay { Id = "note", Text = "NÃO É A MESMA COISA", At = new VideoMoment { Sec = at }, Until = until.HasValue ? new VideoMoment { Sec = until } : null, HoldSec = hold }];
            return edit;
        }

        /// <summary>
        /// Finds one compiled layer.
        /// </summary>
        /// <param name="result">Compile result.</param>
        /// <param name="id">Layer id.</param>
        /// <returns>Layer.</returns>
        static VisualLayer Layer(VideoCompileResult result, string id) {
            Assert.NotNull(result.Composition);
            return result.Composition.Layers.Single(layer => layer.Id == id);
        }

        /// <summary>
        /// Lists the diagnostics of a result for assertion messages.
        /// </summary>
        /// <param name="result">Compile result.</param>
        /// <returns>One line per diagnostic.</returns>
        static string Describe(VideoCompileResult result) {
            return string.Join("\n", result.Diagnostics.Select(diagnostic => $"{diagnostic.Severity} {diagnostic.Code} {diagnostic.Path}: {diagnostic.Message}"));
        }
    }
}
