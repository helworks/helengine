using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Derives caption cues from the words spoken in each scene take, grouped by the caption track settings, with timed
    /// words for spoken-word highlighting. Before a take has timed words, previews estimate cues from the planned speech;
    /// final renders report <c>caption_alignment</c> instead.
    /// </summary>
    public static class VideoCaptionBuilder {
        /// <summary>
        /// Draw order of captions inside their scene group.
        /// </summary>
        public const int CaptionOrder = 20;

        /// <summary>
        /// Adds caption layers for every scene.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        public static void Build(VideoCompileState state) {
            VideoCaptionTrack captions = state.Edit.Tracks.Captions;
            if (captions == null) {
                return;
            }
            foreach (VideoSceneSpan span in state.Spans) {
                List<List<CompositionTextWord>> cues = TimedCues(state, span, captions);
                bool timed = cues != null;
                if (!timed) {
                    if (string.IsNullOrWhiteSpace(span.Scene.Speech)) {
                        continue;
                    }
                    if (state.Context.Final) {
                        state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Pending, "caption_alignment", span.Scene.Id, $"scenes[{span.Index}].take", "Captions need the timed words of a recorded take."));
                        continue;
                    }
                    cues = EstimatedCues(span, captions);
                }
                for (int index = 0; index < cues.Count; index++) {
                    MediaTime start = cues[index][0].Start;
                    MediaTime end = index + 1 < cues.Count ? cues[index + 1][0].Start : span.End;
                    VideoCaptionOverride replacement = captions.Overrides.FirstOrDefault(item => item.Scene == span.Scene.Id && item.Cue == index);
                    string text = replacement?.Text ?? string.Join(" ", cues[index].Select(word => word.Text));
                    VisualLayer layer = new VisualLayer {
                        Id = "caption-" + span.Scene.Id + "-" + index, Kind = "text", MediaId = "", Order = CaptionOrder, Start = start, End = end,
                        Text = new CompositionText { Cues = [new CompositionTextCue { Text = text, Start = start, End = end, Words = timed && replacement == null ? cues[index] : [] }], Style = VideoSceneCompiler.TextStyle(state, captions.Style, "tracks.captions.style") },
                        Effects = captions.Effects.Select(VideoSceneCompiler.Effect).ToList()
                    };
                    VideoAnimationBuilder.AddLift(layer, captions);
                    state.AddMember(span, layer);
                }
            }
        }

        /// <summary>
        /// Groups the take words spoken inside the scene into cues, with global word times.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene span.</param>
        /// <param name="captions">Caption settings.</param>
        /// <returns>Cues of timed words, or null when the take has no timed words.</returns>
        static List<List<CompositionTextWord>> TimedCues(VideoCompileState state, VideoSceneSpan span, VideoCaptionTrack captions) {
            VideoTake take = span.Scene.Take;
            if (take == null || !state.Media.TryGetValue(take.Media, out VideoMedia media) || media.Analysis == null) {
                return null;
            }
            List<CompositionTextWord> words = media.Analysis.Words
                .Where(word => word.StartSec >= take.InSec && word.StartSec < take.OutSec)
                .Select(word => new CompositionTextWord {
                    Text = word.Text,
                    Start = span.Start + MediaTime.FromSeconds(word.StartSec - take.InSec),
                    End = span.Start + MediaTime.FromSeconds(Math.Min(word.EndSec, take.OutSec) - take.InSec)
                }).ToList();
            return words.Count == 0 ? null : words.Chunk(captions.WordsPerCue).Select(chunk => chunk.ToList()).ToList();
        }

        /// <summary>
        /// Spreads the planned speech over the scene in proportion to the length of each cue's text.
        /// </summary>
        /// <param name="span">Scene span.</param>
        /// <param name="captions">Caption settings.</param>
        /// <returns>Estimated cues, each holding one pseudo-word with the cue text and start; never used as word timings.</returns>
        static List<List<CompositionTextWord>> EstimatedCues(VideoSceneSpan span, VideoCaptionTrack captions) {
            string[] words = span.Scene.Speech.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            List<string> texts = words.Chunk(captions.WordsPerCue).Select(chunk => string.Join(" ", chunk)).ToList();
            double total = texts.Sum(text => text.Length), cursor = 0, duration = span.Duration.ToSeconds();
            List<List<CompositionTextWord>> cues = new List<List<CompositionTextWord>>();
            foreach (string text in texts) {
                MediaTime start = span.Start + MediaTime.FromSeconds(cursor);
                cues.Add([new CompositionTextWord { Text = text, Start = start, End = start }]);
                cursor += duration * text.Length / total;
            }
            return cues;
        }
    }
}
