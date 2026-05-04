using helengine;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides a minimal 2D render manager that can materialize runtime textures for UI-oriented tests.
    /// </summary>
    internal class TestRenderManager2D : RenderManager2D {
        /// <summary>
        /// Creates a runtime texture that mirrors the supplied raw texture dimensions.
        /// </summary>
        /// <param name="data">Raw texture data requested by the UI under test.</param>
        /// <returns>Minimal runtime texture carrying the requested dimensions.</returns>
        public override RuntimeTexture BuildTextureFromRaw(TextureAsset data) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            return new TestRuntimeTexture {
                Width = data.Width,
                Height = data.Height
            };
        }

        /// <summary>
        /// Ignores sprite draw calls because UI tests only need texture creation and layout wiring.
        /// </summary>
        /// <param name="sprite">Sprite draw request issued by the UI.</param>
        public override void DrawSprite(ISpriteDrawable2D sprite) {
        }

        /// <summary>
        /// Ignores text draw calls because UI tests only verify interaction behavior.
        /// </summary>
        /// <param name="text">Text draw request issued by the UI.</param>
        public override void DrawText(ITextDrawable2D text) {
        }

        /// <summary>
        /// Ignores rounded-rectangle draw calls because UI tests do not need raster output.
        /// </summary>
        /// <param name="shape">Rounded-rectangle draw request issued by the UI.</param>
        public override void DrawRoundedRect(IRoundedRectDrawable2D shape) {
        }
    }
}
