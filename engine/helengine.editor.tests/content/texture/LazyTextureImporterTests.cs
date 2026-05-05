using helengine.editor.tests.testing;

namespace helengine.editor.tests.content.texture {
    /// <summary>
    /// Verifies that lazy texture importers defer importer construction until the first import request.
    /// </summary>
    public sealed class LazyTextureImporterTests {
        /// <summary>
        /// Ensures the wrapped importer factory is only invoked once, and only when a texture import is requested.
        /// </summary>
        [Fact]
        public void ImportTexture_WhenCalled_CreatesWrappedImporterOnlyOnFirstUse() {
            CountingTextureImporterFactory factory = new CountingTextureImporterFactory();
            LazyTextureImporter importer = new LazyTextureImporter(factory);

            Assert.Equal(0, factory.CreateCallCount);

            using MemoryStream firstStream = new MemoryStream(new byte[] { 1 });
            TextureAsset firstAsset = importer.ImportTexture(firstStream);

            Assert.Equal(1, factory.CreateCallCount);
            Assert.Equal((ushort)1, firstAsset.Width);
            Assert.Equal((ushort)1, firstAsset.Height);

            using MemoryStream secondStream = new MemoryStream(new byte[] { 2 });
            TextureAsset secondAsset = importer.ImportTexture(secondStream);

            Assert.Equal(1, factory.CreateCallCount);
            Assert.Equal(firstAsset.Colors, secondAsset.Colors);
        }
    }
}
