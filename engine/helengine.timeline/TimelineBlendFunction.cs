namespace helengine.timeline {
    /// <summary>
    /// The cross-fade of two neighbouring clips on one channel across their overlap [start, end]: the outgoing clip's
    /// value weighted by 1 - w plus the incoming clip's value weighted by w, where w rises linearly from 0 at the start
    /// of the overlap to 1 at its end.
    /// </summary>
    sealed class TimelineBlendFunction : ITimelineScalarFunction {
        /// <summary>
        /// Segments of the outgoing clip.
        /// </summary>
        readonly List<FlattenedCurveSegment> Outgoing;

        /// <summary>
        /// Segments of the incoming clip.
        /// </summary>
        readonly List<FlattenedCurveSegment> Incoming;

        /// <summary>
        /// Start of the overlap.
        /// </summary>
        readonly double StartSeconds;

        /// <summary>
        /// End of the overlap.
        /// </summary>
        readonly double EndSeconds;

        /// <summary>
        /// Initializes the blend.
        /// </summary>
        /// <param name="outgoing">Segments of the clip that ends.</param>
        /// <param name="incoming">Segments of the clip that starts.</param>
        /// <param name="startSeconds">Start of the overlap (incoming clip start).</param>
        /// <param name="endSeconds">End of the overlap (outgoing clip end).</param>
        public TimelineBlendFunction(List<FlattenedCurveSegment> outgoing, List<FlattenedCurveSegment> incoming, double startSeconds, double endSeconds) {
            Outgoing = outgoing;
            Incoming = incoming;
            StartSeconds = startSeconds;
            EndSeconds = endSeconds;
        }

        /// <summary>
        /// Evaluates the cross-fade.
        /// </summary>
        /// <param name="seconds">Time inside the overlap.</param>
        /// <returns>The blended value.</returns>
        public double Evaluate(double seconds) {
            double weight = (seconds - StartSeconds) / (EndSeconds - StartSeconds);
            if (weight < 0) {
                weight = 0;
            } else if (weight > 1) {
                weight = 1;
            }

            return (1 - weight) * TimelinePiecewise.Evaluate(Outgoing, seconds) + weight * TimelinePiecewise.Evaluate(Incoming, seconds);
        }
    }
}
