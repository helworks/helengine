namespace helengine {
    /// <summary>
    /// Stores one authored scene-memory probe step.
    /// </summary>
    public sealed class SceneMemoryProbeStep {
        /// <summary>
        /// Gets or sets the authored action executed by this probe step.
        /// </summary>
        public SceneMemoryProbeActionKind ActionKind { get; set; }

        /// <summary>
        /// Gets or sets the target scene id used by load and unload steps.
        /// </summary>
        public string SceneId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the authored wait duration in seconds.
        /// </summary>
        public double DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the stable label written to the emitted probe checkpoint.
        /// </summary>
        public string Label { get; set; } = string.Empty;
    }
}
