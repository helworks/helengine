using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies asset import manager behavior for current sidecars and cache files during project startup.
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
        /// Ensures a current import-settings sidecar is consumed and the texture cache is generated.
        /// </summary>
        [Fact]
        public void ImportTexturesMissingCache_WithCurrentSettings_ImportsTexture() {
            string sourcePath = WriteSourceTexture("current-settings.png");
            string settingsPath = sourcePath + ".hasset";
            AssetImportManager manager = CreateManager();
            AssetImportSettings settings = manager.LoadOrCreateImportSettings(sourcePath);
            manager.SaveImportSettings(sourcePath, settings);

            List<string> importedAssets = manager.ImportTexturesMissingCache();

            Assert.Single(importedAssets);
            using (FileStream settingsStream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                AssetImportSettings loadedSettings = AssetImportSettingsBinarySerializer.Deserialize(settingsStream);
                Assert.Equal("test-texture", loadedSettings.Importer.ImporterId);
                Assert.False(string.IsNullOrWhiteSpace(loadedSettings.Importer.SourceChecksum));
                Assert.False(string.IsNullOrWhiteSpace(loadedSettings.Importer.AssetId));
                Assert.Equal(Path.Combine(CacheRootPath, loadedSettings.Importer.AssetId), importedAssets[0]);
            }

            using (FileStream assetStream = new FileStream(importedAssets[0], FileMode.Open, FileAccess.Read, FileShare.Read)) {
                TextureAsset asset = (TextureAsset)AssetSerializer.Deserialize(assetStream);
                Assert.Equal((ushort)1, asset.Width);
                Assert.Equal((ushort)1, asset.Height);
                Assert.Equal(new byte[] { 255, 128, 64, 255 }, asset.Colors);
            }
        }

        /// <summary>
        /// Ensures an existing current cached texture is reused during startup import scanning.
        /// </summary>
        [Fact]
        public void ImportTexturesMissingCache_WithCurrentCachedAsset_SkipsImport() {
            string sourcePath = WriteSourceTexture("current-cache.png");
            AssetImportManager manager = CreateManager();
            TextureAsset importedAsset = manager.ImportTexture(sourcePath);
            string outputPath = Path.Combine(CacheRootPath, importedAsset.Id);

            List<string> importedAssets = manager.ImportTexturesMissingCache();

            Assert.Empty(importedAssets);
            using (FileStream assetStream = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                TextureAsset asset = (TextureAsset)AssetSerializer.Deserialize(assetStream);
                Assert.Equal((ushort)1, asset.Width);
                Assert.Equal((ushort)1, asset.Height);
                Assert.Equal(new byte[] { 255, 128, 64, 255 }, asset.Colors);
            }
        }

        /// <summary>
        /// Ensures unsupported asset-import-settings versions are rejected instead of being regenerated.
        /// </summary>
        [Fact]
        public void LoadOrCreateImportSettings_WhenSettingsUseUnsupportedVersion_Throws() {
            string sourcePath = WriteSourceTexture("unsupported-settings-version.png");
            string settingsPath = sourcePath + ".hasset";
            AssetImportManager manager = CreateManager();
            AssetImportSettings settings = manager.LoadOrCreateImportSettings(sourcePath);

            using (FileStream stream = new FileStream(settingsPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetImportSettingsBinarySerializer.Serialize(stream, settings);
            }

            byte[] unsupportedVersionSettings = File.ReadAllBytes(settingsPath);
            unsupportedVersionSettings[5] = 2;
            File.WriteAllBytes(settingsPath, unsupportedVersionSettings);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => manager.LoadOrCreateImportSettings(sourcePath));
            Assert.Contains("Unsupported asset import settings binary version", exception.Message);
        }

        /// <summary>
        /// Ensures loaded texture settings with a missing importer id are repaired to the registered default importer.
        /// </summary>
        [Fact]
        public void TryLoadOrCreateImportSettings_WhenTextureSettingsMissImporterId_RewritesDefaultImporter() {
            string sourcePath = WriteSourceTexture("missing-importer.tga");
            string settingsPath = sourcePath + ".hasset";
            AssetImportManager manager = CreateTgaManager();
            AssetImportSettings settings = manager.LoadOrCreateImportSettings(sourcePath);
            settings.Importer.ImporterId = string.Empty;
            manager.SaveImportSettings(sourcePath, settings);

            AssetImportSettings loadedSettings;
            bool loaded = manager.TryLoadOrCreateImportSettings(sourcePath, out loadedSettings);

            Assert.True(loaded);
            Assert.Equal("pfim", loadedSettings.Importer.ImporterId);
            using (FileStream stream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                AssetImportSettings savedSettings = AssetImportSettingsBinarySerializer.Deserialize(stream);
                Assert.Equal("pfim", savedSettings.Importer.ImporterId);
            }
        }

        /// <summary>
        /// Ensures multiple texture importers can register the same file extension while preserving the first importer as the default.
        /// </summary>
        [Fact]
        public void RegisterTextureImporter_WhenMultipleImportersShareExtension_RegistersAllForTheFormat() {
            ContentManager contentManager = new ContentManager(AssetsRootPath);
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterTextureImporter(new TextureImporterRegistration("first-texture", new TestTextureImporter(), new[] { ".png" }));
            manager.RegisterTextureImporter(new TextureImporterRegistration("second-texture", new TestTextureImporter(), new[] { ".png" }));

            IReadOnlyList<string> importerIds = manager.GetImporterIdsForExtension(".png");
            AssetImportSettings settings = manager.LoadOrCreateImportSettings(WriteSourceTexture("shared-extension.png"));

            Assert.Equal(new[] { "first-texture", "second-texture" }, importerIds);
            Assert.Equal("first-texture", settings.Importer.ImporterId);
        }

        /// <summary>
        /// Ensures importer options for a shared texture extension preserve registration order instead of alphabetical order.
        /// </summary>
        [Fact]
        public void GetImporterIdsForExtension_WhenMultipleTextureImportersShareExtension_PreservesRegistrationOrder() {
            ContentManager contentManager = new ContentManager(AssetsRootPath);
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterTextureImporter(new TextureImporterRegistration("z-default", new TestTextureImporter(), new[] { ".png" }));
            manager.RegisterTextureImporter(new TextureImporterRegistration("a-override", new TestTextureImporter(), new[] { ".png" }));

            IReadOnlyList<string> importerIds = manager.GetImporterIdsForExtension(".png");

            Assert.Equal(new[] { "z-default", "a-override" }, importerIds);
        }

        /// <summary>
        /// Ensures default texture importer selection rejects importers that were not registered for the requested extension.
        /// </summary>
        [Fact]
        public void SetDefaultTextureImporter_WhenImporterDoesNotSupportExtension_Throws() {
            ContentManager contentManager = new ContentManager(AssetsRootPath);
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterTextureImporter(new TextureImporterRegistration("png-importer", new TestTextureImporter(), new[] { ".png" }));
            manager.RegisterTextureImporter(new TextureImporterRegistration("dds-importer", new TestTextureImporter(), new[] { ".dds" }));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                manager.SetDefaultTextureImporter(".png", "dds-importer"));

            Assert.Equal("Texture importer 'dds-importer' does not support '.png'.", exception.Message);
        }

        /// <summary>
        /// Ensures switching an overlapping texture format to an explicit importer override reloads with that importer instead of serving stale cached bytes.
        /// </summary>
        [Fact]
        public void TryLoadTextureAsset_WhenTextureImporterOverrideChangesForSharedExtension_ReimportsWithTheSelectedImporter() {
            string sourcePath = WriteSourceTexture("shared-override.tga");
            ContentManager contentManager = new ContentManager(AssetsRootPath);
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterTextureImporter(new TextureImporterRegistration("pfim", new ConfigurableTextureImporter(new byte[] { 1, 2, 3, 4 }), new[] { ".tga" }));
            manager.RegisterTextureImporter(new TextureImporterRegistration("magick", new ConfigurableTextureImporter(new byte[] { 9, 8, 7, 6 }), new[] { ".tga" }));

            Assert.True(manager.TryLoadTextureAsset(sourcePath, out TextureAsset initialAsset));
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, initialAsset.Colors);

            AssetImportSettings settings = manager.LoadOrCreateImportSettings(sourcePath);
            settings.Importer.ImporterId = "magick";
            manager.SaveImportSettings(sourcePath, settings);

            Assert.True(manager.TryLoadTextureAsset(sourcePath, out TextureAsset overriddenAsset));
            Assert.Equal(new byte[] { 9, 8, 7, 6 }, overriddenAsset.Colors);
        }

        /// <summary>
        /// Ensures source font files import into cached font assets through the shared import manager.
        /// </summary>
        [Fact]
        public void TryLoadFontAsset_WhenSourceFontExists_ImportsAndCachesFontAsset() {
            string sourcePath = WriteSourceFont("demo-title.ttf");
            ContentManager contentManager = new ContentManager(AssetsRootPath);
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), new[] { ".ttf" }));

            bool loaded = manager.TryLoadFontAsset(sourcePath, out FontAsset asset);

            Assert.True(loaded);
            Assert.NotNull(asset);

            AssetImportSettings settings = manager.LoadOrCreateImportSettings(sourcePath);
            string outputPath = Path.Combine(CacheRootPath, settings.Importer.AssetId);
            Assert.True(File.Exists(outputPath));
        }

        /// <summary>
        /// Ensures the editor content manager registers the shared material processor needed by the material settings view.
        /// </summary>
        [Fact]
        public void EditorContentManagerConfiguration_WhenLoadingSerializedMaterial_RegistersEditorMaterialProcessor() {
            string materialPath = Path.Combine(AssetsRootPath, "demo.material");
            MaterialAsset materialAsset = new MaterialAsset {
                Id = "Materials/Demo.material",
                ShaderAssetId = "Shaders/Standard.shader"
            };
            using (FileStream stream = new FileStream(materialPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, materialAsset);
            }

            ContentManager contentManager = new ContentManager(AssetsRootPath);
            EditorContentManagerConfiguration.ConfigureEditorContentManager(contentManager);

            MaterialAsset loadedMaterialAsset = contentManager.Load<MaterialAsset>(materialPath, EditorContentProcessorIds.MaterialAsset);

            Assert.Equal(materialAsset.Id, loadedMaterialAsset.Id);
            Assert.Equal(materialAsset.ShaderAssetId, loadedMaterialAsset.ShaderAssetId);
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
        /// Creates an import manager with the texture importers required for .tga default selection tests.
        /// </summary>
        /// <returns>Configured asset import manager for .tga texture coverage.</returns>
        AssetImportManager CreateTgaManager() {
            ContentManager contentManager = new ContentManager(AssetsRootPath);
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterTextureImporter(new TextureImporterRegistration("pfim", new ConfigurableTextureImporter(new byte[] { 1, 2, 3, 4 }), new[] { ".tga" }));
            manager.RegisterTextureImporter(new TextureImporterRegistration("magick", new ConfigurableTextureImporter(new byte[] { 9, 8, 7, 6 }), new[] { ".tga" }));
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

        /// <summary>
        /// Writes a minimal source font file for importer tests.
        /// </summary>
        /// <param name="fileName">Source file name to create inside the assets folder.</param>
        /// <returns>Absolute path to the source file.</returns>
        string WriteSourceFont(string fileName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("File name must be provided.", nameof(fileName));
            }

            string sourcePath = Path.Combine(AssetsRootPath, fileName);
            File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3, 4 });
            return sourcePath;
        }
    }
}
