namespace helengine.timeline {
    /// <summary>
    /// Maps the local time of one (possibly nested) timeline instance to root-timeline time,
    /// <c>root = Offset + local * Scale</c>, and records the root-time window in which the instance is visible (the
    /// intersection of every enclosing nested clip).
    /// </summary>
    sealed class TimelineTimeMap {
        /// <summary>
        /// Gets the root time of local time 0.
        /// </summary>
        public double Offset { get; }

        /// <summary>
        /// Gets the root seconds per local second (1 / product of nested speeds).
        /// </summary>
        public double Scale { get; }

        /// <summary>
        /// Gets the first root time at which the instance is visible.
        /// </summary>
        public double WindowStart { get; }

        /// <summary>
        /// Gets the root time at which the instance stops being visible.
        /// </summary>
        public double WindowEnd { get; }

        /// <summary>
        /// Initializes a map.
        /// </summary>
        /// <param name="offset">Root time of local time 0.</param>
        /// <param name="scale">Root seconds per local second.</param>
        /// <param name="windowStart">Visible window start.</param>
        /// <param name="windowEnd">Visible window end.</param>
        public TimelineTimeMap(double offset, double scale, double windowStart, double windowEnd) {
            Offset = offset;
            Scale = scale;
            WindowStart = windowStart;
            WindowEnd = windowEnd;
        }

        /// <summary>
        /// Creates the identity map of a root timeline.
        /// </summary>
        /// <param name="durationSeconds">Root duration.</param>
        /// <returns>The map.</returns>
        public static TimelineTimeMap Root(double durationSeconds) {
            return new TimelineTimeMap(0, 1, 0, durationSeconds);
        }

        /// <summary>
        /// Converts local time to root time.
        /// </summary>
        /// <param name="localSeconds">Local time.</param>
        /// <returns>Root time.</returns>
        public double ToRoot(double localSeconds) {
            return Offset + localSeconds * Scale;
        }

        /// <summary>
        /// Builds the map of a timeline nested in this instance by one nested clip: the child's local time
        /// <c>clipIn + (t - start) * speed</c> plays while this instance is at <c>t</c> in [start, start + duration].
        /// </summary>
        /// <param name="startSeconds">Clip start in this instance's time.</param>
        /// <param name="durationSeconds">Clip duration in this instance's time.</param>
        /// <param name="clipInSeconds">Child time shown at the clip start.</param>
        /// <param name="speed">Child seconds per second of this instance.</param>
        /// <returns>The child map.</returns>
        public TimelineTimeMap Nest(double startSeconds, double durationSeconds, double clipInSeconds, double speed) {
            double childOffset = ToRoot(startSeconds - clipInSeconds / speed);
            double childScale = Scale / speed;
            double childStart = ToRoot(startSeconds);
            double childEnd = ToRoot(startSeconds + durationSeconds);
            return new TimelineTimeMap(
                childOffset,
                childScale,
                childStart > WindowStart ? childStart : WindowStart,
                childEnd < WindowEnd ? childEnd : WindowEnd);
        }
    }
}
