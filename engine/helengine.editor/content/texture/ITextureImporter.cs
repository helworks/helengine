namespace helengine.editor {
    /// <summary>
    /// Provides a contract for importing textures from arbitrary streams.
    /// </summary>
    public interface ITextureImporter {
        /// <summary>
        /// Imports a texture asset from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing texture data.</param>
        /// <returns>A newly created <see cref="TextureAsset"/>.</returns>
        TextureAsset ImportTexture(Stream stream);
    }
}
