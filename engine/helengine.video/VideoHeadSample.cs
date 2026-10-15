using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Where the speaker's head is on screen at one instant of the composition, after fit, zoom, focus and transform.
    /// </summary>
    public sealed class VideoHeadSample {
        /// <summary>
        /// Creates a sample.
        /// </summary>
        /// <param name="time">Absolute composition instant.</param>
        /// <param name="bounds">Head box in output pixels.</param>
        public VideoHeadSample(MediaTime time, VideoGraphicRectangle bounds) {
            Time = time;
            Bounds = bounds;
        }

        /// <summary>
        /// Gets the absolute composition instant.
        /// </summary>
        public MediaTime Time { get; }

        /// <summary>
        /// Gets the head box in output pixels, clipped to the frame.
        /// </summary>
        public VideoGraphicRectangle Bounds { get; }
    }
}
