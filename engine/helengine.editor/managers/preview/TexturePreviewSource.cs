namespace helengine.editor {
    /// <summary>
    /// Preview source that exposes one imported texture as a runtime texture.
    /// </summary>
    public class TexturePreviewSource : IPreviewSource {
        /// <summary>
        /// Runtime texture exposed by the preview source.
        /// </summary>
        readonly RuntimeTexture texture;

        /// <summary>
        /// Initializes a new texture preview source from one runtime texture.
        /// </summary>
        /// <param name="texture">Runtime texture to expose.</param>
        public TexturePreviewSource(RuntimeTexture texture) {
            if (texture == null) {
                throw new ArgumentNullException(nameof(texture));
            }

            this.texture = texture;
        }

        /// <summary>
        /// Loads one texture asset through the editor import pipeline and wraps it in a preview source.
        /// </summary>
        /// <param name="entry">Selected asset browser entry.</param>
        /// <param name="assetImportManager">Asset import manager used to load the texture asset.</param>
        /// <param name="renderManager2D">2D render manager used to build the runtime texture.</param>
        /// <param name="source">Created preview source when the texture could be loaded.</param>
        /// <returns>True when a preview source was created; otherwise false.</returns>
        public static bool TryCreate(AssetBrowserEntry entry, AssetImportManager assetImportManager, RenderManager2D renderManager2D, out TexturePreviewSource source) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            if (assetImportManager == null) {
                throw new ArgumentNullException(nameof(assetImportManager));
            }
            if (renderManager2D == null) {
                throw new ArgumentNullException(nameof(renderManager2D));
            }

            TextureAsset textureAsset;
            if (!assetImportManager.TryLoadTextureAsset(entry.FullPath, out textureAsset)) {
                source = null;
                return false;
            }

            source = new TexturePreviewSource(renderManager2D.BuildTextureFromRaw(textureAsset));
            return true;
        }

        /// <summary>
        /// Gets the runtime texture currently exposed by the preview source.
        /// </summary>
        public RuntimeTexture Texture => texture;

        /// <summary>
        /// Texture previews are static and do not need a per-frame update.
        /// </summary>
        public void Update() {
        }

        /// <summary>
        /// Texture previews keep their native texture dimensions and do not resize in place.
        /// </summary>
        /// <param name="contentSize">Usable panel content size in pixels.</param>
        public void Resize(int2 contentSize) {
        }

        /// <summary>
        /// Releases the preview source.
        /// </summary>
        public void Dispose() {
        }
    }
}
