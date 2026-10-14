using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Builds composition animation tracks: raw keyframes, the zoom-to-focus motion preset, the appear ramp and the caption
    /// entrance lift. Times are local to the animated layer.
    /// </summary>
    public static class VideoAnimationBuilder {
        /// <summary>
        /// Builds a two-keyframe track.
        /// </summary>
        /// <param name="property">Animated layer property.</param>
        /// <param name="start">Local start time.</param>
        /// <param name="from">Value at the start.</param>
        /// <param name="end">Local end time.</param>
        /// <param name="to">Value at the end.</param>
        /// <param name="curve">Curve from the start keyframe toward the end keyframe.</param>
        /// <returns>Animation track.</returns>
        public static PropertyAnimation Ramp(string property, MediaTime start, double from, MediaTime end, double to, string curve) {
            return new PropertyAnimation {
                Property = property,
                Keyframes = [new AnimationKeyframe { Time = start, Value = from, Curve = curve }, new AnimationKeyframe { Time = end, Value = to, Curve = "linear.v1" }]
            };
        }

        /// <summary>
        /// Converts raw keyframes to a track, resolving each moment inside the scene.
        /// </summary>
        /// <param name="state">Compilation state.</param>
        /// <param name="span">Scene span; the layer starts at the scene start.</param>
        /// <param name="animation">Raw animation.</param>
        /// <param name="path">JSON path used in diagnostics.</param>
        /// <returns>Animation track in layer time.</returns>
        public static PropertyAnimation Raw(VideoCompileState state, VideoSceneSpan span, VideoAnimation animation, string path) {
            PropertyAnimation track = new PropertyAnimation { Property = animation.Property };
            for (int index = 0; index < animation.Keyframes.Count; index++) {
                VideoKeyframe frame = animation.Keyframes[index];
                MediaTime time = VideoMomentResolver.Resolve(state.Edit, span, frame.At, $"{path}.keyframes[{index}].at", state.Diagnostics);
                track.Keyframes.Add(new AnimationKeyframe { Time = time, Value = frame.Value, Curve = frame.Curve });
            }
            track.Keyframes.Sort((left, right) => left.Time.CompareTo(right.Time));
            return track;
        }

        /// <summary>
        /// Cuts the part of a track between <paramref name="from"/> and <paramref name="from"/> + <paramref name="length"/>
        /// into a track local to a layer that starts at <paramref name="from"/>, so a layer split into consecutive pieces
        /// (the segments of a jump-cut take) animates continuously across the cuts. Whole curve spans and linear or held
        /// spans are kept exactly; a curved span cut by the window is sampled every <paramref name="step"/> with linear
        /// keyframes.
        /// </summary>
        /// <param name="animation">Track in the time of the unsplit layer, with strictly ordered keyframes.</param>
        /// <param name="from">Window start in the time of the unsplit layer.</param>
        /// <param name="length">Positive window length.</param>
        /// <param name="step">Positive sampling interval for cut curves, normally one frame.</param>
        /// <returns>Track in window-local time with keyframes from zero to <paramref name="length"/>.</returns>
        public static PropertyAnimation Window(PropertyAnimation animation, MediaTime from, MediaTime length, MediaTime step) {
            List<AnimationKeyframe> source = animation.Keyframes;
            MediaTime end = from + length;
            List<MediaTime> breaks = [MediaTime.Zero];
            breaks.AddRange(source.Where(frame => frame.Time > from && frame.Time < end).Select(frame => frame.Time - from));
            breaks.Add(length);
            PropertyAnimation window = new PropertyAnimation { Property = animation.Property };
            for (int index = 0; index + 1 < breaks.Count; index++) {
                MediaTime start = breaks[index], stop = breaks[index + 1];
                // The original keyframe governing this span, or none when the span is held before the first or after the last.
                int governing = source.FindLastIndex(frame => frame.Time <= from + start);
                bool held = governing < 0 || governing == source.Count - 1;
                if (!held && source[governing].Time == from + start && source[governing + 1].Time == from + stop) {
                    window.Keyframes.Add(new AnimationKeyframe { Time = start, Value = source[governing].Value, Curve = source[governing].Curve });
                } else if (held || source[governing].Curve == "linear.v1") {
                    window.Keyframes.Add(new AnimationKeyframe { Time = start, Value = animation.Evaluate(from + start), Curve = "linear.v1" });
                } else {
                    for (MediaTime time = start; time < stop; time += step) {
                        window.Keyframes.Add(new AnimationKeyframe { Time = time, Value = animation.Evaluate(from + time), Curve = "linear.v1" });
                    }
                }
            }
            window.Keyframes.Add(new AnimationKeyframe { Time = length, Value = animation.Evaluate(end), Curve = "linear.v1" });
            return window;
        }

        /// <summary>
        /// Adds the entrance and exit lift used by captions and overlays: the text rises into place, holds, and sinks out.
        /// </summary>
        /// <param name="layer">Text layer.</param>
        /// <param name="captions">Caption track carrying the lift settings.</param>
        public static void AddLift(VisualLayer layer, VideoCaptionTrack captions) {
            if (captions == null || captions.LiftFraction <= 0) {
                return;
            }
            MediaTime duration = layer.End - layer.Start;
            double seconds = duration.ToSeconds();
            double entrance = Math.Min(captions.EntranceSec, seconds * 0.4), exit = Math.Min(captions.ExitSec, seconds * 0.2);
            if (entrance <= 0 || exit <= 0) {
                return;
            }
            layer.Animations.Add(new PropertyAnimation {
                Property = "position_y",
                Keyframes = [
                    new AnimationKeyframe { Time = MediaTime.Zero, Value = captions.LiftFraction, Curve = "ease_out_cubic.v1" },
                    new AnimationKeyframe { Time = MediaTime.FromSeconds(entrance), Value = 0, Curve = "linear.v1" },
                    new AnimationKeyframe { Time = duration - MediaTime.FromSeconds(exit), Value = 0, Curve = "ease_in_quad.v1" },
                    new AnimationKeyframe { Time = duration, Value = captions.LiftFraction, Curve = "linear.v1" }
                ]
            });
        }
    }
}
