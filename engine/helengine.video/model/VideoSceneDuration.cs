namespace helengine.video {
    /// <summary>
    /// Source of a scene length.
    /// </summary>
    public sealed class VideoSceneDuration {
        /// <summary>
        /// from_take, fixed or estimate.
        /// </summary>
        public string Mode { get; set; } = "estimate";

        /// <summary>
        /// Length in seconds for fixed and estimate.
        /// </summary>
        public double? Sec { get; set; }

        /// <summary>
        /// human when locked against AI replanning.
        /// </summary>
        public string By { get; set; }

    }
}
