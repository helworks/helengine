namespace helengine.video {
    /// <summary>
    /// One kept interval of a take's media; a take made of several segments plays them back to back as jump cuts.
    /// </summary>
    public sealed class VideoTakeSegment {
        /// <summary>
        /// Source start in seconds.
        /// </summary>
        public double InSec { get; set; } = 0;

        /// <summary>
        /// Source end in seconds.
        /// </summary>
        public double OutSec { get; set; } = 0;

    }
}
