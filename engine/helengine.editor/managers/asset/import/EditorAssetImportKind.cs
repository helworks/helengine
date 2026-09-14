namespace helengine.editor {
    /// <summary>
    /// Identifies the asset families the editor import pipeline registers importers for. Each kind owns exactly one
    /// <see cref="EditorAssetTypeImportHandler"/> implementation inside <see cref="AssetImportManager"/>.
    /// </summary>
    enum EditorAssetImportKind {
        /// <summary>
        /// Texture assets imported from image source files.
        /// </summary>
        Texture = 0,

        /// <summary>
        /// Text assets imported from plain-text source files.
        /// </summary>
        Text = 1,

        /// <summary>
        /// Font assets imported from font source files.
        /// </summary>
        Font = 2,

        /// <summary>
        /// Audio assets imported from sound source files.
        /// </summary>
        Audio = 3,

        /// <summary>
        /// Model assets imported from mesh source files.
        /// </summary>
        Model = 4
    }
}
