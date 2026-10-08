using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Lays the scenes end to end on the global timeline. Each scene length comes from its duration source (the take
    /// interval, a fixed length or an estimate); entries overlap the previous scene instead of moving scene starts, so the
    /// video length is always the sum of the scene lengths.
    /// </summary>
    public static class VideoSceneTimeline {
        /// <summary>
        /// Length used when a scene has no usable duration, so later scenes still get a position.
        /// </summary>
        static readonly MediaTime FallbackDuration = new MediaTime(1, 1);

        /// <summary>
        /// Resolves the span of every scene.
        /// </summary>
        /// <param name="edit">Edit document.</param>
        /// <param name="diagnostics">Receives errors for unusable durations.</param>
        /// <returns>Scene spans in playback order.</returns>
        public static IReadOnlyList<VideoSceneSpan> Build(VideoEdit edit, List<VideoDiagnostic> diagnostics) {
            List<VideoSceneSpan> spans = new List<VideoSceneSpan>();
            MediaTime cursor = MediaTime.Zero;
            for (int index = 0; index < edit.Scenes.Count; index++) {
                VideoScene scene = edit.Scenes[index];
                MediaTime duration = Length(scene, index, diagnostics);
                spans.Add(new VideoSceneSpan { Scene = scene, Index = index, Start = cursor, End = cursor + duration, Estimated = scene.Duration?.Mode == "estimate" });
                cursor += duration;
            }
            return spans;
        }

        /// <summary>
        /// Resolves the length of one scene from its duration source.
        /// </summary>
        /// <param name="scene">Scene.</param>
        /// <param name="index">Scene index, used in diagnostic paths.</param>
        /// <param name="diagnostics">Receives an error when the duration is unusable.</param>
        /// <returns>Positive scene length.</returns>
        static MediaTime Length(VideoScene scene, int index, List<VideoDiagnostic> diagnostics) {
            string path = $"scenes[{index}].duration";
            double seconds;
            if (scene.Duration?.Mode == "from_take") {
                seconds = scene.Take == null ? 0 : scene.Take.OutSec - scene.Take.InSec;
            } else if (scene.Duration?.Mode is "fixed" or "estimate") {
                seconds = scene.Duration.Sec ?? 0;
            } else {
                seconds = 0;
            }
            if (!double.IsFinite(seconds) || seconds <= 0) {
                diagnostics.Add(VideoDiagnostic.Create(VideoDiagnosticSeverity.Error, "invalid_duration", scene.Id, path, "The scene has no positive duration from its take, fixed length or estimate."));
                return FallbackDuration;
            }
            return MediaTime.FromSeconds(seconds);
        }
    }
}
