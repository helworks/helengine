namespace helengine.editor {
    /// <summary>
    /// Selects how one model source file is imported into engine assets.
    /// </summary>
    public enum ModelImportMode : byte {
        /// <summary>
        /// The file imports as one model asset. This is the default and the only behavior of settings written before import modes existed.
        /// </summary>
        SingleModel = 0,

        /// <summary>
        /// The file imports as a scene made of several parts.
        /// </summary>
        Scene = 1
    }
}
