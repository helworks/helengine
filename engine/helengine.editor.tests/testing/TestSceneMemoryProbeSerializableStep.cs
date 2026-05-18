namespace helengine.editor.tests.testing {
    /// <summary>
    /// Stores one simple nested authored probe step used to exercise automatic reflected persistence support for arrays of simple classes.
    /// </summary>
    public sealed class TestSceneMemoryProbeSerializableStep {
        /// <summary>
        /// Gets or sets the action that this probe step should execute.
        /// </summary>
        public TestSceneMemoryProbeSerializableActionKind ActionKind { get; set; }

        /// <summary>
        /// Gets or sets the authored scene id associated with this probe step.
        /// </summary>
        public string SceneId { get; set; }

        /// <summary>
        /// Gets or sets the authored step duration in seconds.
        /// </summary>
        public double DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the human-readable step label.
        /// </summary>
        public string Label { get; set; }
    }
}
