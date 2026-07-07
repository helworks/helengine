namespace helengine {
    /// <summary>
    /// Abstract base for 2D rendering backends.
    /// </summary>
    public abstract class RenderManager2D : IDisposable {
        /// <summary>
        /// Builds a runtime texture from raw texture data.
        /// </summary>
        /// <param name="data">Raw texture data.</param>
        /// <returns>Runtime texture instance.</returns>
        public abstract RuntimeTexture BuildTextureFromRaw(TextureAsset data);

        /// <summary>
        /// Builds a runtime texture from one platform-owned cooked texture payload.
        /// </summary>
        /// <param name="cookedAssetPath">Runtime asset path of the cooked texture payload.</param>
        /// <param name="contentStreamSource">Stream source that owns cooked asset reads for the active runtime.</param>
        /// <returns>Runtime texture instance.</returns>
        public virtual RuntimeTexture BuildTextureFromCooked(string cookedAssetPath, IContentStreamSource contentStreamSource) {
            if (string.IsNullOrWhiteSpace(cookedAssetPath)) {
                throw new ArgumentException("Cooked texture asset path must be provided.", nameof(cookedAssetPath));
            }
            if (contentStreamSource == null) {
                throw new ArgumentNullException(nameof(contentStreamSource));
            }

            throw new NotSupportedException("This renderer does not support platform-owned cooked texture creation.");
        }

        /// <summary>
        /// Releases one runtime texture previously created by this renderer and assumes ownership for final destruction timing.
        /// </summary>
        /// <param name="texture">Runtime texture that should release any renderer-owned resources and be disposed when safe.</param>
        public virtual void ReleaseTexture(RuntimeTexture texture) {
            if (texture == null) {
                throw new ArgumentNullException(nameof(texture));
            }

            NativeOwnership.DisposeAndDelete(texture);
        }

        /// <summary>
        /// Releases one font asset previously materialized for this renderer and assumes ownership for final destruction timing.
        /// </summary>
        /// <param name="font">Font asset that should release any renderer-owned or native-owned resources when safe.</param>
        public virtual void ReleaseFont(FontAsset font) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            RuntimeTexture texture = font.Texture;
            if (texture != null && !texture.IsDisposed) {
                ReleaseTexture(texture);
            }

            font.Dispose();
            NativeOwnership.Delete(font);
        }

        /// <summary>
        /// Flushes any renderer-owned runtime texture releases that were deferred until the renderer reached a safe point.
        /// </summary>
        public virtual void FlushReleasedTextures() {
        }

        /// <summary>
        /// Performs per-frame update for 2D rendering systems.
        /// </summary>
        public virtual void Update() { }

        /// <summary>
        /// Executes the 2D render pass.
        /// </summary>
        public virtual void Draw() { }

        /// <summary>
        /// Releases resources owned by the render manager.
        /// </summary>
        public virtual void Dispose() { }

        /// <summary>
        /// Draws a sprite component.
        /// </summary>
        /// <param name="sprite">Sprite to draw.</param>
        public abstract void DrawSprite(ISpriteDrawable2D sprite);

        /// <summary>
        /// Draws text for a text drawable.
        /// </summary>
        /// <param name="text">Text drawable.</param>
        public abstract void DrawText(ITextDrawable2D text);

        /// <summary>
        /// Draws a rounded rectangle.
        /// </summary>
        /// <param name="shape">Rounded rectangle drawable.</param>
        public abstract void DrawRoundedRect(IRoundedRectDrawable2D shape);
    }
}
