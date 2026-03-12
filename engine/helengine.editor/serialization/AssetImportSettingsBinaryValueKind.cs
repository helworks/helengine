namespace helengine.editor {
    /// <summary>
    /// Identifies the concrete value stored in an asset import settings payload.
    /// </summary>
    public enum AssetImportSettingsBinaryValueKind : ushort {
        /// <summary>
        /// The payload stores an <see cref="AssetImportSettings"/> instance.
        /// </summary>
        AssetImportSettings = 1
    }
}
