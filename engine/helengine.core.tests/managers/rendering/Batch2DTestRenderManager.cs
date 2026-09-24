using helengine;

namespace helengine.core.tests.managers.rendering {
    /// <summary>
    /// Provides a headless renderer that creates dimensioned runtime textures for batch emitter tests.
    /// </summary>
    public sealed class Batch2DTestRenderManager : RenderManager2D {
        /// <summary>
        /// Creates a texture whose dimensions match the supplied CPU asset.
        /// </summary>
        /// <param name="data">Texture data whose size is represented by the test texture.</param>
        /// <returns>A managed runtime texture with matching dimensions.</returns>
        public override RuntimeTexture BuildTextureFromRaw(TextureAsset data) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            return CreateTexture(data.Width, data.Height);
        }

        /// <summary>
        /// Creates a runtime texture with explicit pixel dimensions.
        /// </summary>
        /// <param name="width">Texture width in pixels.</param>
        /// <param name="height">Texture height in pixels.</param>
        /// <returns>The created runtime texture.</returns>
        public RuntimeTexture CreateTexture(int width, int height) {
            return new TestRuntimeTexture { Width = width, Height = height };
        }

        /// <summary>Accepts no-op region updates because tests inspect geometry rather than texture contents.</summary>
        protected override void UpdateTextureRegionCore(RuntimeTexture texture, int x, int y, int width, int height,
            [NativeNoEscape] byte[] rgba8, int sourceRowPitch) {
        }

        /// <summary>Accepts sprite draws without requiring a graphics device.</summary>
        public override void DrawSprite(ISpriteDrawable2D sprite) {
        }

        /// <summary>Accepts text draws without requiring a graphics device.</summary>
        public override void DrawText(ITextDrawable2D text) {
        }

        /// <summary>Accepts rounded-rectangle draws without requiring a graphics device.</summary>
        public override void DrawRoundedRect(IRoundedRectDrawable2D shape) {
        }

        /// <summary>
        /// Represents a renderer-created texture using the base runtime texture's managed lifetime behavior.
        /// </summary>
        sealed class TestRuntimeTexture : RuntimeTexture {
        }
    }
}
