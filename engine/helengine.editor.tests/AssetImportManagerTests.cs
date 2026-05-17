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
            TextureAssetImportSettings settings = manager.LoadOrCreateTextureImportSettings(sourcePath);
            manager.SaveTextureImportSettings(sourcePath, settings);

            List<string> importedAssets = manager.ImportTexturesMissingCache();

            Assert.Single(importedAssets);
            using (FileStream settingsStream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                TextureAssetImportSettings loadedSettings = TextureAssetImportSettingsBinarySerializer.Deserialize(settingsStream);
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
        /// Ensures imported textures can be reloaded from the cache id after the cached file is deleted.
        /// </summary>
        [Fact]
        public void TryLoadImportedTextureAsset_WhenCacheFileIsMissing_RecreatesAndLoadsTexture() {
            string sourcePath = WriteSourceTexture("missing-imported-cache.png");
            AssetImportManager manager = CreateManager();
            TextureAsset importedAsset = manager.ImportTexture(sourcePath);
            string outputPath = Path.Combine(CacheRootPath, importedAsset.Id);

            File.Delete(outputPath);

            bool loaded = manager.TryLoadImportedTextureAsset(importedAsset.Id, out TextureAsset recoveredAsset);

            Assert.True(loaded);
            Assert.NotNull(recoveredAsset);
            Assert.True(File.Exists(outputPath));
            Assert.Equal((ushort)1, recoveredAsset.Width);
            Assert.Equal((ushort)1, recoveredAsset.Height);
            Assert.Equal(new byte[] { 255, 128, 64, 255 }, recoveredAsset.Colors);
        }

        /// <summary>
        /// Ensures stale texture import settings are regenerated in the current typed format instead of failing to load.
        /// </summary>
        [Fact]
        public void LoadOrCreateTextureImportSettings_WhenSettingsUseUnsupportedVersion_RecreatesDefaults() {
            string sourcePath = WriteSourceTexture("unsupported-settings-version.png");
            string settingsPath = sourcePath + ".hasset";
            AssetImportManager manager = CreateManager();
            TextureAssetImportSettings settings = manager.LoadOrCreateTextureImportSettings(sourcePath);

            using (FileStream stream = new FileStream(settingsPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                TextureAssetImportSettingsBinarySerializer.Serialize(stream, settings);
            }

            byte[] unsupportedVersionSettings = File.ReadAllBytes(settingsPath);
            unsupportedVersionSettings[5] = 2;
            File.WriteAllBytes(settingsPath, unsupportedVersionSettings);

            TextureAssetImportSettings recoveredSettings = manager.LoadOrCreateTextureImportSettings(sourcePath);

            Assert.Equal(settings.Importer.ImporterId, recoveredSettings.Importer.ImporterId);
            using (FileStream stream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                TextureAssetImportSettings savedSettings = TextureAssetImportSettingsBinarySerializer.Deserialize(stream);
                Assert.Equal(settings.Importer.ImporterId, savedSettings.Importer.ImporterId);
            }
        }

        /// <summary>
        /// Ensures loaded texture settings with a missing importer id are repaired to the registered default importer.
        /// </summary>
        [Fact]
        public void TryLoadOrCreateImportSettings_WhenTextureSettingsMissImporterId_RewritesDefaultImporter() {
            string sourcePath = WriteSourceTexture("missing-importer.tga");
            string settingsPath = sourcePath + ".hasset";
            AssetImportManager manager = CreateTgaManager();
            TextureAssetImportSettings settings = manager.LoadOrCreateTextureImportSettings(sourcePath);
            settings.Importer.ImporterId = string.Empty;
            manager.SaveTextureImportSettings(sourcePath, settings);

            TextureAssetImportSettings loadedSettings;
            bool loaded = manager.TryLoadOrCreateTextureImportSettings(sourcePath, out loadedSettings);

            Assert.True(loaded);
            Assert.Equal("pfim", loadedSettings.Importer.ImporterId);
            using (FileStream stream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                TextureAssetImportSettings savedSettings = TextureAssetImportSettingsBinarySerializer.Deserialize(stream);
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
            TextureAssetImportSettings settings = manager.LoadOrCreateTextureImportSettings(WriteSourceTexture("shared-extension.png"));

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

            TextureAssetImportSettings settings = manager.LoadOrCreateTextureImportSettings(sourcePath);
            settings.Importer.ImporterId = "magick";
            manager.SaveTextureImportSettings(sourcePath, settings);

            Assert.True(manager.TryLoadTextureAsset(sourcePath, out TextureAsset overriddenAsset));
            Assert.Equal(new byte[] { 9, 8, 7, 6 }, overriddenAsset.Colors);
        }

        /// <summary>
        /// Ensures texture import settings can cap the larger texture axis and preserve aspect ratio during cache generation.
        /// </summary>
        [Fact]
        public void TryLoadTextureAsset_WhenTextureMaxResolutionIsCapped_DownsizesWhilePreservingAspectRatio() {
            string sourcePath = WriteSourceTexture("checker.tga");
            ContentManager contentManager = new ContentManager(AssetsRootPath);
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterTextureImporter(new TextureImporterRegistration("pfim", new ConfigurableTextureImporter(1024, 512, new byte[1024 * 512 * 4]), new[] { ".tga" }));
            manager.CurrentPlatformId = "windows";

            TextureAssetImportSettings settings = manager.LoadOrCreateTextureImportSettings(sourcePath);
            settings.Importer.ImporterId = "pfim";
            settings.Processor.Platforms["windows"] = new TextureAssetProcessorSettings {
                MaxResolution = 256
            };
            manager.SaveTextureImportSettings(sourcePath, settings);

            bool loaded = manager.TryLoadTextureAsset(sourcePath, out TextureAsset asset);

            Assert.True(loaded);
            Assert.Equal((ushort)256, asset.Width);
            Assert.Equal((ushort)128, asset.Height);
        }

        /// <summary>
        /// Ensures changing the texture max-resolution processor setting produces a new cached asset identity.
        /// </summary>
        [Fact]
        public void TryLoadTextureAsset_WhenTextureMaxResolutionChanges_ReimportsWithANewAssetId() {
            string sourcePath = WriteSourceTexture("checker-id.tga");
            ContentManager contentManager = new ContentManager(AssetsRootPath);
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterTextureImporter(new TextureImporterRegistration("pfim", new ConfigurableTextureImporter(1024, 512, new byte[1024 * 512 * 4]), new[] { ".tga" }));
            manager.CurrentPlatformId = "windows";

            TextureAssetImportSettings settings = manager.LoadOrCreateTextureImportSettings(sourcePath);
            settings.Importer.ImporterId = "pfim";
            settings.Processor.Platforms["windows"] = new TextureAssetProcessorSettings {
                MaxResolution = 512,
                ColorFormat = TextureAssetColorFormat.Rgba32
            };
            manager.SaveTextureImportSettings(sourcePath, settings);
            Assert.True(manager.TryLoadTextureAsset(sourcePath, out _));
            string firstAssetId = manager.LoadOrCreateTextureImportSettings(sourcePath).Importer.AssetId;

            settings = manager.LoadOrCreateTextureImportSettings(sourcePath);
            settings.Processor.Platforms["windows"].MaxResolution = 128;
            manager.SaveTextureImportSettings(sourcePath, settings);
            Assert.True(manager.TryLoadTextureAsset(sourcePath, out _));
            string secondAssetId = manager.LoadOrCreateTextureImportSettings(sourcePath).Importer.AssetId;

            Assert.NotEqual(firstAssetId, secondAssetId);
        }

        /// <summary>
        /// Ensures missing texture sidecars are created as typed texture settings documents.
        /// </summary>
        [Fact]
        public void LoadOrCreateTextureImportSettings_WhenTextureSidecarMissing_ReturnsTypedDefaults() {
            string sourcePath = WriteSourceTexture("typed-defaults.tga");
            AssetImportManager manager = CreateTgaManager();

            TextureAssetImportSettings settings = manager.LoadOrCreateTextureImportSettings(sourcePath);

            Assert.Equal("pfim", settings.Importer.ImporterId);
            Assert.NotNull(settings.Processor);
            Assert.Empty(settings.Processor.Platforms);
        }

        /// <summary>
        /// Ensures typed texture sidecars drive cache identity changes when the texture processor settings change.
        /// </summary>
        [Fact]
        public void TryLoadTextureAsset_WhenTextureMaxResolutionChanges_ReimportsWithATypedSidecarAssetId() {
            string sourcePath = WriteSourceTexture("typed-asset-id.tga");
            AssetImportManager manager = CreateTgaManager();
            manager.CurrentPlatformId = "windows";

            TextureAssetImportSettings settings = manager.LoadOrCreateTextureImportSettings(sourcePath);
            settings.Processor.Platforms["windows"] = new TextureAssetProcessorSettings {
                MaxResolution = 512,
                ColorFormat = TextureAssetColorFormat.Rgba32
            };
            manager.SaveTextureImportSettings(sourcePath, settings);
            Assert.True(manager.TryLoadTextureAsset(sourcePath, out _));
            string firstAssetId = manager.LoadOrCreateTextureImportSettings(sourcePath).Importer.AssetId;

            settings = manager.LoadOrCreateTextureImportSettings(sourcePath);
            settings.Processor.Platforms["windows"].MaxResolution = 128;
            manager.SaveTextureImportSettings(sourcePath, settings);
            Assert.True(manager.TryLoadTextureAsset(sourcePath, out _));
            string secondAssetId = manager.LoadOrCreateTextureImportSettings(sourcePath).Importer.AssetId;

            Assert.NotEqual(firstAssetId, secondAssetId);
        }

        /// <summary>
        /// Ensures changing DS alpha precision invalidates the cached texture asset id.
        /// </summary>
        [Fact]
        public void TryLoadTextureAsset_WhenTextureAlphaPrecisionChanges_ReimportsWithANewAssetId() {
            string sourcePath = WriteSourceTexture("alpha-precision-cache-id.tga");
            AssetImportManager manager = CreateTgaManager();
            manager.CurrentPlatformId = "ds";

            TextureAssetImportSettings settings = manager.LoadOrCreateTextureImportSettings(sourcePath);
            settings.Processor.Platforms["ds"] = new TextureAssetProcessorSettings {
                MaxResolution = 256,
                ColorFormat = TextureAssetColorFormat.Indexed8,
                AlphaPrecision = TextureAssetAlphaPrecision.A4
            };
            manager.SaveTextureImportSettings(sourcePath, settings);
            Assert.True(manager.TryLoadTextureAsset(sourcePath, out _));
            string firstAssetId = manager.LoadOrCreateTextureImportSettings(sourcePath).Importer.AssetId;

            settings = manager.LoadOrCreateTextureImportSettings(sourcePath);
            settings.Processor.Platforms["ds"].AlphaPrecision = TextureAssetAlphaPrecision.A8;
            manager.SaveTextureImportSettings(sourcePath, settings);
            Assert.True(manager.TryLoadTextureAsset(sourcePath, out _));
            string secondAssetId = manager.LoadOrCreateTextureImportSettings(sourcePath).Importer.AssetId;

            Assert.NotEqual(firstAssetId, secondAssetId);
        }

        /// <summary>
        /// Ensures typed texture sidecars drive cache identity changes when the cooked texture format changes.
        /// </summary>
        [Fact]
        public void TryLoadTextureAsset_WhenTextureColorFormatChanges_ReimportsWithATypedSidecarAssetId() {
            string sourcePath = WriteSourceTexture("typed-format-id.tga");
            AssetImportManager manager = CreateTgaManager();
            manager.CurrentPlatformId = "ds";

            TextureAssetImportSettings settings = manager.LoadOrCreateTextureImportSettings(sourcePath);
            settings.Processor.Platforms["ds"] = new TextureAssetProcessorSettings {
                MaxResolution = 512,
                ColorFormat = TextureAssetColorFormat.Rgba32
            };
            manager.SaveTextureImportSettings(sourcePath, settings);
            Assert.True(manager.TryLoadTextureAsset(sourcePath, out _));
            string firstAssetId = manager.LoadOrCreateTextureImportSettings(sourcePath).Importer.AssetId;

            settings = manager.LoadOrCreateTextureImportSettings(sourcePath);
            settings.Processor.Platforms["ds"].ColorFormat = TextureAssetColorFormat.Rgba4444;
            manager.SaveTextureImportSettings(sourcePath, settings);
            Assert.True(manager.TryLoadTextureAsset(sourcePath, out _));
            string secondAssetId = manager.LoadOrCreateTextureImportSettings(sourcePath).Importer.AssetId;

            Assert.NotEqual(firstAssetId, secondAssetId);
        }

        /// <summary>
        /// Ensures changing the GameCube cooked texture format invalidates the cached texture asset id.
        /// </summary>
        [Fact]
        public void TryLoadTextureAsset_WhenGameCubeColorFormatChanges_ReimportsWithANewAssetId() {
            string sourcePath = WriteSourceTexture("gamecube-format-id.tga");
            AssetImportManager manager = CreateTgaManager();
            manager.CurrentPlatformId = "gamecube";

            TextureAssetImportSettings settings = manager.LoadOrCreateTextureImportSettings(sourcePath);
            settings.Processor.Platforms["gamecube"] = new TextureAssetProcessorSettings {
                MaxResolution = 256,
                ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8
            };
            manager.SaveTextureImportSettings(sourcePath, settings);
            Assert.True(manager.TryLoadTextureAsset(sourcePath, out _));
            string firstAssetId = manager.LoadOrCreateTextureImportSettings(sourcePath).Importer.AssetId;

            settings = manager.LoadOrCreateTextureImportSettings(sourcePath);
            settings.Processor.Platforms["gamecube"].ColorFormat = TextureAssetColorFormat.GxRgb5A3;
            manager.SaveTextureImportSettings(sourcePath, settings);
            Assert.True(manager.TryLoadTextureAsset(sourcePath, out _));
            string secondAssetId = manager.LoadOrCreateTextureImportSettings(sourcePath).Importer.AssetId;

            Assert.NotEqual(firstAssetId, secondAssetId);
        }

        /// <summary>
        /// Ensures Nintendo DS texture imports use the compact default texture budget even when no explicit per-platform override was authored yet.
        /// </summary>
        [Fact]
        public void TryLoadTextureAsset_WhenCurrentPlatformIsDsAndTextureSettingsAreMissing_UsesDsDefaultTextureBudget() {
            string sourcePath = WriteSourceTexture("typed-ds-default-budget.tga");
            ContentManager contentManager = new ContentManager(AssetsRootPath);
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterTextureImporter(new TextureImporterRegistration("pfim", new ConfigurableTextureImporter(1024, 512, new byte[1024 * 512 * 4]), new[] { ".tga" }));
            manager.CurrentPlatformId = "ds";

            bool loaded = manager.TryLoadTextureAsset(sourcePath, out TextureAsset asset);

            Assert.True(loaded);
            Assert.NotNull(asset);
            Assert.Equal((ushort)128, asset.Width);
            Assert.Equal((ushort)64, asset.Height);
            Assert.Equal(TextureAssetColorFormat.Rgba4444, asset.ColorFormat);
            Assert.Equal(128 * 64 * 2, asset.Colors.Length);
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
        /// Ensures font atlas textures honor platform texture processor settings before the cached font asset is written.
        /// </summary>
        [Fact]
        public void TryLoadFontAsset_WhenPlatformTextureColorFormatIsConfigured_CachesProcessedAtlasFormat() {
            string sourcePath = WriteSourceFont("demo-body-ds.ttf");
            AssetImportManager manager = CreateFontManager();
            manager.CurrentPlatformId = "ds";

            AssetImportSettings settings = manager.LoadOrCreateImportSettings(sourcePath);
            settings.Processor.Platforms["ds"] = new AssetPlatformProcessorSettings {
                Texture = new TextureAssetProcessorSettings {
                    MaxResolution = 0,
                    ColorFormat = TextureAssetColorFormat.Rgba4444
                },
                Model = new ModelAssetProcessorSettings(),
                Material = new MaterialAssetProcessorSettings()
            };
            manager.SaveImportSettings(sourcePath, settings);

            bool loaded = manager.TryLoadFontAsset(sourcePath, out FontAsset asset);

            Assert.True(loaded);
            Assert.NotNull(asset);
            Assert.Equal(TextureAssetColorFormat.Rgba4444, asset.SourceTextureAsset.ColorFormat);
            Assert.Equal(new byte[] { 0xFF, 0xFF }, asset.SourceTextureAsset.Colors);

            string outputPath = Path.Combine(CacheRootPath, manager.LoadOrCreateImportSettings(sourcePath).Importer.AssetId);
            using Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = ProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "version"));
            using FileStream stream = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            FontAsset cachedAsset = FilesFontAssetBinarySerializer.Deserialize(stream);
            Assert.Equal(TextureAssetColorFormat.Rgba4444, cachedAsset.SourceTextureAsset.ColorFormat);
            Assert.Equal(new byte[] { 0xFF, 0xFF }, cachedAsset.SourceTextureAsset.Colors);
        }

        /// <summary>
        /// Ensures Nintendo DS font imports use the compact default texture format even when no explicit per-platform override was authored yet.
        /// </summary>
        [Fact]
        public void TryLoadFontAsset_WhenCurrentPlatformIsDsAndTextureSettingsAreMissing_UsesDsDefaultTextureFormat() {
            string sourcePath = WriteSourceFont("demo-body-ds-default.ttf");
            AssetImportManager manager = CreateFontManager();
            manager.CurrentPlatformId = "ds";

            bool loaded = manager.TryLoadFontAsset(sourcePath, out FontAsset asset);

            Assert.True(loaded);
            Assert.NotNull(asset);
            Assert.Equal(TextureAssetColorFormat.Rgba4444, asset.SourceTextureAsset.ColorFormat);
            Assert.Equal(new byte[] { 0xFF, 0xFF }, asset.SourceTextureAsset.Colors);
        }

        /// <summary>
        /// Ensures Nintendo DS font imports cap oversized atlas textures to the default DS texture budget when no explicit override was authored yet.
        /// </summary>
        [Fact]
        public void TryLoadFontAsset_WhenCurrentPlatformIsDsAndTextureSettingsAreMissing_CapsAtlasToDsDefaultResolution() {
            string sourcePath = WriteSourceFont("demo-body-ds-default-budget.ttf");
            ContentManager contentManager = new ContentManager(AssetsRootPath);
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterFontImporter(new FontImporterRegistration("test-font", new ConfigurableFontImporter(256, 256, new byte[256 * 256 * 4]), new[] { ".ttf" }));
            manager.CurrentPlatformId = "ds";

            bool loaded = manager.TryLoadFontAsset(sourcePath, out FontAsset asset);

            Assert.True(loaded);
            Assert.NotNull(asset);
            Assert.Equal((ushort)128, asset.SourceTextureAsset.Width);
            Assert.Equal((ushort)128, asset.SourceTextureAsset.Height);
            Assert.Equal(TextureAssetColorFormat.Rgba4444, asset.SourceTextureAsset.ColorFormat);
            Assert.Equal(128 * 128 * 2, asset.SourceTextureAsset.Colors.Length);
        }

        /// <summary>
        /// Ensures font metrics follow the processed atlas size when per-platform texture settings resize the imported atlas.
        /// </summary>
        [Fact]
        public void TryLoadFontAsset_WhenPlatformTextureMaxResolutionResizesAtlas_RescalesFontMetrics() {
            string sourcePath = WriteSourceFont("demo-body-ds-resized-metrics.ttf");
            ContentManager contentManager = new ContentManager(AssetsRootPath);
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterFontImporter(new FontImporterRegistration("test-font", new ConfigurableFontImporter(256, 128, new byte[256 * 128 * 4]), new[] { ".ttf" }));
            manager.CurrentPlatformId = "ds";

            AssetImportSettings settings = manager.LoadOrCreateImportSettings(sourcePath);
            settings.Processor.Platforms["ds"] = new AssetPlatformProcessorSettings {
                Texture = new TextureAssetProcessorSettings {
                    MaxResolution = 128,
                    ColorFormat = TextureAssetColorFormat.Rgba32,
                    AlphaPrecision = TextureAssetAlphaPrecision.A8
                },
                Model = new ModelAssetProcessorSettings(),
                Material = new MaterialAssetProcessorSettings()
            };
            manager.SaveImportSettings(sourcePath, settings);

            bool loaded = manager.TryLoadFontAsset(sourcePath, out FontAsset asset);

            Assert.True(loaded);
            Assert.NotNull(asset);
            Assert.Equal((ushort)128, asset.SourceTextureAsset.Width);
            Assert.Equal((ushort)64, asset.SourceTextureAsset.Height);
            Assert.Equal(128, asset.AtlasWidth);
            Assert.Equal(64, asset.AtlasHeight);
            Assert.Equal(8f, asset.LineHeight);
            Assert.Equal(8, asset.FontInfo.LineSpacing);
            Assert.Equal(2f, asset.FontInfo.SpaceWidth);
        }

        /// <summary>
        /// Ensures platform texture format changes invalidate cached font asset ids when font atlases are rebuilt.
        /// </summary>
        [Fact]
        public void TryLoadFontAsset_WhenPlatformTextureColorFormatChanges_ReimportsWithANewAssetId() {
            string sourcePath = WriteSourceFont("demo-font-format-id.ttf");
            AssetImportManager manager = CreateFontManager();
            manager.CurrentPlatformId = "ds";

            AssetImportSettings settings = manager.LoadOrCreateImportSettings(sourcePath);
            settings.Processor.Platforms["ds"] = new AssetPlatformProcessorSettings {
                Texture = new TextureAssetProcessorSettings {
                    MaxResolution = 0,
                    ColorFormat = TextureAssetColorFormat.Rgba32
                },
                Model = new ModelAssetProcessorSettings(),
                Material = new MaterialAssetProcessorSettings()
            };
            manager.SaveImportSettings(sourcePath, settings);
            Assert.True(manager.TryLoadFontAsset(sourcePath, out _));
            string firstAssetId = manager.LoadOrCreateImportSettings(sourcePath).Importer.AssetId;

            settings = manager.LoadOrCreateImportSettings(sourcePath);
            settings.Processor.Platforms["ds"].Texture.ColorFormat = TextureAssetColorFormat.Rgba4444;
            manager.SaveImportSettings(sourcePath, settings);
            Assert.True(manager.TryLoadFontAsset(sourcePath, out _));
            string secondAssetId = manager.LoadOrCreateImportSettings(sourcePath).Importer.AssetId;

            Assert.NotEqual(firstAssetId, secondAssetId);
        }

        /// <summary>
        /// Ensures stale generic font settings are treated as missing and rebuilt when importing a ttf source.
        /// </summary>
        [Fact]
        public void ImportFont_WhenGenericSettingsSidecarIsStale_RebuildsSettingsAndImportsFont() {
            string sourcePath = WriteSourceFont("demo-body.ttf");
            string settingsPath = sourcePath + ".hasset";
            AssetImportManager manager = CreateFontManager();
            AssetImportSettings settings = manager.LoadOrCreateImportSettings(sourcePath);

            using (FileStream stream = new FileStream(settingsPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetImportSettingsBinarySerializer.Serialize(stream, settings);
            }

            byte[] corruptedSettings = File.ReadAllBytes(settingsPath);
            corruptedSettings[5] = 2;
            File.WriteAllBytes(settingsPath, corruptedSettings);

            FontAsset fontAsset = manager.ImportFont(sourcePath);

            Assert.NotNull(fontAsset);
            using (FileStream stream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                AssetImportSettings rebuiltSettings = AssetImportSettingsBinarySerializer.Deserialize(stream);
                Assert.Equal("test-font", rebuiltSettings.Importer.ImporterId);
            }
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
        /// Creates an import manager with the font importer required for ttf recovery tests.
        /// </summary>
        /// <returns>Configured asset import manager for ttf font coverage.</returns>
        AssetImportManager CreateFontManager() {
            ContentManager contentManager = new ContentManager(AssetsRootPath);
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), new[] { ".ttf" }));
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
