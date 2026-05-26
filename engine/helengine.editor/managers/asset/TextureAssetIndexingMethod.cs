namespace helengine.editor {
    /// <summary>
    /// Identifies the shared editor-side palette indexing strategy used when one platform selects an indexed texture format.
    /// </summary>
    public enum TextureAssetIndexingMethod : byte {
        /// <summary>
        /// Reduces the source image into the target palette size using alpha-aware color quantization.
        /// </summary>
        QuantizedIndexed = 1
    }
}
