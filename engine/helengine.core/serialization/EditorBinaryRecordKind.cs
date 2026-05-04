namespace helengine {
    /// <summary>
    /// Identifies the logical record stored in an editor-authored HELE payload.
    /// </summary>
    public enum EditorBinaryRecordKind : ushort {
        /// <summary>
        /// The payload stores a serialized asset.
        /// </summary>
        Asset = 1,

        /// <summary>
        /// The payload stores asset import settings.
        /// </summary>
        AssetImportSettings = 2,

        /// <summary>
        /// The payload stores shader cache metadata.
        /// </summary>
        ShaderCacheMetadata = 3,

        /// <summary>
        /// The payload stores a packaged font asset.
        /// </summary>
        FontAsset = 4
    }
}
