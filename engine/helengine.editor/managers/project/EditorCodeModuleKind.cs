namespace helengine.editor {
    /// <summary>
    /// Declares whether one authored code module participates in runtime packaging or editor-only tooling.
    /// </summary>
    public enum EditorCodeModuleKind {
        /// <summary>
        /// Module participates in runtime packaging and player execution.
        /// </summary>
        Runtime = 0,

        /// <summary>
        /// Module loads only into the editor process and is excluded from player packaging.
        /// </summary>
        Editor = 1
    }
}
