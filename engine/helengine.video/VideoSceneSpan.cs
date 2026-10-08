using helengine.media;

namespace helengine.video {
    /// <summary>
    /// Where one scene sits on the global timeline once its duration source is resolved.
    /// </summary>
    public sealed class VideoSceneSpan {
        /// <summary>
        /// Gets or sets the scene.
        /// </summary>
        public VideoScene Scene { get; set; }

        /// <summary>
        /// Gets or sets the zero-based scene index.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets the global start time.
        /// </summary>
        public MediaTime Start { get; set; }

        /// <summary>
        /// Gets or sets the global end time.
        /// </summary>
        public MediaTime End { get; set; }

        /// <summary>
        /// Gets the scene duration.
        /// </summary>
        public MediaTime Duration {
            get {
                return End - Start;
            }
        }

        /// <summary>
        /// Gets or sets whether the duration is only an estimate awaiting a take.
        /// </summary>
        public bool Estimated { get; set; }
    }
}
