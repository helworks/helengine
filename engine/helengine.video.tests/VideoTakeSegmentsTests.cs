using helengine.media;

namespace helengine.video.tests {
    /// <summary>
    /// Verifies takes played faster or slower and takes made of several source segments (jump cuts inside one scene):
    /// scene length, captions, word anchors and facts mapped through the segment table, one composition layer and voice
    /// clip per segment with continuous motion and crossfaded joins, validation, JSON and the take lock.
    /// </summary>
    public class VideoTakeSegmentsTests {
        /// <summary>
        /// A long segment at a speed with a nanosecond-precision fraction is laid out without overflowing the exact time
        /// fraction, and each piece still never consumes more source than its segment holds.
        /// </summary>
        [Theory]
        [InlineData(0.666666667)]
        [InlineData(1.234567891)]
        [InlineData(1.333333333)]
        public void Pieces_IrregularSpeedDoesNotOverflow(double speed) {
            VideoTake take = new VideoTake { InSec = 3.5, OutSec = 40, Speed = speed, Segments = [new VideoTakeSegment { InSec = 3.5, OutSec = 15.5 }, new VideoTakeSegment { InSec = 16.25, OutSec = 40 }] };

            List<VideoTakePiece> pieces = VideoTakeTiming.Pieces(take);

            Assert.Equal(2, pieces.Count);
            foreach (VideoTakePiece piece in pieces) {
                Assert.True(piece.Duration.AtRate(speed) <= piece.SourceOut - piece.SourceIn);
                Assert.Equal((piece.SourceOut - piece.SourceIn).ToSeconds() / speed, piece.Duration.ToSeconds(), 6);
            }
        }

        /// <summary>
        /// The scene lasts the sum of the segment lengths divided by the speed.
        /// </summary>
        [Fact]
        public void Timeline_SceneLastsSegmentsOverSpeed() {
            IReadOnlyList<VideoSceneSpan> spans = VideoSceneTimeline.Build(Sample(), []);

            Assert.Equal(2.0, spans[1].Duration.ToSeconds(), 6);
            Assert.Equal(3.0, spans[1].Start.ToSeconds(), 6);
        }

        /// <summary>
        /// Captions keep only words spoken inside kept segments and place them through the segment table and the speed.
        /// </summary>
        [Fact]
        public void Captions_FollowSegmentsAndSpeed() {
            CompositionDocument composition = Compile(Sample()).Composition;

            List<CompositionTextWord> words = composition.Layers.Where(layer => layer.Id.StartsWith("caption-question-")).SelectMany(layer => layer.Text.Cues[0].Words).ToList();
            Assert.DoesNotContain(words, word => word.Text == "lei?");
            CompositionTextWord first = words.Single(word => word.Text == "Qual");
            Assert.Equal(3.08, first.Start.ToSeconds(), 6);
            Assert.Equal(3.24, first.End.ToSeconds(), 6);
            // Política starts 0.2 s into the second segment, which begins 0.4 s into the scene: 3 + 0.4 + 0.2 / 1.25.
            Assert.Equal(3.56, words.Single(word => word.Text == "Política,").Start.ToSeconds(), 6);
            // "a" ends exactly at the cut.
            Assert.Equal(3.4, words.Single(word => word.Text == "a").End.ToSeconds(), 6);
        }

        /// <summary>
        /// A word anchor resolves through the segment table; a word that only falls in a dropped gap is not found.
        /// </summary>
        [Fact]
        public void Moments_WordAnchorsFollowSegments() {
            VideoEdit edit = Sample();
            VideoSceneSpan span = VideoSceneTimeline.Build(edit, [])[1];
            List<VideoDiagnostic> diagnostics = [];

            MediaTime politics = VideoMomentResolver.Resolve(edit, span, new VideoMoment { Word = "política", Occurrence = 2 }, "at", diagnostics);
            MediaTime dropped = VideoMomentResolver.Resolve(edit, span, new VideoMoment { Word = "lei" }, "at", diagnostics);

            Assert.Equal(0.4 + 1.1 / 1.25, politics.ToSeconds(), 6);
            Assert.Equal(MediaTime.Zero, dropped);
            Assert.Contains(diagnostics, item => item.Code == "anchor_unresolved");
        }

        /// <summary>
        /// Facts report speed, segments with scene and source times, words and silences in scene time, voiced and look-away
        /// spans mapped through the cuts, and handles around the first and last segment.
        /// </summary>
        [Fact]
        public void Facts_ExposeSpeedSegmentsAndMappedSpans() {
            VideoEdit edit = Sample();
            VideoMediaAnalysis analysis = edit.Media.Single(media => media.Id == "take").Analysis;
            analysis.Voiced = [[12.35, 13.0], [13.3, 14.0]];
            analysis.LookAway = [[13.0, 13.6]];

            VideoSceneFact fact = VideoSceneFacts.Build(edit)[1];

            Assert.Equal(1.25, fact.Speed);
            Assert.Equal(2, fact.Segments.Count);
            Assert.Equal(0.4, fact.Segments[1].StartSec, 6);
            Assert.Equal(2.0, fact.Segments[1].EndSec, 6);
            Assert.Equal(13.3, fact.Segments[1].SourceInSec, 6);
            Assert.DoesNotContain(fact.Words, word => word.Text == "lei?");
            Assert.Equal(0.56, fact.Words.Single(word => word.Text == "Política,").StartSec, 6);
            // The voiced spans touch across the cut and merge into one.
            double[] voiced = Assert.Single(fact.Voiced);
            Assert.Equal(0.04, voiced[0], 6);
            Assert.Equal(0.4 + 0.7 / 1.25, voiced[1], 6);
            double[] away = Assert.Single(fact.LookAway);
            Assert.Equal(0.4, away[0], 6);
            Assert.Equal(0.4 + 0.3 / 1.25, away[1], 6);
            Assert.Equal(12.3, fact.HandleBeforeSec, 6);
            Assert.Equal(4.7, fact.HandleAfterSec, 6);
        }

        /// <summary>
        /// Each segment becomes its own take layer at the take speed, spanning its slice of the scene, and the engine
        /// validator accepts the composition.
        /// </summary>
        [Fact]
        public void Compile_OneTakeLayerPerSegment() {
            VideoCompileResult result = Compile(Sample());

            Assert.False(result.HasErrors, Describe(result));
            VisualLayer first = result.Composition.Layers.Single(layer => layer.Id == "question-take");
            VisualLayer second = result.Composition.Layers.Single(layer => layer.Id == "question-take-segment-1");
            Assert.Equal(1.25, first.Rate);
            Assert.Equal(1.25, second.Rate);
            Assert.Equal(3.0, first.Start.ToSeconds(), 6);
            Assert.Equal(first.End, second.Start);
            Assert.Equal(3.4, second.Start.ToSeconds(), 6);
            Assert.Equal(12.3, first.SourceIn.ToSeconds(), 6);
            Assert.Equal(12.8, first.SourceOut.ToSeconds(), 6);
            Assert.Equal(13.3, second.SourceIn.ToSeconds(), 6);
            Assert.Contains(second.Id, result.Composition.Layers.Single(layer => layer.Id == "scene-question").Members);
            Assert.Empty(CompositionValidator.Validate(result.Composition, VideoEditValidatorTests.Catalog()));
            // The last source frame of the first segment plays just before the cut, the first of the second right after.
            LayerEvaluation after = CompositionEvaluator.Evaluate(result.Composition, MediaTime.FromSeconds(3.5)).Single(item => item.Layer.Id.StartsWith("question-take"));
            Assert.Equal(13.3 + 0.1 * 1.25, after.SourceTime.ToSeconds(), 6);
        }

        /// <summary>
        /// A scene-long zoom keeps animating continuously across the cut instead of restarting in every segment.
        /// </summary>
        [Fact]
        public void Compile_MotionIsContinuousAcrossCuts() {
            VideoEdit edit = Sample();
            edit.Scenes[1].Layers = [new VideoLayer { Id = "take", Kind = "take", Fit = "cover", Motion = new VideoMotion { Preset = "zoom_to_focus", FromScale = 1, ToScale = 1.5, Curve = "smoothstep.v1" } }];
            CompositionDocument composition = Compile(edit).Composition;

            double Zoom(double seconds) {
                return CompositionEvaluator.Evaluate(composition, MediaTime.FromSeconds(seconds)).Single(item => item.Layer.Id.StartsWith("question-take")).Transform.Zoom;
            }
            double Expected(double local) {
                return 1 + 0.5 * MediaCurve.Evaluate("smoothstep.v1", local / 2.0);
            }

            Assert.Equal(Expected(0.2), Zoom(3.2), 3);
            Assert.Equal(Expected(0.4), Zoom(3.4), 3);
            Assert.Equal(Expected(1.3), Zoom(4.3), 3);
            Assert.True(Math.Abs(Zoom(3.4) - Zoom(3.399)) < 0.002);
            Assert.Empty(CompositionValidator.Validate(composition, VideoEditValidatorTests.Catalog()));
        }

        /// <summary>
        /// A blurred background is cut into the same segments as the speaker, and the matte follows each piece.
        /// </summary>
        [Fact]
        public void Compile_BlurredBackgroundFollowsSegments() {
            VideoEdit edit = Sample();
            edit.Media.Add(new VideoMedia { Id = "take-matte", Kind = "video", Path = "media/take-matte.mp4", Sha256 = new string('e', 64), Width = 270, Height = 480, DurationSec = 20 });
            edit.Media.Single(media => media.Id == "take").Matte = "take-matte";
            edit.Scenes[1].Background = new VideoSceneBackground { Mode = "blur" };

            MediaCapabilities catalog = VideoEditValidatorTests.Catalog();
            catalog.Effects.Add(new MediaEffectDescriptor {
                Id = "gaussian-blur", Version = 1, Category = "layer", MainInputRole = "Source", InputRoles = ["Source"],
                Parameters = new(StringComparer.Ordinal) { ["Radius"] = new MediaParameterDescriptor { Type = "number", Minimum = 0, Maximum = 96 } }
            });
            VideoCompileResult result = VideoEditCompiler.Compile(edit, new VideoCompileContext { Capabilities = catalog });

            Assert.False(result.HasErrors, Describe(result));
            CompositionDocument composition = result.Composition;
            VisualLayer back = composition.Layers.Single(layer => layer.Id == "question-take-background-segment-1");
            VisualLayer front = composition.Layers.Single(layer => layer.Id == "question-take-segment-1");
            Assert.Equal(front.Start, back.Start);
            Assert.Equal(front.SourceIn, back.SourceIn);
            Assert.Equal(1.25, back.Rate);
            Assert.Equal("take-matte", front.Mask.MediaId);
            Assert.Empty(CompositionValidator.Validate(composition, catalog));
        }

        /// <summary>
        /// The voice becomes one clip per segment at the take speed, overlapping by 10 ms at the cut with equal-power fades,
        /// and a scene envelope crossing the cut is split between the clips.
        /// </summary>
        [Fact]
        public void Compile_VoiceClipsCrossfadeAtCuts() {
            VideoEdit edit = Sample();
            edit.Scenes[1].Voice = new VideoVoice { Envelopes = [new VideoEnvelope { Type = "linear", At = new VideoMoment { Sec = 0.2 }, DurationSec = 0.4, From = 1, To = 0.5 }] };

            VideoCompileResult result = Compile(edit);

            Assert.False(result.HasErrors, Describe(result));
            AudioClip first = result.Composition.AudioClips.Single(clip => clip.Id == "voice-question");
            AudioClip second = result.Composition.AudioClips.Single(clip => clip.Id == "voice-question-segment-1");
            Assert.Equal(1.25, first.Rate);
            Assert.Equal(3.405, first.End.ToSeconds(), 5);
            Assert.Equal(3.395, second.Start.ToSeconds(), 5);
            Assert.Contains(first.Envelopes, envelope => envelope.Type == "equal_power_out");
            Assert.Contains(second.Envelopes, envelope => envelope.Type == "equal_power_in");
            AudioEnvelope head = first.Envelopes.Single(envelope => envelope.Type == "linear");
            Assert.Equal(1, head.From, 6);
            Assert.Equal(1 - 0.5 * (0.405 - 0.2) / 0.4, head.To, 4);
            AudioEnvelope tail = second.Envelopes.Single(envelope => envelope.Type == "linear");
            Assert.Equal(0.5, tail.To, 6);
            Assert.Empty(CompositionValidator.Validate(result.Composition, VideoEditValidatorTests.Catalog()));
        }

        /// <summary>
        /// A real-time take without segments still compiles to a single layer and clip, written without a rate.
        /// </summary>
        [Fact]
        public void Compile_PlainTakeIsUnchanged() {
            VideoEdit edit = VideoEditSamples.TwoScenes();
            edit.Media.Single(media => media.Id == "take").HasAudio = true;

            CompositionDocument composition = Compile(edit).Composition;

            Assert.DoesNotContain(composition.Layers, layer => layer.Id.Contains("-segment-"));
            Assert.Equal(1, Assert.Single(composition.AudioClips, clip => clip.Id.StartsWith("voice-")).Rate);
            Assert.DoesNotContain("\"rate\":", CompositionJson.Serialize(composition));
        }

        /// <summary>
        /// Speed, segment length, order and envelope problems each get their own diagnostic.
        /// </summary>
        [Fact]
        public void Validate_ReportsSpeedAndSegmentErrors() {
            VideoEdit edit = Sample();
            edit.Scenes[1].Take.Speed = 2.5;
            Assert.Contains(VideoEditValidator.Validate(edit, VideoEditValidatorTests.Catalog()), item => item.Code == "invalid_take_speed" && item.Path == "scenes[1].take.speed");

            edit = Sample();
            edit.Scenes[1].Take.Segments[0].OutSec = 12.35;
            Assert.Contains(VideoEditValidator.Validate(edit, VideoEditValidatorTests.Catalog()), item => item.Code == "take_segment_too_short" && item.Path == "scenes[1].take.segments[0]");

            edit = Sample();
            edit.Scenes[1].Take.Segments[1].InSec = 12.6;
            Assert.Contains(VideoEditValidator.Validate(edit, VideoEditValidatorTests.Catalog()), item => item.Code == "take_segment_order");

            edit = Sample();
            edit.Scenes[1].Take.OutSec = 15.0;
            Assert.Contains(VideoEditValidator.Validate(edit, VideoEditValidatorTests.Catalog()), item => item.Code == "take_segment_envelope");

            edit = Sample();
            edit.Media.Single(media => media.Id == "take").Analysis.Voiced = [[2, 1]];
            Assert.Contains(VideoEditValidator.Validate(edit, VideoEditValidatorTests.Catalog()), item => item.Code == "invalid_analysis_span" && item.Path == "media[1].analysis.voiced[0]");

            Assert.DoesNotContain(VideoEditValidator.Validate(Sample(), VideoEditValidatorTests.Catalog()), item => item.Severity == VideoDiagnosticSeverity.Error);
        }

        /// <summary>
        /// Speed, segments and analysis spans use snake_case JSON and round-trip.
        /// </summary>
        [Fact]
        public void Json_RoundTripsSpeedSegmentsAndSpans() {
            VideoEdit edit = Sample();
            edit.Media.Single(media => media.Id == "take").Analysis.LookAway = [[13.0, 13.6]];

            string json = VideoEditJson.Serialize(edit);
            VideoEdit parsed = VideoEditJson.Parse(json);

            Assert.Contains("\"speed\": 1.25", json);
            Assert.Contains("\"segments\": [", json);
            Assert.Contains("\"look_away\": [", json);
            Assert.Equal(13.3, parsed.Scenes[1].Take.Segments[1].InSec);
            Assert.Equal(13.6, parsed.Media.Single(media => media.Id == "take").Analysis.LookAway[0][1]);
            System.Text.Json.JsonElement speed = VideoEditSchema.Create().GetProperty("properties").GetProperty("scenes").GetProperty("items").GetProperty("properties").GetProperty("take").GetProperty("properties").GetProperty("speed");
            Assert.Equal(2, speed.GetProperty("maximum").GetDouble());
        }

        /// <summary>
        /// A human-locked take keeps its speed and segments through a replan that proposes a plain take.
        /// </summary>
        [Fact]
        public void Merge_LockedTakeKeepsSpeedAndSegments() {
            VideoEdit current = Sample();
            current.Scenes[1].Take.By = "human";
            VideoEdit proposal = VideoEditSamples.TwoScenes();

            VideoEdit merged = VideoEditMerge.Replan(current, proposal);

            Assert.Equal(1.25, merged.Scenes[1].Take.Speed);
            Assert.Equal(2, merged.Scenes[1].Take.Segments.Count);
        }

        /// <summary>
        /// The sample with its question take cut into two segments that drop "lei?", played at 1.25x with audio.
        /// </summary>
        /// <returns>Edit.</returns>
        static VideoEdit Sample() {
            VideoEdit edit = VideoEditSamples.TwoScenes();
            edit.Media.Single(media => media.Id == "take").HasAudio = true;
            edit.Scenes[1].Overlays.Clear();
            edit.Scenes[1].Take = new VideoTake {
                Media = "take", InSec = 12.3, OutSec = 15.3, Speed = 1.25,
                Segments = [new VideoTakeSegment { InSec = 12.3, OutSec = 12.8 }, new VideoTakeSegment { InSec = 13.3, OutSec = 15.3 }]
            };
            return edit;
        }

        /// <summary>
        /// Compiles with the validator catalog.
        /// </summary>
        /// <param name="edit">Edit.</param>
        /// <returns>Compilation result.</returns>
        static VideoCompileResult Compile(VideoEdit edit) {
            return VideoEditCompiler.Compile(edit, new VideoCompileContext { Capabilities = VideoEditValidatorTests.Catalog() });
        }

        /// <summary>
        /// Lists the diagnostics of a result.
        /// </summary>
        /// <param name="result">Compilation result.</param>
        /// <returns>One line per diagnostic.</returns>
        static string Describe(VideoCompileResult result) {
            return string.Join("; ", result.Diagnostics.Select(item => item.Code + " " + item.Path + " " + item.Message));
        }
    }
}
