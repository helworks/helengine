using System.Text.Json;
using helengine.media;

namespace helengine.video.tests {
    /// <summary>
    /// Verifies that text stays off the speaker's face: punch-in zooms are limited (and their focus shifted) so the head box
    /// of a take stays inside the face zone, also through segments and speed; overlays meeting the head move above or below
    /// it, scaled when needed; caption cues move below or above the head; text with no room left is reported; a held overlay
    /// avoids the head of the later scene it crosses; the scene facts report the framed head and the free bands; and the
    /// head boxes and face zone are validated and exported.
    /// </summary>
    public class VideoFaceFramingTests {
        /// <summary>
        /// Output height of the sample edit (1080 x 1920).
        /// </summary>
        const double Height = 1920;

        /// <summary>
        /// A 1.6x punch-in on a head 0.25..0.60 of the source height cannot fit the 0.16..0.70 zone (the head would be taller
        /// than the zone): the zoom is capped near 1.54 on both keyframes' ceiling, the curve keeps its two keyframes and its
        /// start, and every head sample lands inside the zone.
        /// </summary>
        [Fact]
        public void Compile_PunchInPushesHeadOutOfZone_ClampsZoomAndKeepsHeadInside() {
            VideoEdit edit = Talking(0.3, 0.25, 0.7, 0.6);
            edit.Scenes[0].Layers.Add(new VideoLayer { Id = "take", Kind = "take", Fit = "cover", Motion = Zoom(1, 1.6) });

            VideoCompileResult result = Compile(edit);

            Assert.False(result.HasErrors, Describe(result));
            PropertyAnimation zoom = Layer(result, "contrast-take").Animations.Single(animation => animation.Property == "zoom");
            Assert.Equal(2, zoom.Keyframes.Count);
            Assert.Equal(1, zoom.Keyframes[0].Value, 6);
            Assert.InRange(zoom.Keyframes[1].Value, 1.5, 1.545);
            VideoDiagnostic warning = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Code == "framing_adjusted");
            Assert.Equal(VideoDiagnosticSeverity.Warning, warning.Severity);
            Assert.Equal("contrast", warning.Scene);
            AssertInsideZone(result);
        }

        /// <summary>
        /// A small punch-in on a head well inside the zone is left exactly as authored, without a warning.
        /// </summary>
        [Fact]
        public void Compile_HeadInsideZone_KeepsMotion() {
            VideoEdit edit = Talking(0.35, 0.3, 0.65, 0.55);
            edit.Scenes[0].Layers.Add(new VideoLayer { Id = "take", Kind = "take", Fit = "cover", Motion = Zoom(1, 1.1) });

            VideoCompileResult result = Compile(edit);

            Assert.Equal(1.1, Layer(result, "contrast-take").Animations.Single(animation => animation.Property == "zoom").Keyframes[1].Value, 6);
            Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "framing_adjusted");
            Assert.NotEmpty(result.Heads["contrast"]);
        }

        /// <summary>
        /// A head high in the frame fits the zone at the authored 1.3x zoom once the focus moves up: the zoom is kept, only
        /// focus_y changes.
        /// </summary>
        [Fact]
        public void Compile_HeadHighInFrame_ShiftsFocusInsteadOfZoom() {
            VideoEdit edit = Talking(0.35, 0.2, 0.65, 0.45);
            edit.Scenes[0].Layers.Add(new VideoLayer { Id = "take", Kind = "take", Fit = "cover", Motion = Zoom(1, 1.3) });

            VideoCompileResult result = Compile(edit);

            VisualLayer take = Layer(result, "contrast-take");
            Assert.Equal(1.3, take.Animations.Single(animation => animation.Property == "zoom").Keyframes[1].Value, 6);
            Assert.True(take.Transform.FocusY < 0.5, $"focus_y {take.Transform.FocusY}");
            Assert.Single(result.Diagnostics, diagnostic => diagnostic.Code == "framing_adjusted");
            AssertInsideZone(result);
        }

        /// <summary>
        /// A jump-cut take played at 1.25x keeps its pieces and speed; every piece carries the clamped zoom, and the head
        /// samples are placed in scene time through the segments.
        /// </summary>
        [Fact]
        public void Compile_SegmentedFastTake_ClampsEveryPiece() {
            VideoEdit edit = Talking(0.3, 0.25, 0.7, 0.6);
            edit.Scenes[0].Take = new VideoTake { Media = "take", InSec = 10, OutSec = 14, Speed = 1.25, Segments = [new VideoTakeSegment { InSec = 10, OutSec = 11.5 }, new VideoTakeSegment { InSec = 12, OutSec = 14 }] };
            edit.Scenes[0].Layers.Add(new VideoLayer { Id = "take", Kind = "take", Fit = "cover", Motion = Zoom(1, 1.6) });

            VideoCompileResult result = Compile(edit);

            Assert.False(result.HasErrors, Describe(result));
            List<VisualLayer> pieces = result.Composition.Layers.Where(layer => layer.Id.StartsWith("contrast-take", StringComparison.Ordinal)).ToList();
            Assert.Equal(2, pieces.Count);
            Assert.All(pieces, piece => Assert.Equal(1.25, piece.Rate, 6));
            Assert.All(pieces.SelectMany(piece => piece.Animations.Where(animation => animation.Property == "zoom").SelectMany(animation => animation.Keyframes)), frame => Assert.True(frame.Value <= 1.545, $"zoom {frame.Value}"));
            Assert.All(result.Heads["contrast"], sample => Assert.True(sample.Time.ToSeconds() <= 3.0 + 1e-6, $"sample at {sample.Time.ToSeconds()}"));
            AssertInsideZone(result);
        }

        /// <summary>
        /// A plain overlay centered on the face moves into the free band above the head (the nearer one where it fits whole),
        /// keeping its size, and its placement reports the moved box.
        /// </summary>
        [Fact]
        public void Compile_OverlayOnFace_MovesAboveHead() {
            VideoEdit edit = Talking(0.3, 0.3, 0.7, 0.6);
            edit.Scenes[0].Overlays.Add(Plain("hello", "OI"));

            VideoCompileResult result = Compile(edit);

            VideoOverlayPlacement placement = Assert.Single(result.Overlays);
            VideoGraphicRectangle head = HeadBox(result);
            Assert.True(placement.Bounds.Bottom <= head.Top, $"overlay bottom {placement.Bounds.Bottom}, head top {head.Top}");
            Assert.Equal(96 * VideoTextStyles.LineHeight, placement.Bounds.Height, 3);
            Assert.True(placement.Layer.Transform.PositionY < 0);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "text_moved_from_face");
        }

        /// <summary>
        /// Below the head belongs to the captions: an overlay on the face never moves down there. It goes above the head
        /// (smaller if it must) or, when even that is impossible, stays and warns.
        /// </summary>
        [Fact]
        public void Compile_OverlayOnFace_NeverMovesBelowTheHead() {
            VideoEdit edit = Talking(0.3, 0.12, 0.7, 0.45);
            edit.Scenes[0].Overlays.Add(Plain("hello", "OI"));

            VideoCompileResult result = Compile(edit);

            VideoOverlayPlacement placement = Assert.Single(result.Overlays);
            bool warned = result.Diagnostics.Any(diagnostic => diagnostic.Code == "text_covers_face" && !diagnostic.Path.EndsWith(".captions", StringComparison.Ordinal));
            Assert.True(warned || placement.Bounds.Bottom <= HeadBox(result).Top + 1e-6, $"overlay bottom {placement.Bounds.Bottom}");
            Assert.True(placement.Layer.Transform.PositionY <= 0, "overlays never move down");
        }

        /// <summary>
        /// A graphic overlay too tall for either band is scaled down uniformly to fit the larger one.
        /// </summary>
        [Fact]
        public void Compile_GraphicTooTallForBand_IsScaledDown() {
            VideoEdit edit = Talking(0.3, 0.28, 0.7, 0.58);
            edit.Scenes[0].Overlays.Add(GraphicEditSamples.ContrastChain().Scenes[0].Overlays[0]);
            VideoGraphicRectangle before = Assert.Single(Compile(Talking(0.3, 0.28, 0.7, 0.58, withHeads: false, overlay: GraphicEditSamples.ContrastChain().Scenes[0].Overlays[0])).Overlays).Bounds;

            VideoCompileResult result = Compile(edit);

            VideoOverlayPlacement placement = Assert.Single(result.Overlays);
            VideoGraphicRectangle head = HeadBox(result);
            Assert.False(placement.Bounds.Overlaps(head), $"overlay {placement.Bounds.Top}..{placement.Bounds.Bottom}, head {head.Top}..{head.Bottom}");
            Assert.True(placement.Bounds.Height < before.Height, $"height {placement.Bounds.Height} of {before.Height}");
            Assert.Equal(placement.Layer.Transform.ScaleX, placement.Layer.Transform.ScaleY, 6);
            Assert.True(placement.Layer.Transform.ScaleX < 1);
        }

        /// <summary>
        /// A head filling the frame leaves no band for the overlay: it stays where it is and the scene gets a
        /// <c>text_covers_face</c> warning.
        /// </summary>
        [Fact]
        public void Compile_NoRoomAroundHead_KeepsOverlayAndWarns() {
            // The face fills the frame and the text is far too tall for the strips left above and below it.
            VideoEdit edit = Talking(0.05, 0.1, 0.95, 0.95);
            var style = System.Text.Json.Nodes.JsonNode.Parse(edit.TextStyles["graphic"].GetRawText())!.AsObject();
            style["FontSize"] = 900;
            edit.TextStyles["graphic"] = System.Text.Json.JsonSerializer.SerializeToElement(style);
            edit.Scenes[0].Overlays.Add(Plain("hello", "OI"));

            VideoCompileResult result = Compile(edit);

            VideoOverlayPlacement placement = Assert.Single(result.Overlays);
            Assert.Equal(0.4 * Height, (placement.Bounds.Top + placement.Bounds.Bottom) / 2, 3);
            Assert.Equal(0, placement.Layer.Transform.PositionY, 6);
            VideoDiagnostic warning = Assert.Single(result.Diagnostics, diagnostic => diagnostic.Code == "text_covers_face" && !diagnostic.Path.EndsWith(".captions", StringComparison.Ordinal));
            Assert.Equal("contrast", warning.Scene);
        }

        /// <summary>
        /// Caption cues over a low head cannot go below it (no room above the bottom margin), so they stay at the bottom and warn.
        /// </summary>
        [Fact]
        public void Compile_CaptionOnLowHead_StaysAtTheBottom() {
            VideoEdit edit = Talking(0.2, 0.4, 0.8, 0.85);

            VideoCompileResult result = Compile(edit);

            // No room below the head: the cue goes as low as the bottom margin allows instead of climbing over the face.
            List<VisualLayer> captions = result.Composition.Layers.Where(layer => layer.Id.StartsWith("caption-contrast-", StringComparison.Ordinal)).ToList();
            Assert.NotEmpty(captions);
            Assert.All(captions, caption => Assert.True(caption.Transform.PositionY >= 0, "captions never move up"));
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "text_covers_face");
        }

        /// <summary>
        /// Caption cues over a head that ends above them move just below the head when they fit above the bottom margin.
        /// </summary>
        [Fact]
        public void Compile_CaptionOnHead_MovesBelowWhenItFits() {
            VideoEdit edit = Talking(0.2, 0.3, 0.8, 0.74);

            VideoCompileResult result = Compile(edit);

            VideoGraphicRectangle head = HeadBox(result);
            VisualLayer caption = result.Composition.Layers.First(layer => layer.Id.StartsWith("caption-contrast-", StringComparison.Ordinal));
            double expected = (head.Bottom + VideoCaptionBand.Gap * Height + 2 * 64 * VideoTextStyles.LineHeight / 2 - 0.78 * Height) / Height;
            Assert.Equal(expected, caption.Transform.PositionY, 3);
            Assert.True(caption.Transform.PositionY > 0);
        }

        /// <summary>
        /// A text of a short scene without a take is held over the next scene, whose speaker's head is where the text sits:
        /// it moves off that later head. Without the reading time it ends with its own scene and stays put.
        /// </summary>
        [Fact]
        public void Compile_HeldOverlayAcrossScenes_AvoidsLaterHead() {
            VideoEdit held = HeldEdit(2);
            VideoEdit short_ = HeldEdit(null);

            VideoCompileResult result = Compile(held);
            VideoCompileResult unheld = Compile(short_);

            VideoOverlayPlacement placement = Assert.Single(result.Overlays);
            Assert.Equal("opening", placement.Span.Scene.Id);
            VideoGraphicRectangle head = HeadBox(result, "contrast");
            Assert.True(placement.Bounds.Bottom <= head.Top, $"overlay bottom {placement.Bounds.Bottom}, head top {head.Top}");
            Assert.Equal(0, Assert.Single(unheld.Overlays).Layer.Transform.PositionY, 6);
        }

        /// <summary>
        /// The scene facts report the head after framing (inside the zone), the zone and the free bands above and below it
        /// (a band too thin for text is left out).
        /// </summary>
        [Fact]
        public void Facts_TakeWithHeads_ReportFramedHeadAndFreeAreas() {
            VideoEdit edit = Talking(0.3, 0.25, 0.7, 0.6);
            edit.Scenes[0].Layers.Add(new VideoLayer { Id = "take", Kind = "take", Fit = "cover", Motion = Zoom(1, 1.6) });

            VideoSceneFact fact = VideoSceneFacts.Build(edit)[0];

            Assert.NotNull(fact.Head);
            Assert.InRange(fact.Head.Top, VideoFaceFraming.DefaultFaceTop - 0.001, 1);
            Assert.InRange(fact.Head.Bottom, 0, VideoFaceFraming.DefaultFaceBottom + 0.001);
            Assert.Equal(VideoFaceFraming.DefaultFaceTop, fact.FaceZone.Top, 6);
            Assert.Equal(VideoFaceFraming.DefaultFaceBottom, fact.FaceZone.Bottom, 6);
            // The framed head reaches down to the caption band, so only the band above it is left.
            VideoFactBand above = Assert.Single(fact.FreeAreas);
            Assert.True(above.Bottom <= fact.Head.Top);
            Assert.Equal(VideoFaceClearance.FrameMargin, above.Top, 3);
            Assert.Equal(2, VideoSceneFacts.Build(Talking(0.35, 0.3, 0.65, 0.5))[0].FreeAreas.Count);
            JsonElement json = JsonSerializer.SerializeToElement(fact, VideoEditJson.Options);
            Assert.True(json.TryGetProperty("free_areas", out _));
            Assert.True(json.TryGetProperty("face_zone", out _));
            Assert.Null(VideoSceneFacts.Build(Talking(0.3, 0.25, 0.7, 0.6, withHeads: false))[0].Head);
        }

        /// <summary>
        /// A custom face zone is used instead of the default, and horizontal formats have none unless set.
        /// </summary>
        [Fact]
        public void Zone_UsesFramingOrVerticalDefault() {
            VideoEdit edit = Talking(0.3, 0.25, 0.7, 0.6);
            Assert.Equal((0.16, 0.70), VideoFaceFraming.Zone(edit));
            edit.Framing = new VideoFraming { FaceTop = 0.1 };
            Assert.Equal((0.1, 0.70), VideoFaceFraming.Zone(edit));
            edit.Framing = null;
            edit.Format.Width = 1920;
            edit.Format.Height = 1080;
            Assert.Null(VideoFaceFraming.Zone(edit));
        }

        /// <summary>
        /// Malformed head boxes and an inverted face zone are errors; valid ones round-trip through JSON and the schema allows
        /// five numbers per head box.
        /// </summary>
        [Fact]
        public void Validate_HeadsAndFraming() {
            VideoEdit edit = Talking(0.3, 0.25, 0.7, 0.6);
            edit.Media[0].Analysis.Heads.Add([1, 0.5, 0.5, 0.4, 0.6]);
            edit.Framing = new VideoFraming { FaceTop = 0.8, FaceBottom = 0.2 };

            IReadOnlyList<VideoDiagnostic> errors = VideoEditValidator.Validate(edit, GraphicEditSamples.Catalog());

            Assert.Contains(errors, error => error.Code == "invalid_analysis_head");
            Assert.Contains(errors, error => error.Code == "invalid_framing");
            VideoEdit valid = Talking(0.3, 0.25, 0.7, 0.6);
            valid.Framing = new VideoFraming { FaceTop = 0.2, FaceBottom = 0.65 };
            string json = VideoEditJson.Serialize(valid);
            Assert.Contains("\"heads\"", json);
            Assert.Contains("\"face_top\"", json);
            VideoEdit parsed = VideoEditJson.Parse(json);
            Assert.Equal(valid.Media[0].Analysis.Heads.Count, parsed.Media[0].Analysis.Heads.Count);
            Assert.Equal(0.65, parsed.Framing.FaceBottom);
            Assert.DoesNotContain(VideoEditValidator.Validate(parsed, GraphicEditSamples.Catalog()), error => error.Severity == VideoDiagnosticSeverity.Error);
            string schema = VideoEditSchema.Create().GetRawText();
            Assert.Contains("\"maxItems\":5", schema.Replace(" ", "").Replace("\n", "").Replace("\r", ""));
        }

        /// <summary>
        /// Builds the sample talking-head edit: one 5 s take scene of a 1080 x 1920 speaker whose head box stays at the
        /// given normalized source box, captions at 0.78 and a graphic style at 0.4.
        /// </summary>
        static VideoEdit Talking(double x0, double y0, double x1, double y1, bool withHeads = true, VideoOverlay overlay = null) {
            VideoEdit edit = GraphicEditSamples.ContrastChain();
            edit.Scenes[0].Overlays.Clear();
            if (overlay != null) {
                edit.Scenes[0].Overlays.Add(overlay);
            }
            if (withHeads) {
                double[] head = Measured(x0, y0, x1, y1);
                edit.Media[0].Analysis.Heads = Enumerable.Range(0, 201).Select(index => new[] { index / 10.0, head[0], head[1], head[2], head[3] }).ToList();
            }
            return edit;
        }

        /// <summary>
        /// A 1 s opening scene without a take whose text, with <paramref name="hold"/> seconds of reading time, crosses into
        /// the take scene of a head in the middle of the frame.
        /// </summary>
        static VideoEdit HeldEdit(double? hold) {
            VideoEdit edit = Talking(0.3, 0.3, 0.7, 0.6);
            VideoOverlay text = Plain("held", "OI");
            text.At = new VideoMoment { Sec = 0.2 };
            text.HoldSec = hold;
            edit.Scenes.Insert(0, new VideoScene { Id = "opening", Duration = new VideoSceneDuration { Mode = "fixed", Sec = 1 }, Overlays = [text] });
            return edit;
        }

        /// <summary>
        /// A plain overlay in the graphic style (centered at 0.4 of the height, 96 px).
        /// </summary>
        static VideoOverlay Plain(string id, string text) {
            return new VideoOverlay { Id = id, Text = text, Style = "graphic", At = new VideoMoment { Sec = 0.5 } };
        }

        /// <summary>
        /// A zoom-to-focus punch-in on the frame center over the whole scene.
        /// </summary>
        static VideoMotion Zoom(double from, double to) {
            return new VideoMotion { Preset = "zoom_to_focus", FromScale = from, ToScale = to, Start = new VideoMoment { Sec = 0 }, Focus = new VideoFocus { Type = "center" }, Curve = "linear.v1" };
        }

        static VideoCompileResult Compile(VideoEdit edit) {
            return VideoEditCompiler.Compile(edit, GraphicEditSamples.Context(new FixedAdvanceTextMeasurer()));
        }

        static VisualLayer Layer(VideoCompileResult result, string id) {
            return result.Composition.Layers.Single(layer => layer.Id == id);
        }

        /// <summary>
        /// Union of a scene's head boxes in output pixels.
        /// </summary>
        /// <summary>The head box the measuring tool reports for a face occupying the given box (inverse of FaceCore).</summary>
        static double[] Measured(double x0, double y0, double x1, double y1) {
            double width = (x1 - x0) / (1 - 2 * 0.12 / 1.3), height = (y1 - y0) / (1 - (0.35 + 0.09) / 1.52);
            double left = x0 - width * (0.12 / 1.3), top = y0 - height * (0.35 / 1.52);
            return [Math.Max(0, left), Math.Max(0, top), Math.Min(1, left + width), Math.Min(1, top + height)];
        }

        static VideoGraphicRectangle HeadBox(VideoCompileResult result, string scene = "contrast") {
            List<VideoHeadSample> track = result.Heads[scene];
            return new VideoGraphicRectangle(track.Min(sample => sample.Bounds.Left), track.Min(sample => sample.Bounds.Top), track.Max(sample => sample.Bounds.Right), track.Max(sample => sample.Bounds.Bottom));
        }

        static void AssertInsideZone(VideoCompileResult result) {
            Assert.All(result.Heads["contrast"], sample => {
                Assert.True(sample.Bounds.Top >= VideoFaceFraming.DefaultFaceTop * Height - 1, $"head top {sample.Bounds.Top} at {sample.Time.ToSeconds()}");
                Assert.True(sample.Bounds.Bottom <= VideoFaceFraming.DefaultFaceBottom * Height + 1, $"head bottom {sample.Bounds.Bottom} at {sample.Time.ToSeconds()}");
            });
        }

        static string Describe(VideoCompileResult result) {
            return string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Code + " " + diagnostic.Path + ": " + diagnostic.Message));
        }
    }
}
