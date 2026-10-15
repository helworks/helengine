namespace helengine.video {
    /// <summary>
    /// Where the speaker's head may sit in the output frame: the compiler limits punch-in zooms and shifts their focus so the
    /// head box of a take stays between <see cref="FaceTop"/> and <see cref="FaceBottom"/> for the whole scene.
    /// </summary>
    public sealed class VideoFraming {
        /// <summary>
        /// Highest point the head may reach, as a fraction of the output height; null uses
        /// <see cref="VideoFaceFraming.DefaultFaceTop"/>.
        /// </summary>
        public double? FaceTop { get; set; }

        /// <summary>
        /// Lowest point the head may reach, as a fraction of the output height; null uses
        /// <see cref="VideoFaceFraming.DefaultFaceBottom"/>.
        /// </summary>
        public double? FaceBottom { get; set; }

    }
}
