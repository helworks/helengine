namespace helengine.video {
    /// <summary>
    /// The part of a recording that a scene plays.
    /// </summary>
    public sealed class VideoTake {
        /// <summary>
        /// Video or audio media id.
        /// </summary>
        public string Media { get; set; } = "";

        /// <summary>
        /// Source start in seconds.
        /// </summary>
        public double InSec { get; set; } = 0;

        /// <summary>
        /// Source end in seconds.
        /// </summary>
        public double OutSec { get; set; } = 0;

        /// <summary>
        /// human when locked against AI replanning.
        /// </summary>
        public string By { get; set; }

    }
}
