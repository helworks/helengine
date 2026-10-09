namespace helengine.timeline {
    /// <summary>
    /// Operations on sorted, non-overlapping lists of <see cref="FlattenedCurveSegment"/> treated as one function of time.
    /// </summary>
    static class TimelinePiecewise {
        /// <summary>
        /// Evaluates the list at a time: the segment containing it, the end value of the last segment before it, or the
        /// start value of the first segment when it lies before them all.
        /// </summary>
        /// <param name="segments">Sorted segments (at least one).</param>
        /// <param name="seconds">Time to sample.</param>
        /// <returns>The value.</returns>
        public static double Evaluate(List<FlattenedCurveSegment> segments, double seconds) {
            FlattenedCurveSegment current = segments[0];
            for (int index = 0; index < segments.Count; index++) {
                if (segments[index].StartSeconds <= seconds) {
                    current = segments[index];
                } else {
                    break;
                }
            }
            return current.Evaluate(seconds);
        }

        /// <summary>
        /// Appends the parts of a list that fall between two times, dropping parts shorter than the timeline tolerance.
        /// </summary>
        /// <param name="segments">Sorted segments.</param>
        /// <param name="startSeconds">Start of the window.</param>
        /// <param name="endSeconds">End of the window.</param>
        /// <param name="output">List receiving the trimmed parts.</param>
        public static void AppendTrimmed(List<FlattenedCurveSegment> segments, double startSeconds, double endSeconds, List<FlattenedCurveSegment> output) {
            for (int index = 0; index < segments.Count; index++) {
                FlattenedCurveSegment segment = segments[index];
                double start = segment.StartSeconds > startSeconds ? segment.StartSeconds : startSeconds;
                double end = segment.EndSeconds < endSeconds ? segment.EndSeconds : endSeconds;
                if (end - start > TimelineValidator.TimeTolerance) {
                    output.Add(segment.Trim(start, end));
                }
            }
        }
    }
}
