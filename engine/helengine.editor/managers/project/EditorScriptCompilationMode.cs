namespace helengine.editor {
    /// <summary>
    /// Selects which authored script surfaces participate in one generated-code solution.
    /// </summary>
    public enum EditorScriptCompilationMode {
        /// <summary>
        /// Includes runtime modules, editor modules, and inferred sibling test projects for editor authoring workflows.
        /// </summary>
        EditorFull,

        /// <summary>
        /// Includes runtime production modules only for cook and native platform packaging workflows.
        /// </summary>
        RuntimeOnly
    }
}
