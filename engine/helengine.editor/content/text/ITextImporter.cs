namespace helengine.editor {
    /// <summary>
    /// Provides a contract for importing plain text assets from streams.
    /// </summary>
    public interface ITextImporter {
        /// <summary>
        /// Imports a text asset from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing UTF-8 text data.</param>
        /// <returns>A newly created <see cref="TextAsset"/>.</returns>
        TextAsset ImportText(Stream stream);
    }
}
