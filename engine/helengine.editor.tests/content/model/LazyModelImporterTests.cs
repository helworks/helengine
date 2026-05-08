using helengine.editor.tests.testing;

namespace helengine.editor.tests.content.model {
    /// <summary>
    /// Verifies that lazy model importers defer importer construction until the first import request.
    /// </summary>
    public sealed class LazyModelImporterTests {
        /// <summary>
        /// Ensures the lazy importer returns the richer model import result contract.
        /// </summary>
        [Fact]
        public void ImportModel_WhenCalled_ReturnsImportedModelAssetSet() {
            CountingModelImporterFactory factory = new CountingModelImporterFactory();
            LazyModelImporter importer = new LazyModelImporter(factory);

            using MemoryStream stream = new MemoryStream(new byte[] { 1 });
            ImportedModelAssetSet importedModel = importer.ImportModel(stream);

            Assert.NotNull(importedModel);
            Assert.NotNull(importedModel.ModelAsset);
            Assert.NotNull(importedModel.GeneratedMaterials);
            Assert.Single(importedModel.ModelAsset.Submeshes);
            Assert.Equal("Default", importedModel.ModelAsset.Submeshes[0].MaterialSlotName);
        }

        /// <summary>
        /// Ensures the wrapped importer factory is only invoked once, and only when a model import is requested.
        /// </summary>
        [Fact]
        public void ImportModel_WhenCalled_CreatesWrappedImporterOnlyOnFirstUse() {
            CountingModelImporterFactory factory = new CountingModelImporterFactory();
            LazyModelImporter importer = new LazyModelImporter(factory);

            Assert.Equal(0, factory.CreateCallCount);

            using MemoryStream firstStream = new MemoryStream(new byte[] { 1 });
            ImportedModelAssetSet firstAsset = importer.ImportModel(firstStream);

            Assert.Equal(1, factory.CreateCallCount);
            Assert.NotNull(firstAsset);

            using MemoryStream secondStream = new MemoryStream(new byte[] { 2 });
            ImportedModelAssetSet secondAsset = importer.ImportModel(secondStream);

            Assert.Equal(1, factory.CreateCallCount);
            Assert.Same(firstAsset, secondAsset);
        }
    }
}
