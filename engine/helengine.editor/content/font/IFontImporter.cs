namespace helengine.editor {
    /// <summary>
    /// Provides a contract for importing font assets.
    /// </summary>
    public interface IFontImporter {
        /// <summary>
        /// Imports the representation of a font from a stream.
        /// </summary>
        /// <param name="stream">Stream containing font data.</param>
        /// <returns>Imported <see cref="ModelAsset"/> for the font.</returns>
        FontAsset ImportFont(Stream stream);
    }
}
