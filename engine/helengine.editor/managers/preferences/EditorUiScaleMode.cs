namespace helengine.editor {
    /// <summary>
    /// Controls whether the editor UI follows the current monitor DPI or one explicit user-selected scale.
    /// </summary>
    public enum EditorUiScaleMode {
        /// <summary>
        /// Uses the current monitor DPI to resolve the effective editor UI scale.
        /// </summary>
        Auto,
        /// <summary>
        /// Uses one explicit user-selected scale percentage instead of the monitor DPI.
        /// </summary>
        Override
    }
}
