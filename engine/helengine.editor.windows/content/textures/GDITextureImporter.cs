namespace helengine.editor {
    /// <summary>
    /// Texture importer backed by GDI utilities for simple formats.
    /// </summary>
    public class GDITextureImporter : ITextureImporter {
        /// <summary>
        /// Imports a texture asset from the provided stream.
        /// </summary>
        /// <param name="stream">Stream containing the raw texture data.</param>
        /// <returns>Created <see cref="TextureAsset"/> instance.</returns>
        public TextureAsset ImportTexture(Stream stream) {

            TextureAsset rawTex = new TextureAsset();

            throw new NotImplementedException();
        }
    }
}
