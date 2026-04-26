using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies asset import manager behavior for registered model importers and cached model assets.
    /// </summary>
    public class AssetImportManagerModelTests : IDisposable {
        /// <summary>
        /// Temporary project root used for each test.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Temporary project assets root used for source files and sidecars.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Temporary cache root used for imported asset outputs.
        /// </summary>
        readonly string CacheRootPath;

        /// <summary>
        /// Initializes isolated project directories for each test case.
        /// </summary>
        public AssetImportManagerModelTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-model-import-tests", Guid.NewGuid().ToString("N"));
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            CacheRootPath = Path.Combine(ProjectRootPath, "cache");
            Directory.CreateDirectory(AssetsRootPath);
            Directory.CreateDirectory(CacheRootPath);
        }

        /// <summary>
        /// Deletes temporary project directories after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures registered model extensions return registered model importer ids.
        /// </summary>
        [Fact]
        public void GetImporterIdsForExtension_WhenModelExtensionRegistered_ReturnsModelImporters() {
            AssetImportManager manager = CreateManager(new TestModelImporter());

            IReadOnlyList<string> importerIds = manager.GetImporterIdsForExtension(".obj");

            Assert.Equal(new[] { "test-model" }, importerIds);
            Assert.True(manager.IsModelExtension(".obj"));
        }

        /// <summary>
        /// Ensures unsupported extensions do not resolve importer ids.
        /// </summary>
        [Fact]
        public void GetImporterIdsForExtension_WhenExtensionUnsupported_ReturnsEmptyList() {
            AssetImportManager manager = CreateManager(new TestModelImporter());

            IReadOnlyList<string> importerIds = manager.GetImporterIdsForExtension(".wav");

            Assert.Empty(importerIds);
            Assert.False(manager.IsModelExtension(".wav"));
        }

        /// <summary>
        /// Ensures duplicate importer ids are rejected across importer categories.
        /// </summary>
        [Fact]
        public void RegisterModelImporter_WhenIdAlreadyRegisteredForTexture_Throws() {
            ContentManager contentManager = new ContentManager(AssetsRootPath);
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterTextureImporter(new TextureImporterRegistration("shared-id", new TestTextureImporter(), new[] { ".png" }));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                manager.RegisterModelImporter(new ModelImporterRegistration("shared-id", new TestModelImporter(), new[] { ".obj" })));
            Assert.Contains("shared-id", exception.Message);
        }

        /// <summary>
        /// Ensures importing a model source writes a cached model asset to disk.
        /// </summary>
        [Fact]
        public void ImportModel_WithRegisteredImporter_WritesCachedModelAsset() {
            string sourcePath = WriteSourceModel("triangle.obj");
            TestModelImporter modelImporter = new TestModelImporter();
            AssetImportManager manager = CreateManager(modelImporter);

            ModelAsset importedAsset = manager.ImportModel(sourcePath);
            string outputPath = Path.Combine(CacheRootPath, importedAsset.Id);

            Assert.True(File.Exists(outputPath));
            Assert.Equal(1, modelImporter.ImportCount);
            using (FileStream assetStream = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                ModelAsset cachedAsset = (ModelAsset)AssetSerializer.Deserialize(assetStream);
                Assert.Equal(importedAsset.Id, cachedAsset.Id);
                Assert.Equal(new ushort[] { 0, 1, 2 }, cachedAsset.Indices16);
            }
        }

        /// <summary>
        /// Ensures model assets are imported lazily when no cache file exists.
        /// </summary>
        [Fact]
        public void TryLoadModelAsset_WhenCacheMissing_ImportsModel() {
            string sourcePath = WriteSourceModel("lazy.obj");
            TestModelImporter modelImporter = new TestModelImporter();
            AssetImportManager manager = CreateManager(modelImporter);

            bool loaded = manager.TryLoadModelAsset(sourcePath, out ModelAsset asset);

            Assert.True(loaded);
            Assert.NotNull(asset);
            Assert.Equal(1, modelImporter.ImportCount);
            Assert.Equal(new ushort[] { 0, 1, 2 }, asset.Indices16);
        }

        /// <summary>
        /// Ensures valid cached model assets are reused without invoking the source importer.
        /// </summary>
        [Fact]
        public void TryLoadModelAsset_WhenCacheValid_LoadsCachedModel() {
            string sourcePath = WriteSourceModel("cached.obj");
            TestModelImporter modelImporter = new TestModelImporter();
            AssetImportManager manager = CreateManager(modelImporter);
            ModelAsset importedAsset = manager.ImportModel(sourcePath);

            bool loaded = manager.TryLoadModelAsset(sourcePath, out ModelAsset asset);

            Assert.True(loaded);
            Assert.NotNull(asset);
            Assert.Equal(1, modelImporter.ImportCount);
            Assert.Equal(importedAsset.Id, asset.Id);
        }

        /// <summary>
        /// Ensures invalid cached payloads are reported instead of being silently replaced.
        /// </summary>
        [Fact]
        public void TryLoadModelAsset_WhenCacheContainsTexture_Throws() {
            string sourcePath = WriteSourceModel("invalid-cache.obj");
            AssetImportManager manager = CreateManager(new TestModelImporter());
            AssetImportSettings settings = manager.LoadOrCreateImportSettings(sourcePath);
            string outputPath = Path.Combine(CacheRootPath, settings.AssetId);
            using (FileStream stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, new TextureAsset {
                    Id = settings.AssetId,
                    Width = 1,
                    Height = 1,
                    Colors = new byte[] { 255, 255, 255, 255 }
                });
            }

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                manager.TryLoadModelAsset(sourcePath, out _));
            Assert.Contains("ModelAsset", exception.Message);
        }

        /// <summary>
        /// Ensures startup scans import model sources that do not have cached assets yet.
        /// </summary>
        [Fact]
        public void ImportModelsMissingCache_WhenModelCacheMissing_ImportsModel() {
            string sourcePath = WriteSourceModel("startup.obj");
            TestModelImporter modelImporter = new TestModelImporter();
            AssetImportManager manager = CreateManager(modelImporter);
            AssetImportSettings settings = manager.LoadOrCreateImportSettings(sourcePath);

            List<string> importedAssets = manager.ImportModelsMissingCache();

            string outputPath = Path.Combine(CacheRootPath, settings.AssetId);
            Assert.Equal(new[] { outputPath }, importedAssets);
            Assert.Equal(1, modelImporter.ImportCount);
            Assert.True(File.Exists(outputPath));
        }

        /// <summary>
        /// Creates a configured asset import manager with a deterministic model importer.
        /// </summary>
        /// <param name="modelImporter">Model importer instance to register.</param>
        /// <returns>Configured asset import manager.</returns>
        AssetImportManager CreateManager(TestModelImporter modelImporter) {
            if (modelImporter == null) {
                throw new ArgumentNullException(nameof(modelImporter));
            }

            ContentManager contentManager = new ContentManager(AssetsRootPath);
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterModelImporter(new ModelImporterRegistration("test-model", modelImporter, new[] { ".obj" }));
            return manager;
        }

        /// <summary>
        /// Writes a minimal source model file for importer manager tests.
        /// </summary>
        /// <param name="fileName">Source file name to create inside the assets folder.</param>
        /// <returns>Absolute path to the source file.</returns>
        string WriteSourceModel(string fileName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("File name must be provided.", nameof(fileName));
            }

            string sourcePath = Path.Combine(AssetsRootPath, fileName);
            File.WriteAllText(sourcePath, "test model source");
            return sourcePath;
        }
    }
}
