namespace helengine.editor.tests.testing {
    /// <summary>
    /// Represents one simple probe-step action used to exercise automatic reflected persistence support for enums.
    /// </summary>
    public enum TestSceneMemoryProbeSerializableActionKind {
        /// <summary>
        /// Waits for one authored duration.
        /// </summary>
        Wait = 0,

        /// <summary>
        /// Loads one scene in single mode.
        /// </summary>
        LoadSceneSingle = 1
    }
}
