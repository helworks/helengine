namespace helengine.video {
    /// <summary>
    /// Derives per-scene facts from an edit: resolved durations, take-relative words and silences, take handles, speed and
    /// segments, and the voiced and look-away spans of the take, all mapped through the segment table and the take speed, and
    /// where the speaker's head is on screen after the face framing (head box, face zone and the bands free for text).
    /// </summary>
    public static class VideoSceneFacts {
        /// <summary>
        /// Smallest gap, in seconds, reported as a silence.
        /// </summary>
        public const double MinSilenceSec = 0.25;

        /// <summary>
        /// Builds the facts of every scene in playback order.
        /// </summary>
        /// <param name="edit">Edit document.</param>
        /// <returns>One fact per scene.</returns>
        public static List<VideoSceneFact> Build(VideoEdit edit) {
            if (edit == null) {
                throw new ArgumentNullException(nameof(edit));
            }
            IReadOnlyList<VideoSceneSpan> spans = VideoSceneTimeline.Build(edit, new List<VideoDiagnostic>());
            List<VideoSceneFact> facts = [];
            VideoCompileState framing = null;
            for (int index = 0; index < spans.Count; index++) {
                VideoScene scene = spans[index].Scene;
                VideoMedia media = scene.Take == null ? null : edit.Media.FirstOrDefault(item => item.Id == scene.Take.Media);
                double duration = spans[index].Duration.ToSeconds();
                VideoSceneFact fact = new VideoSceneFact {
                    SceneId = scene.Id,
                    Section = scene.Section,
                    SectionChanges = index > 0 && !string.IsNullOrEmpty(scene.Section) && scene.Section != spans[index - 1].Scene.Section,
                    DurationSec = duration,
                    HasTake = scene.Take != null,
                    Visual = ResolveVisual(edit, scene)
                };
                if (scene.Take != null) {
                    List<VideoTakePiece> pieces = VideoTakeTiming.Pieces(scene.Take);
                    IReadOnlyList<VideoTakeSegment> segments = VideoTakeTiming.Segments(scene.Take);
                    fact.HandleBeforeSec = Math.Max(0, segments[0].InSec);
                    fact.HandleAfterSec = media == null ? 0 : Math.Max(0, media.DurationSec - segments[^1].OutSec);
                    fact.Speed = VideoTakeTiming.Speed(scene.Take);
                    fact.Segments = pieces.Select(piece => new VideoFactSegment { StartSec = piece.Start.ToSeconds(), EndSec = piece.End.ToSeconds(), SourceInSec = piece.Segment.InSec, SourceOutSec = piece.Segment.OutSec }).ToList();
                    fact.Words = CollectWords(pieces, media);
                    fact.Silences = CollectSilences(fact.Words, duration);
                    fact.Voiced = VideoTakeTiming.MapSpans(pieces, media?.Analysis?.Voiced);
                    fact.LookAway = VideoTakeTiming.MapSpans(pieces, media?.Analysis?.LookAway);
                    if (media?.Analysis?.Heads is { Count: > 0 }) {
                        framing ??= FramingState(edit, spans);
                        Head(framing, spans[index], fact);
                    }
                }
                facts.Add(fact);
            }
            return facts;
        }

        /// <summary>
        /// Creates the compilation state the head framing of the facts runs in: the edit's format and frame rate, no catalog.
        /// </summary>
        /// <param name="edit">Edit.</param>
        /// <param name="spans">Scene spans.</param>
        /// <returns>State whose diagnostics are discarded.</returns>
        static VideoCompileState FramingState(VideoEdit edit, IReadOnlyList<VideoSceneSpan> spans) {
            helengine.media.CompositionDocument document = new helengine.media.CompositionDocument {
                Width = edit.Format.Width, Height = edit.Format.Height,
                FrameRate = new helengine.media.MediaTime(edit.Format.FrameRate.Numerator, edit.Format.FrameRate.Denominator)
            };
            return new VideoCompileState(edit, new VideoCompileContext(), document, spans, new List<VideoDiagnostic>());
        }

        /// <summary>
        /// Compiles the scene's take layer, frames it as the compiler does (see <see cref="VideoFaceFraming"/>) and reports the
        /// head's box over the scene, the face zone and the bands free for text, as fractions of the frame.
        /// </summary>
        /// <param name="state">Framing state.</param>
        /// <param name="span">Scene span.</param>
        /// <param name="fact">Fact completed in place.</param>
        static void Head(VideoCompileState state, VideoSceneSpan span, VideoSceneFact fact) {
            VideoLayer source = VideoFaceFraming.TakeLayer(span.Scene);
            if (source == null || state.Edit.Format.Width <= 0 || state.Edit.Format.Height <= 0) {
                return;
            }
            string path = $"scenes[{span.Index}]";
            try {
                VideoFaceFraming.Guard(state, span, VideoSceneCompiler.Layer(state, span, source, path), path, true);
            } catch (Exception error) when (error is InvalidDataException or KeyNotFoundException or ArgumentException or InvalidOperationException) {
                // An edit the validator would reject has no reliable framing; the facts leave the head out.
                return;
            }
            VideoGraphicRectangle head = VideoFaceFraming.Union(state, span.Start, span.End);
            if (head == null) {
                return;
            }
            double width = state.Edit.Format.Width, height = state.Edit.Format.Height;
            fact.Head = new VideoFactBox { Left = Round(head.Left / width), Top = Round(head.Top / height), Right = Round(head.Right / width), Bottom = Round(head.Bottom / height) };
            if (VideoFaceFraming.Zone(state.Edit) is { } zone) {
                fact.FaceZone = new VideoFactBand { Top = zone.Top, Bottom = zone.Bottom };
            }
            fact.FreeAreas = VideoFaceClearance.Bands(state.Edit, head.Inflate(VideoCaptionBand.Gap * height)).Where(band => band.Bottom - band.Top >= VideoFaceClearance.MinimumBand * height)
                .Select(band => new VideoFactBand { Top = Round(band.Top / height), Bottom = Round(band.Bottom / height) }).ToList();
        }

        /// <summary>
        /// Rounds a frame fraction for planners.
        /// </summary>
        /// <param name="value">Fraction.</param>
        /// <returns>Value rounded to three decimals.</returns>
        static double Round(double value) {
            return Math.Round(value, 3);
        }

        /// <summary>
        /// Picks the dominant visual of a scene: an image layer wins, then a take, otherwise text.
        /// </summary>
        /// <param name="edit">Edit document that owns the media.</param>
        /// <param name="scene">Scene.</param>
        /// <returns>image, take or text.</returns>
        static string ResolveVisual(VideoEdit edit, VideoScene scene) {
            bool image = scene.Layers.Any(layer => layer.Kind == "media" && edit.Media.Any(item => item.Id == layer.Media && item.Kind == "image"));
            if (image) {
                return "image";
            }
            return scene.Take != null ? "take" : "text";
        }

        /// <summary>
        /// Converts the analysed words overlapping a kept segment to scene-relative seconds, clamped to that segment. A word
        /// is reported once, in the segment where it starts or else the first segment it reaches; words that only fall in
        /// the gaps between segments are not played and are left out.
        /// </summary>
        /// <param name="pieces">Take segments placed in scene time.</param>
        /// <param name="media">Take media; may be null or unanalysed.</param>
        /// <returns>Words in scene time.</returns>
        static List<VideoFactWord> CollectWords(IReadOnlyList<VideoTakePiece> pieces, VideoMedia media) {
            List<VideoFactWord> words = [];
            if (media?.Analysis == null) {
                return words;
            }
            foreach (VideoWord word in media.Analysis.Words) {
                VideoTakePiece piece = VideoTakeTiming.PieceAt(pieces, word.StartSec) ?? pieces.FirstOrDefault(item => Math.Min(word.EndSec, item.Segment.OutSec) > Math.Max(word.StartSec, item.Segment.InSec));
                if (piece == null) {
                    continue;
                }
                double start = piece.SceneSec(Math.Max(word.StartSec, piece.Segment.InSec));
                double end = piece.SceneSec(Math.Min(word.EndSec, piece.Segment.OutSec));
                if (end > start) {
                    words.Add(new VideoFactWord { Text = word.Text, StartSec = start, EndSec = end });
                }
            }
            return words;
        }

        /// <summary>
        /// Finds the leading, inner and trailing gaps of at least <see cref="MinSilenceSec"/> around the words.
        /// </summary>
        /// <param name="words">Scene-relative words in order.</param>
        /// <param name="duration">Scene length in seconds.</param>
        /// <returns>Silences in order; empty when there are no words.</returns>
        static List<VideoFactSilence> CollectSilences(List<VideoFactWord> words, double duration) {
            List<VideoFactSilence> silences = [];
            if (words.Count == 0) {
                return silences;
            }
            double cursor = 0;
            foreach (VideoFactWord word in words) {
                if (word.StartSec - cursor >= MinSilenceSec) {
                    silences.Add(new VideoFactSilence { StartSec = cursor, EndSec = word.StartSec });
                }
                cursor = Math.Max(cursor, word.EndSec);
            }
            if (duration - cursor >= MinSilenceSec) {
                silences.Add(new VideoFactSilence { StartSec = cursor, EndSec = duration });
            }
            return silences;
        }
    }
}
