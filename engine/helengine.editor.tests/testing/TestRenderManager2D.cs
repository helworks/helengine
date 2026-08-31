using helengine;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides a minimal 2D render manager that can materialize runtime textures for UI-oriented tests.
    /// </summary>
    internal class TestRenderManager2D : RenderManager2D {
        readonly HashSet<RuntimeTexture> OwnedTextures = new HashSet<RuntimeTexture>();

        /// <summary>
        /// Gets the runtime textures released through this test renderer.
        /// </summary>
        public List<RuntimeTexture> ReleasedTextures { get; } = new List<RuntimeTexture>();

        /// <summary>
        /// Gets the runtime fonts released through this test renderer.
        /// </summary>
        public List<FontAsset> ReleasedFonts { get; } = new List<FontAsset>();

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
            RuntimeTexture texture = new TestRuntimeTexture {
                Width = data.Width,
                Height = data.Height
            };
            OwnedTextures.Add(texture);
            return texture;
        }

        /// <summary>
        /// Rejects texture updates for runtime textures created by another test renderer.
        /// </summary>
        /// <param name="texture">Runtime texture that should receive the update.</param>
        /// <param name="x">Destination rectangle X coordinate in pixels.</param>
        /// <param name="y">Destination rectangle Y coordinate in pixels.</param>
        /// <param name="width">Destination rectangle width in pixels.</param>
        /// <param name="height">Destination rectangle height in pixels.</param>
        /// <param name="rgba8">RGBA8 source pixels.</param>
        /// <param name="sourceRowPitch">Number of bytes between source rows.</param>
        protected override void UpdateTextureRegionCore(
            RuntimeTexture texture,
            int x,
            int y,
            int width,
            int height,
            [NativeNoEscape] byte[] rgba8,
            int sourceRowPitch) {
            if (!OwnedTextures.Contains(texture)) {
                throw new ArgumentException("Texture was not created by this renderer.", nameof(texture));
            }
        }

        /// <summary>
        /// Records one released runtime texture so tests can assert scene-owned asset disposal.
        /// </summary>
        /// <param name="texture">Runtime texture released by production code.</param>
        public override void ReleaseTexture(RuntimeTexture texture) {
            if (texture == null) {
                throw new ArgumentNullException(nameof(texture));
            }

            OwnedTextures.Remove(texture);
            ReleasedTextures.Add(texture);
            base.ReleaseTexture(texture);
        }

        /// <summary>
        /// Records one released font and then applies the shared release behavior.
        /// </summary>
        /// <param name="font">Runtime font released by production code.</param>
        public override void ReleaseFont(FontAsset font) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            ReleasedFonts.Add(font);
            base.ReleaseFont(font);
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
