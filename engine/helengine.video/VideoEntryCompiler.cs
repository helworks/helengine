using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Compiles transition entries: the previous scene is extended over the overlap (never shortening the video) and a
    /// composition transition blends the two scene groups. Take and video layers that run out of source hold their last
    /// frame instead of failing.
    /// </summary>
    public static class VideoEntryCompiler {
        /// <summary>
        /// Adds every transition entry.
        /// </summary>
        /// <param name="state">Compilation state with all scene groups built.</param>
        public static void Compile(VideoCompileState state) {
            for (int index = 1; index < state.Spans.Count; index++) {
                VideoSceneSpan span = state.Spans[index];
                VideoEntry entry = span.Scene.Entry;
                if (entry == null || entry.Effect is "cut" or "appear") {
                    continue;
                }
                MediaTime overlap = MediaTime.FromSeconds(Math.Min(entry.DurationSec, span.Duration.ToSeconds()));
                VideoSceneSpan previous = state.Spans[index - 1];
                Extend(state, previous, previous.End + overlap);
                state.Document.Transitions.Add(new CompositionTransition {
                    Id = "transition-" + span.Scene.Id,
                    FromLayer = "scene-" + previous.Scene.Id,
                    ToLayer = "scene-" + span.Scene.Id,
                    Start = span.Start,
                    Duration = overlap,
                    EffectId = entry.Effect,
                    EffectVersion = entry.Version,
                    Parameters = new(entry.Parameters, StringComparer.Ordinal)
                });
            }
        }

        /// <summary>
        /// Extends a scene group and the members that reach its end.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene being extended.</param>
        /// <param name="end">New global end.</param>
        static void Extend(VideoCompileState state, VideoSceneSpan span, MediaTime end) {
            VisualLayer group = state.Groups[span.Scene.Id];
            MediaTime oldEnd = group.End;
            group.End = end;
            foreach (string id in group.Members) {
                VisualLayer member = state.Document.Layers.First(layer => layer.Id == id);
                if (member.End != oldEnd) {
                    continue;
                }
                MediaTime extra = end - member.End;
                member.End = end;
                if (member.Kind == "text") {
                    member.Text.Cues[^1].End = end;
                } else if (state.Media.TryGetValue(member.MediaId ?? "", out VideoMedia media) && media.Kind == "video") {
                    MediaTime wanted = member.SourceOut + extra;
                    if (!member.HoldLastFrame && media.DurationSec > 0 && wanted.ToSeconds() <= media.DurationSec) {
                        member.SourceOut = wanted;
                    } else {
                        member.HoldLastFrame = true;
                    }
                }
            }
        }
    }
}
