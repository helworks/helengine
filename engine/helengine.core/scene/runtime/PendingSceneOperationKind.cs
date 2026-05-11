namespace helengine {
    /// <summary>
    /// Identifies the deferred runtime scene operation that should run after the active update loop completes.
    /// </summary>
    public enum PendingSceneOperationKind {
        /// <summary>
        /// Loads one built runtime scene.
        /// </summary>
        Load,

        /// <summary>
        /// Unloads one built runtime scene.
        /// </summary>
        Unload
    }
}
