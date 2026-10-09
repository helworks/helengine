namespace helengine.timeline {
    /// <summary>
    /// Turns the keyframes of one channel into segments: per clip (holding the first value before the first keyframe and
    /// the last value after the last, up to the clip's edges) and then per track, cross-fading neighbouring clips over
    /// their overlap.
    /// </summary>
    static class TimelineChannelBuilder {
        /// <summary>
        /// Builds a clip's segments in instance time.
        /// </summary>
        /// <param name="clipStart">Clip start in instance time.</param>
        /// <param name="duration">Clip duration.</param>
        /// <param name="keys">Keyframes in clip time, strictly increasing.</param>
        /// <returns>The segments, or null when the channel has no keyframes in this clip.</returns>
        public static List<FlattenedCurveSegment> BuildClipSegments(double clipStart, double duration, List<TimelineKeyframeSample> keys) {
            if (keys.Count == 0) {
                return null;
            }

            List<FlattenedCurveSegment> segments = new List<FlattenedCurveSegment>();
            TimelineKeyframeSample first = keys[0];
            if (first.TimeSeconds > TimelineValidator.TimeTolerance) {
                segments.Add(FlattenedCurveSegment.Constant(clipStart, clipStart + first.TimeSeconds, first.Value));
            }
            for (int index = 0; index + 1 < keys.Count; index++) {
                TimelineKeyframeSample from = keys[index];
                TimelineKeyframeSample to = keys[index + 1];
                segments.Add(new FlattenedCurveSegment {
                    StartSeconds = clipStart + from.TimeSeconds,
                    EndSeconds = clipStart + to.TimeSeconds,
                    From = from.Value,
                    To = to.Value,
                    Curve = from.Curve
                });
            }

            TimelineKeyframeSample last = keys[keys.Count - 1];
            if (duration - last.TimeSeconds > TimelineValidator.TimeTolerance) {
                segments.Add(FlattenedCurveSegment.Constant(clipStart + last.TimeSeconds, clipStart + duration, last.Value));
            }
            if (segments.Count == 0) {
                segments.Add(FlattenedCurveSegment.Constant(clipStart, clipStart + duration, first.Value));
            }
            return segments;
        }

        /// <summary>
        /// Joins the clips of one track into one segment list. Where a clip overlaps the next one and both drive the
        /// channel, the overlap becomes a linear cross-fade (approximated with straight segments within the tolerance);
        /// elsewhere each clip's own segments are used.
        /// </summary>
        /// <param name="clips">The track's clips in start order.</param>
        /// <param name="tolerance">Largest error accepted when approximating cross-fades.</param>
        /// <returns>Sorted, non-overlapping segments in instance time.</returns>
        public static List<FlattenedCurveSegment> CombineClips(List<TimelineClipChannel> clips, double tolerance) {
            List<FlattenedCurveSegment> output = new List<FlattenedCurveSegment>();
            for (int index = 0; index < clips.Count; index++) {
                TimelineClipChannel clip = clips[index];
                if (clip.Segments == null) {
                    continue;
                }

                double start = clip.StartSeconds;
                double end = clip.EndSeconds;
                TimelineClipChannel previous = index > 0 ? clips[index - 1] : null;
                TimelineClipChannel next = index + 1 < clips.Count ? clips[index + 1] : null;
                if (previous != null && previous.Segments != null && clip.StartSeconds < previous.EndSeconds - TimelineValidator.TimeTolerance) {
                    start = previous.EndSeconds;
                }

                bool blendsIntoNext = next != null && next.Segments != null && next.StartSeconds < clip.EndSeconds - TimelineValidator.TimeTolerance;
                if (blendsIntoNext) {
                    end = next.StartSeconds;
                }

                TimelinePiecewise.AppendTrimmed(clip.Segments, start, end, output);
                if (blendsIntoNext) {
                    TimelineBlendFunction blend = new TimelineBlendFunction(clip.Segments, next.Segments, next.StartSeconds, clip.EndSeconds);
                    TimelineCurveLinearizer.LinearizeSeconds(blend, next.StartSeconds, clip.EndSeconds, tolerance, output);
                }
            }
            return output;
        }
    }
}
