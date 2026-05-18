namespace helengine.editor.tests.testing {
    /// <summary>
    /// Stores one simple reflected component shape that matches the planned scene-memory probe authoring contract.
    /// </summary>
    public sealed class TestSceneMemoryProbeSerializableComponent : Component {
        /// <summary>
        /// Gets or sets the authored probe name.
        /// </summary>
        public string ProbeName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the authored probe should loop.
        /// </summary>
        public bool Loop { get; set; }

        /// <summary>
        /// Gets or sets the authored ordered probe steps.
        /// </summary>
        public TestSceneMemoryProbeSerializableStep[] Steps { get; set; }
    }
}
