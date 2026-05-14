using helengine;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides a minimal 2D render manager that can materialize runtime textures for UI-oriented tests.
    /// </summary>
    internal class TestRenderManager2D : RenderManager2D {
        /// <summary>
        /// Gets the runtime textures released through this test renderer.
        /// </summary>
        public List<RuntimeTexture> ReleasedTextures { get; } = new List<RuntimeTexture>();

        /// <summary>
        /// Gets how many times production code requested one deferred-texture flush.
        /// </summary>
        public int FlushReleasedTexturesCallCount { get; private set; }

        /// <summary>
        /// Gets how many runtime textures were built from raw texture data.
        /// </summary>
        public int BuildTextureFromRawCallCount { get; private set; }

        /// <summary>
        /// Creates a runtime texture that mirrors the supplied raw texture dimensions.
        /// </summary>
        /// <param name="data">Raw texture data requested by the UI under test.</param>
        /// <returns>Minimal runtime texture carrying the requested dimensions.</returns>
        public override RuntimeTexture BuildTextureFromRaw(TextureAsset data) {
            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            BuildTextureFromRawCallCount++;
            return new TestRuntimeTexture {
                Width = data.Width,
                Height = data.Height
            };
        }

        /// <summary>
        /// Records one released runtime texture so tests can assert scene-owned asset disposal.
        /// </summary>
        /// <param name="texture">Runtime texture released by production code.</param>
        public override void ReleaseTexture(RuntimeTexture texture) {
            if (texture == null) {
                throw new ArgumentNullException(nameof(texture));
            }

            ReleasedTextures.Add(texture);
        }

        /// <summary>
        /// Records one deferred-texture flush request so tests can assert scene transitions flush releases before reloading.
        /// </summary>
        public override void FlushReleasedTextures() {
            FlushReleasedTexturesCallCount++;
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
