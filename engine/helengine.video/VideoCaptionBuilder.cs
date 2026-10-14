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
                    cues = EstimatedCues(state, span, captions);
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
        /// Groups the take words spoken inside the scene into cues, with global word times. Only words starting inside a
        /// kept segment are shown; each is placed through the segment table and the take speed, and ends no later than its
        /// segment.
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
            List<VideoTakePiece> pieces = VideoTakeTiming.Pieces(take);
            List<CompositionTextWord> words = new List<CompositionTextWord>();
            foreach (VideoWord word in media.Analysis.Words) {
                VideoTakePiece piece = VideoTakeTiming.PieceAt(pieces, word.StartSec);
                if (piece != null) {
                    words.Add(new CompositionTextWord { Text = word.Text, Start = span.Start + piece.SceneTime(word.StartSec), End = span.Start + piece.SceneTime(Math.Min(word.EndSec, piece.Segment.OutSec)) });
                }
            }
            return words.Count == 0 ? null : SplitCues(state, span, captions, words, word => word.Text);
        }

        /// <summary>
        /// Spreads the planned speech over the scene in proportion to the length of each cue's text.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene span.</param>
        /// <param name="captions">Caption settings.</param>
        /// <returns>Estimated cues, each holding one pseudo-word with the cue text and start; never used as word timings.</returns>
        static List<List<CompositionTextWord>> EstimatedCues(VideoCompileState state, VideoSceneSpan span, VideoCaptionTrack captions) {
            string[] words = span.Scene.Speech.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            List<string> texts = SplitCues(state, span, captions, words, word => word).Select(chunk => string.Join(" ", chunk)).ToList();
            double total = texts.Sum(text => text.Length), cursor = 0, duration = span.Duration.ToSeconds();
            List<List<CompositionTextWord>> cues = new List<List<CompositionTextWord>>();
            foreach (string text in texts) {
                MediaTime start = span.Start + MediaTime.FromSeconds(cursor);
                cues.Add([new CompositionTextWord { Text = text, Start = start, End = start }]);
                cursor += duration * text.Length / total;
            }
            return cues;
        }

        /// <summary>
        /// Splits the scene words into cues: at the chosen breaks of the scene when every break phrase is found in order,
        /// otherwise automatically, with a warning naming the phrase that no longer matches the speech.
        /// </summary>
        /// <typeparam name="T">Word type.</typeparam>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene span.</param>
        /// <param name="captions">Caption settings.</param>
        /// <param name="words">Words in spoken order.</param>
        /// <param name="text">Text of a word.</param>
        /// <returns>Cues covering every word once, in order.</returns>
        static List<List<T>> SplitCues<T>(VideoCompileState state, VideoSceneSpan span, VideoCaptionTrack captions, IReadOnlyList<T> words, Func<T, string> text) {
            int breaksIndex = captions.Breaks.FindIndex(item => item.Scene == span.Scene.Id);
            if (breaksIndex >= 0) {
                List<List<T>> chosen = ChosenCues(words, text, captions.Breaks[breaksIndex].Starts, out string missing);
                if (chosen != null) {
                    return chosen;
                }
                state.Diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Warning, "caption_break_unmatched", span.Scene.Id, $"tracks.captions.breaks[{breaksIndex}]", $"The caption break \"{missing}\" is not in the scene speech; captions use the automatic split."));
            }
            return BalancedCues(words, text, captions.WordsPerCue);
        }

        /// <summary>
        /// Cuts the words before each break phrase, searching in spoken order after the previous break.
        /// </summary>
        /// <typeparam name="T">Word type.</typeparam>
        /// <param name="words">Words in spoken order.</param>
        /// <param name="text">Text of a word.</param>
        /// <param name="starts">Opening phrase of every cue after the first.</param>
        /// <param name="missing">The first phrase not found, when the result is null.</param>
        /// <returns>Cues, or null when a phrase is not found.</returns>
        internal static List<List<T>> ChosenCues<T>(IReadOnlyList<T> words, Func<T, string> text, IReadOnlyList<string> starts, out string missing) {
            string[] normalized = words.Select(word => VideoMomentResolver.Normalize(text(word))).ToArray();
            List<int> cuts = [0];
            missing = null;
            foreach (string start in starts) {
                string[] phrase = (start ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Select(VideoMomentResolver.Normalize).Where(word => word.Length > 0).ToArray();
                int found = -1;
                for (int index = cuts[^1] + 1; phrase.Length > 0 && index + phrase.Length <= normalized.Length; index++) {
                    if (phrase.Select((word, offset) => normalized[index + offset] == word).All(match => match)) {
                        found = index;
                        break;
                    }
                }
                if (found < 0) {
                    missing = start;
                    return null;
                }
                cuts.Add(found);
            }
            cuts.Add(words.Count);
            return cuts.Zip(cuts.Skip(1), (from, to) => words.Skip(from).Take(to - from).ToList()).ToList();
        }

        /// <summary>
        /// Splits the words into cues of at most <paramref name="max"/> words, preferring to end cues where a sentence or
        /// clause ends and avoiding one- or two-word leftovers, so a punchline after a comma gets its own cue.
        /// </summary>
        /// <typeparam name="T">Word type.</typeparam>
        /// <param name="words">Words in spoken order.</param>
        /// <param name="text">Text of a word, punctuation included.</param>
        /// <param name="max">Maximum words per cue.</param>
        /// <returns>Cues covering every word once, in order.</returns>
        internal static List<List<T>> BalancedCues<T>(IReadOnlyList<T> words, Func<T, string> text, int max) {
            int count = words.Count;
            max = Math.Max(1, max);
            double[] best = new double[count + 1];
            int[] from = new int[count + 1];
            for (int end = 1; end <= count; end++) {
                best[end] = double.MaxValue;
                for (int start = Math.Max(0, end - max); start < end; start++) {
                    int length = end - start;
                    // Fewer cues first, then clean breaks, then no tiny cues, then even lengths.
                    double cost = best[start] + 10 + BreakCost(text(words[end - 1]), end == count) + 0.01 * length * length;
                    if (length < 3 && !(start == 0 && end == count)) {
                        cost += (3 - length) * 6;
                    }
                    // A cue that finishes one sentence and starts the next reads badly, worst with a dangling word or two.
                    for (int index = start; index < end - 1; index++) {
                        if (BreakCost(text(words[index]), false) == 0) {
                            cost += end - 1 - index <= 2 ? 12 : 4;
                        }
                    }
                    if (cost < best[end]) {
                        best[end] = cost;
                        from[end] = start;
                    }
                }
            }
            List<List<T>> cues = new List<List<T>>();
            for (int end = count; end > 0; end = from[end]) {
                cues.Insert(0, words.Skip(from[end]).Take(end - from[end]).ToList());
            }
            return cues;
        }

        /// <summary>
        /// Cost of ending a cue after a word: free after a sentence end, cheap after a clause mark, expensive mid-phrase.
        /// </summary>
        /// <param name="word">Word text.</param>
        /// <param name="last">Whether the word ends the scene.</param>
        /// <returns>Break cost.</returns>
        static double BreakCost(string word, bool last) {
            string trimmed = word.TrimEnd('"', '\'', '”', '’', ')', ']');
            if (last || trimmed.EndsWith('.') || trimmed.EndsWith('?') || trimmed.EndsWith('!') || trimmed.EndsWith('…')) {
                return 0;
            }
            return trimmed.EndsWith(',') || trimmed.EndsWith(';') || trimmed.EndsWith(':') || trimmed.EndsWith('—') ? 2 : 8;
        }
    }
}
