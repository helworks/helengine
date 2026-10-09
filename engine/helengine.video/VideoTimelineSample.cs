namespace helengine.video {
    /// <summary>
    /// One keyframe of a composition property while it is still in overlay timeline seconds: the value at an instant and
    /// the catalog curve used from it to the next keyframe.
    /// </summary>
    public sealed class VideoTimelineSample {
        /// <summary>
        /// Creates a keyframe.
        /// </summary>
        /// <param name="time">Instant in timeline seconds.</param>
        /// <param name="value">Composition property value.</param>
        /// <param name="curve">Curve id towards the next keyframe.</param>
        public VideoTimelineSample(double time, double value, string curve) {
            Time = time;
            Value = value;
            Curve = curve;
        }

        /// <summary>
        /// Instant in timeline seconds.
        /// </summary>
        public double Time { get; }

        /// <summary>
        /// Composition property value.
        /// </summary>
        public double Value { get; }

        /// <summary>
        /// Curve id towards the next keyframe.
        /// </summary>
        public string Curve { get; }
    }
}
