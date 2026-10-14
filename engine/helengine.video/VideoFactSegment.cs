namespace helengine.video {
    /// <summary>
    /// One kept segment of a scene take: where it plays in the scene and which source seconds it plays.
    /// </summary>
    public sealed class VideoFactSegment {
        /// <summary>
        /// Start in seconds from the scene start.
        /// </summary>
        public double StartSec { get; set; } = 0;

        /// <summary>
        /// End in seconds from the scene start.
        /// </summary>
        public double EndSec { get; set; } = 0;

        /// <summary>
        /// Source start in seconds, as written in the take segments.
        /// </summary>
        public double SourceInSec { get; set; } = 0;

        /// <summary>
        /// Source end in seconds, as written in the take segments.
        /// </summary>
        public double SourceOutSec { get; set; } = 0;
    }
}
