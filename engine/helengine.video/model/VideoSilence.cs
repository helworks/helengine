namespace helengine.video {
    /// <summary>
    /// One silent source interval.
    /// </summary>
    public sealed class VideoSilence {
        /// <summary>
        /// Source start in seconds.
        /// </summary>
        public double StartSec { get; set; } = 0;

        /// <summary>
        /// Source end in seconds.
        /// </summary>
        public double EndSec { get; set; } = 0;

    }
}
