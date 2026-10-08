using helengine.media;

namespace helengine.video {
    /// <summary>
    /// One composition keyframe of an expanded graphic element while its tracks are merged, in absolute timeline time.
    /// </summary>
    public sealed class VideoGraphicKeyframe {
        /// <summary>
        /// Gets the absolute timeline time.
        /// </summary>
        public MediaTime Time { get; }

        /// <summary>
        /// Gets the composition property value.
        /// </summary>
        public double Value { get; }

        /// <summary>
        /// Gets the curve toward the next keyframe.
        /// </summary>
        public string Curve { get; }

        /// <summary>
        /// Creates one keyframe.
        /// </summary>
        /// <param name="time">Absolute timeline time.</param>
        /// <param name="value">Composition property value.</param>
        /// <param name="curve">Curve toward the next keyframe.</param>
        public VideoGraphicKeyframe(MediaTime time, double value, string curve) {
            Time = time;
            Value = value;
            Curve = curve;
        }
    }
}
