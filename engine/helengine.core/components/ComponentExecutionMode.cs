namespace helengine {
    /// <summary>
    /// Identifies whether component behavior should execute as runtime gameplay or as editor-side authoring.
    /// </summary>
    public enum ComponentExecutionMode {
        /// <summary>
        /// Components are executing in normal runtime gameplay mode.
        /// </summary>
        Runtime = 0,

        /// <summary>
        /// Components are executing while the editor is authoring or previewing a scene.
        /// </summary>
        Editor = 1
    }
}
