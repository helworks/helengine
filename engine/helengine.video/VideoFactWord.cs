namespace helengine.video {
    /// <summary>
    /// One spoken word of a scene take, timed relative to the start of the scene.
    /// </summary>
    public sealed class VideoFactWord {
        /// <summary>
        /// Word as transcribed.
        /// </summary>
        public string Text { get; set; } = "";

        /// <summary>
        /// Start in seconds from the scene start.
        /// </summary>
        public double StartSec { get; set; } = 0;

        /// <summary>
        /// End in seconds from the scene start.
        /// </summary>
        public double EndSec { get; set; } = 0;
    }
}
