using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies asset import manager behavior for legacy sidecars and cache files during project startup.
    /// </summary>
    public class AssetImportManagerTests : IDisposable {
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
        public AssetImportManagerTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-asset-import-tests", Guid.NewGuid().ToString("N"));
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            CacheRootPath = Path.Combine(ProjectRootPath, "cache");
            Directory.CreateDirectory(AssetsRootPath);
            Directory.CreateDirectory(CacheRootPath);
        }

        /// <summary>
        /// Deletes the temporary project directories after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a legacy non-HELE import-settings sidecar is rewritten and the texture cache is generated.
        /// </summary>
        [Fact]
        public void ImportTexturesMissingCache_WithLegacySettings_RewritesSettingsAndImportsTexture() {
            string sourcePath = WriteSourceTexture("legacy-settings.png");
            string settingsPath = sourcePath + ".hasset";
            File.WriteAllText(settingsPath, "legacy-settings");
            AssetImportManager manager = CreateManager();

            List<string> importedAssets = manager.ImportTexturesMissingCache();

            Assert.Single(importedAssets);
            using (FileStream settingsStream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                AssetImportSettings settings = AssetImportSettingsBinarySerializer.Deserialize(settingsStream);
                Assert.Equal("test-texture", settings.ImporterId);
                Assert.False(string.IsNullOrWhiteSpace(settings.SourceChecksum));
                Assert.False(string.IsNullOrWhiteSpace(settings.AssetId));
                Assert.Equal(Path.Combine(CacheRootPath, settings.AssetId), importedAssets[0]);
            }

            using (FileStream assetStream = new FileStream(importedAssets[0], FileMode.Open, FileAccess.Read, FileShare.Read)) {
                TextureAsset asset = (TextureAsset)AssetSerializer.Deserialize(assetStream);
                Assert.Equal((ushort)1, asset.Width);
                Assert.Equal((ushort)1, asset.Height);
                Assert.Equal(new byte[] { 255, 128, 64, 255 }, asset.Colors);
            }
        }

        /// <summary>
        /// Ensures a legacy non-HELE cached texture is treated as stale cache and rebuilt during startup import scanning.
        /// </summary>
        [Fact]
        public void ImportTexturesMissingCache_WithLegacyCachedAsset_ReimportsTexture() {
            string sourcePath = WriteSourceTexture("legacy-cache.png");
            AssetImportManager manager = CreateManager();
            AssetImportSettings settings = manager.LoadOrCreateImportSettings(sourcePath);
            manager.SaveImportSettings(sourcePath, settings);

            string outputPath = Path.Combine(CacheRootPath, settings.AssetId);
            File.WriteAllText(outputPath, "legacy-cache");

            List<string> importedAssets = manager.ImportTexturesMissingCache();

            Assert.Single(importedAssets);
            Assert.Equal(outputPath, importedAssets[0]);
            using (FileStream assetStream = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                TextureAsset asset = (TextureAsset)AssetSerializer.Deserialize(assetStream);
                Assert.Equal((ushort)1, asset.Width);
                Assert.Equal((ushort)1, asset.Height);
                Assert.Equal(new byte[] { 255, 128, 64, 255 }, asset.Colors);
            }
        }

        /// <summary>
        /// Creates an import manager configured with the deterministic test texture importer.
        /// </summary>
        /// <returns>Configured asset import manager.</returns>
        AssetImportManager CreateManager() {
            ContentManager contentManager = new ContentManager(AssetsRootPath);
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), new[] { ".png" }));
            return manager;
        }

        /// <summary>
        /// Writes a minimal source texture file for importer tests.
        /// </summary>
        /// <param name="fileName">Source file name to create inside the assets folder.</param>
        /// <returns>Absolute path to the source file.</returns>
        string WriteSourceTexture(string fileName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("File name must be provided.", nameof(fileName));
            }

            string sourcePath = Path.Combine(AssetsRootPath, fileName);
            File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4 });
            return sourcePath;
        }
    }
}
