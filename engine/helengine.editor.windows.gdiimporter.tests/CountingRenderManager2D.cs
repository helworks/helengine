using helengine;

namespace helengine.editor.windows.gdiimporter.tests {
    /// <summary>
    /// Minimal 2D renderer that counts the atlas textures built on it, so a test can tell whether a font import used the
    /// session renderer or the headless managed atlas.
    /// </summary>
    public sealed class CountingRenderManager2D : RenderManager2D {
        /// <summary>
        /// Number of textures built from raw data; written by whichever thread imported.
        /// </summary>
        int BuildTextureFromRawCallCountValue;

        /// <summary>
        /// Gets the number of textures built from raw data.
        /// </summary>
        public int BuildTextureFromRawCallCount => Volatile.Read(ref BuildTextureFromRawCallCountValue);

        /// <summary>
        /// Counts the request and returns a managed texture with the requested dimensions.
        /// </summary>
        /// <param name="data">Raw atlas texture data.</param>
        /// <returns>Managed runtime texture standing in for a GPU texture.</returns>
        public override RuntimeTexture BuildTextureFromRaw(TextureAsset data) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            Interlocked.Increment(ref BuildTextureFromRawCallCountValue);
            return new ManagedRuntimeTexture {
                Width = data.Width,
                Height = data.Height
            };
        }

        /// <summary>
        /// Ignores texture region updates; font imports never update a texture region.
        /// </summary>
        /// <param name="texture">Runtime texture that would receive the update.</param>
        /// <param name="x">Destination rectangle X coordinate in pixels.</param>
        /// <param name="y">Destination rectangle Y coordinate in pixels.</param>
        /// <param name="width">Destination rectangle width in pixels.</param>
        /// <param name="height">Destination rectangle height in pixels.</param>
        /// <param name="rgba8">RGBA8 source pixels.</param>
        /// <param name="sourceRowPitch">Number of bytes between source rows.</param>
        protected override void UpdateTextureRegionCore(RuntimeTexture texture, int x, int y, int width, int height, byte[] rgba8, int sourceRowPitch) {
        }

        /// <summary>
        /// Ignores sprite draws; the importer never draws.
        /// </summary>
        /// <param name="sprite">Sprite draw request.</param>
        public override void DrawSprite(ISpriteDrawable2D sprite) {
        }

        /// <summary>
        /// Ignores text draws; the importer never draws.
        /// </summary>
        /// <param name="text">Text draw request.</param>
        public override void DrawText(ITextDrawable2D text) {
        }

        /// <summary>
        /// Ignores rounded-rectangle draws; the importer never draws.
        /// </summary>
        /// <param name="shape">Rounded-rectangle draw request.</param>
        public override void DrawRoundedRect(IRoundedRectDrawable2D shape) {
        }
    }
}
