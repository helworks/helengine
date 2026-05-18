namespace helengine {
    /// <summary>
    /// Identifies one authored scene-memory probe action.
    /// </summary>
    public enum SceneMemoryProbeActionKind {
        /// <summary>
        /// Waits for the authored duration before emitting a checkpoint.
        /// </summary>
        Wait = 0,

        /// <summary>
        /// Requests one single-scene load for the authored scene id.
        /// </summary>
        LoadSceneSingle = 1,

        /// <summary>
        /// Requests one additive scene load for the authored scene id.
        /// </summary>
        LoadSceneAdditive = 2,

        /// <summary>
        /// Requests one explicit unload for the authored scene id.
        /// </summary>
        UnloadScene = 3
    }
}
