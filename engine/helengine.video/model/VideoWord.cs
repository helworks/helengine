namespace helengine.video {
    /// <summary>
    /// One spoken word with its source interval.
    /// </summary>
    public sealed class VideoWord {
        /// <summary>
        /// Word as transcribed.
        /// </summary>
        public string Text { get; set; } = "";

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
