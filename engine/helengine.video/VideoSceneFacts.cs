namespace helengine.video {
    /// <summary>
    /// Derives per-scene facts from an edit: resolved durations, take-relative words and silences, and take handles.
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
                    fact.HandleBeforeSec = Math.Max(0, scene.Take.InSec);
                    fact.HandleAfterSec = media == null ? 0 : Math.Max(0, media.DurationSec - scene.Take.OutSec);
                    fact.Words = CollectWords(scene.Take, media);
                    fact.Silences = CollectSilences(fact.Words, duration);
                }
                facts.Add(fact);
            }
            return facts;
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
        /// Converts the analysed words overlapping the take interval to scene-relative seconds, clamped to the interval.
        /// </summary>
        /// <param name="take">Scene take.</param>
        /// <param name="media">Take media; may be null or unanalysed.</param>
        /// <returns>Words in scene time.</returns>
        static List<VideoFactWord> CollectWords(VideoTake take, VideoMedia media) {
            List<VideoFactWord> words = [];
            if (media?.Analysis == null) {
                return words;
            }
            foreach (VideoWord word in media.Analysis.Words) {
                double start = Math.Max(word.StartSec, take.InSec);
                double end = Math.Min(word.EndSec, take.OutSec);
                if (end > start) {
                    words.Add(new VideoFactWord { Text = word.Text, StartSec = start - take.InSec, EndSec = end - take.InSec });
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
