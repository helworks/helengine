using helengine.editor.tests.testing;

namespace helengine.editor.tests.content.model {
    /// <summary>
    /// Verifies that lazy model importers defer importer construction until the first import request.
    /// </summary>
    public sealed class LazyModelImporterTests {
        /// <summary>
        /// Ensures the wrapped importer factory is only invoked once, and only when a model import is requested.
        /// </summary>
        [Fact]
        public void ImportModel_WhenCalled_CreatesWrappedImporterOnlyOnFirstUse() {
            CountingModelImporterFactory factory = new CountingModelImporterFactory();
            LazyModelImporter importer = new LazyModelImporter(factory);

            Assert.Equal(0, factory.CreateCallCount);

            using MemoryStream firstStream = new MemoryStream(new byte[] { 1 });
            ModelAsset firstAsset = importer.ImportModel(firstStream);

            Assert.Equal(1, factory.CreateCallCount);
            Assert.NotNull(firstAsset);

            using MemoryStream secondStream = new MemoryStream(new byte[] { 2 });
            ModelAsset secondAsset = importer.ImportModel(secondStream);

            Assert.Equal(1, factory.CreateCallCount);
            Assert.Same(firstAsset, secondAsset);
        }
    }
}
