namespace helengine {
    /// <summary>
    /// Abstract base for 2D rendering backends.
    /// </summary>
    public abstract class RenderManager2D : IDisposable {
        /// <summary>
        /// Lazily created renderer-owned one-pixel fallback textures. These
        /// values intentionally live on the renderer rather than in a
        /// process-global utility cache so concurrent cores cannot share
        /// native resources.
        /// </summary>
        RuntimeTexture PixelTextureValue;
        RuntimeTexture BlackPixelTextureValue;

        /// <summary>
        /// Core that owns this renderer.
        /// </summary>
        public Core OwnerCore { get; internal set; }

        /// <summary>
        /// Gets the renderer-owned opaque white one-pixel texture.
        /// </summary>
        [NativeBorrowedReturn]
        public RuntimeTexture PixelTexture {
            get {
                if (PixelTextureValue == null) {
                    PixelTextureValue = TextureUtils.BuildSolidPixelTexture(this, 255, 255, 255, 255);
                }
                return PixelTextureValue;
            }
        }

        /// <summary>
        /// Gets the renderer-owned opaque black one-pixel texture.
        /// </summary>
        [NativeBorrowedReturn]
        public RuntimeTexture BlackPixelTexture {
            get {
                if (BlackPixelTextureValue == null) {
                    BlackPixelTextureValue = TextureUtils.BuildSolidPixelTexture(this, 0, 0, 0, 255);
                }
                return BlackPixelTextureValue;
            }
        }

        /// <summary>
        /// Builds a runtime texture from raw texture data.
        /// </summary>
        /// <param name="data">Raw texture data.</param>
        /// <returns>Runtime texture instance.</returns>
        public abstract RuntimeTexture BuildTextureFromRaw([NativeNoEscape] TextureAsset data);

        /// <summary>
        /// Updates one pixel rectangle in a renderer-owned runtime texture from an RGBA8 source buffer.
        /// </summary>
        /// <param name="texture">Runtime texture that receives the update.</param>
        /// <param name="x">Destination rectangle X coordinate in pixels.</param>
        /// <param name="y">Destination rectangle Y coordinate in pixels.</param>
        /// <param name="width">Destination rectangle width in pixels.</param>
        /// <param name="height">Destination rectangle height in pixels.</param>
        /// <param name="rgba8">RGBA8 source pixels, arranged row by row.</param>
        /// <param name="sourceRowPitch">Number of bytes between the starts of adjacent source rows.</param>
        public void UpdateTextureRegion(
            RuntimeTexture texture,
            int x,
            int y,
            int width,
            int height,
            [NativeNoEscape] byte[] rgba8,
            int sourceRowPitch) {
            if (texture == null) {
                throw new ArgumentNullException(nameof(texture));
            }
            if (texture.IsDisposed) {
#if HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
                throw new InvalidOperationException("Texture has been disposed.");
#else
                throw new ObjectDisposedException(nameof(texture));
#endif
            }
            if (rgba8 == null) {
                throw new ArgumentNullException(nameof(rgba8));
            }
            if (x < 0) {
                throw new ArgumentOutOfRangeException(nameof(x), "Texture region X coordinate must be non-negative.");
            }
            if (y < 0) {
                throw new ArgumentOutOfRangeException(nameof(y), "Texture region Y coordinate must be non-negative.");
            }
            if (width <= 0) {
                throw new ArgumentOutOfRangeException(nameof(width), "Texture region width must be greater than zero.");
            }
            if (height <= 0) {
                throw new ArgumentOutOfRangeException(nameof(height), "Texture region height must be greater than zero.");
            }
            if ((long)x >= texture.Width) {
                throw new ArgumentOutOfRangeException(nameof(x), "Texture region exceeds the destination texture width.");
            }
            if ((long)x + width > texture.Width) {
                throw new ArgumentOutOfRangeException(nameof(width), "Texture region exceeds the destination texture width.");
            }
            if ((long)y >= texture.Height) {
                throw new ArgumentOutOfRangeException(nameof(y), "Texture region exceeds the destination texture height.");
            }
            if ((long)y + height > texture.Height) {
                throw new ArgumentOutOfRangeException(nameof(height), "Texture region exceeds the destination texture height.");
            }

            if (width > int.MaxValue / 4) {
                throw new ArgumentOutOfRangeException(nameof(width), "Texture region row size is too large.");
            }
            int requiredRowBytes = width * 4;
            if (sourceRowPitch < requiredRowBytes) {
                throw new ArgumentOutOfRangeException(nameof(sourceRowPitch), "Source row pitch is smaller than the requested RGBA8 row.");
            }
            if (sourceRowPitch % 4 != 0) {
                throw new ArgumentException("Source row pitch must be divisible by four for RGBA8 data.", nameof(sourceRowPitch));
            }

            if (height > 1 && sourceRowPitch > (int.MaxValue - requiredRowBytes) / (height - 1)) {
                throw new ArgumentOutOfRangeException(nameof(sourceRowPitch), "Source buffer size is too large.");
            }
            int requiredBytes = sourceRowPitch * (height - 1) + requiredRowBytes;
            if (rgba8.Length < requiredBytes) {
                throw new ArgumentException("Source buffer is shorter than the requested texture region.", nameof(rgba8));
            }

            UpdateTextureRegionCore(texture, x, y, width, height, rgba8, sourceRowPitch);
        }

        /// <summary>
        /// Updates one pixel rectangle in a renderer-owned runtime texture using backend resources.
        /// </summary>
        /// <param name="texture">Runtime texture that receives the update.</param>
        /// <param name="x">Destination rectangle X coordinate in pixels.</param>
        /// <param name="y">Destination rectangle Y coordinate in pixels.</param>
        /// <param name="width">Destination rectangle width in pixels.</param>
        /// <param name="height">Destination rectangle height in pixels.</param>
        /// <param name="rgba8">RGBA8 source pixels, arranged row by row.</param>
        /// <param name="sourceRowPitch">Number of bytes between the starts of adjacent source rows.</param>
        protected abstract void UpdateTextureRegionCore(
            RuntimeTexture texture,
            int x,
            int y,
            int width,
            int height,
            [NativeNoEscape] byte[] rgba8,
            int sourceRowPitch);

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
        public virtual void Dispose() {
            DisposeDefaultTextures();
        }

        /// <summary>
        /// Releases lazily created fallback textures. Derived renderers should
        /// call this from their disposal implementation while their backend is
        /// still available.
        /// </summary>
        protected void DisposeDefaultTextures() {
            if (PixelTextureValue != null) {
                ReleaseTexture(PixelTextureValue);
                PixelTextureValue = null;
            }
            if (BlackPixelTextureValue != null) {
                ReleaseTexture(BlackPixelTextureValue);
                BlackPixelTextureValue = null;
            }
        }


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
