using helengine;

namespace helengine.core.tests.managers.rendering {
    /// <summary>Guards bounded readback validation before unsupported backends or native allocations are reached.</summary>
    public sealed class TextureReadbackTests {
        /// <summary>Rejects invalid origins, excessive rectangles, overflows and empty regions.</summary>
        [Theory]
        [InlineData(-1, 0, 1, 1)]
        [InlineData(0, -1, 1, 1)]
        [InlineData(0, 0, 0, 1)]
        [InlineData(0, 0, 1, 0)]
        [InlineData(0, 0, 129, 1)]
        [InlineData(0, 0, 1, 129)]
        [InlineData(255, 0, 2, 1)]
        [InlineData(0, 255, 1, 2)]
        [InlineData(int.MaxValue, 0, 1, 1)]
        public void InvalidRegionFailsBeforeBackend(int x, int y, int width, int height) {
            using var renderer = new Batch2DTestRenderManager();
            using var texture = renderer.CreateTexture(256, 256);
            Assert.Throws<ArgumentOutOfRangeException>(() => renderer.ReadTextureRegionAsync(texture, x, y, width, height));
        }

        /// <summary>Forbids reading a missing or retired source texture.</summary>
        [Fact]
        public void MissingOrDisposedSourceFailsBeforeBackend() {
            using var renderer = new Batch2DTestRenderManager();
            Assert.Throws<ArgumentNullException>(() => renderer.ReadTextureRegionAsync(null, 0, 0, 1, 1));
            using var texture = renderer.CreateTexture(1, 1);
            texture.Dispose();
            Assert.Throws<ObjectDisposedException>(() => renderer.ReadTextureRegionAsync(texture, 0, 0, 1, 1));
        }

        /// <summary>Existing renderers expose unsupported readback explicitly instead of returning synthetic pixels.</summary>
        [Fact]
        public void ValidMaximumRegionOnUnsupportedBackendFailsExplicitly() {
            using var renderer = new Batch2DTestRenderManager();
            using var texture = renderer.CreateTexture(256, 256);
            Assert.Throws<NotSupportedException>(() => renderer.ReadTextureRegionAsync(texture, 128, 128, 128, 128));
        }
    }
}
