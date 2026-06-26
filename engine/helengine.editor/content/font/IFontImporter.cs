namespace helengine.editor {
    /// <summary>
    /// Provides a contract for importing font assets.
    /// </summary>
    public interface IFontImporter {
        /// <summary>
        /// Imports the representation of a font from a stream using the supplied platform font settings.
        /// </summary>
        /// <param name="stream">Stream containing font data.</param>
        /// <param name="settings">Active platform font settings that should drive rasterization.</param>
        /// <returns>Imported <see cref="FontAsset"/> for the font.</returns>
        FontAsset ImportFont(Stream stream, FontAssetProcessorSettings settings);
    }
}
