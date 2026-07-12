using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies platform font settings are forwarded into font imports.
    /// </summary>
    public sealed class AssetImportManagerFontPlatformSettingsTests : IDisposable {
        /// <summary>
        /// Temporary project root used by each isolated test case.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Temporary assets root used by each isolated test case.
        /// </summary>
        readonly string AssetsRootPath;

        /// <summary>
        /// Initializes one isolated test workspace.
        /// </summary>
        public AssetImportManagerFontPlatformSettingsTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-asset-import-manager-font-platform-tests", Guid.NewGuid().ToString("N"));
            AssetsRootPath = Path.Combine(ProjectRootPath, "assets");
            Directory.CreateDirectory(AssetsRootPath);
        }

        /// <summary>
        /// Deletes the isolated test workspace after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures platform-specific font settings are forwarded into the selected font importer.
        /// </summary>
        [Fact]
        public void BuildFontAssetForPlatform_WhenPlatformFontSectionExists_ForwardsFontPixelSizeToImporter() {
            string sourcePath = Path.Combine(AssetsRootPath, "Fonts", "DemoBody.ttf");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath) ?? throw new InvalidOperationException("Font directory path could not be resolved."));
            File.WriteAllBytes(sourcePath, [0x01, 0x02, 0x03]);

            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(AssetsRootPath));
            RecordingFontImporter importer = new RecordingFontImporter();
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterFontImporter(new FontImporterRegistration("test-font", importer, [".ttf"]));

            AssetImportSettings settings = manager.LoadOrCreateImportSettings(sourcePath);
            AssetPlatformProcessorSettings dsSettings = new AssetPlatformProcessorSettings();
            AssetPlatformSettingsSectionRegistry.Shared.SetSection(dsSettings, "font", new FontAssetProcessorSettings {
                PixelSize = 11
            });
            AssetPlatformSettingsSectionRegistry.Shared.SetSection(dsSettings, "texture", new TextureAssetProcessorSettings {
                MaxResolution = 0,
                ColorFormat = TextureAssetColorFormat.Rgba32,
                AlphaPrecision = TextureAssetAlphaPrecision.A8
            });
            settings.Processor.Platforms["ds"] = dsSettings;
            manager.SaveImportSettings(sourcePath, settings);

            manager.BuildFontAssetForPlatform(sourcePath, "ds");

            Assert.NotNull(importer.LastSettings);
            Assert.Equal(11, importer.LastSettings.PixelSize);
        }

        /// <summary>
        /// Ensures platform-specific font-atlas texture settings are used instead of the generic texture section.
        /// </summary>
        [Fact]
        public void BuildFontAssetForPlatform_WhenPlatformFontAtlasTextureSectionExists_UsesFontAtlasTextureSettings() {
            string sourcePath = Path.Combine(AssetsRootPath, "Fonts", "DemoBody.ttf");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath) ?? throw new InvalidOperationException("Font directory path could not be resolved."));
            File.WriteAllBytes(sourcePath, [0x01, 0x02, 0x03]);

            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(AssetsRootPath));
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"]));

            AssetImportSettings settings = manager.LoadOrCreateImportSettings(sourcePath);
            AssetPlatformProcessorSettings dsSettings = new AssetPlatformProcessorSettings {
                Texture = new TextureAssetProcessorSettings {
                    MaxResolution = 0,
                    ColorFormat = TextureAssetColorFormat.Rgba4444,
                    AlphaPrecision = TextureAssetAlphaPrecision.A4
                },
                FontAtlasTexture = new TextureAssetProcessorSettings {
                    MaxResolution = 0,
                    ColorFormat = TextureAssetColorFormat.Indexed4,
                    AlphaPrecision = TextureAssetAlphaPrecision.Binary
                }
            };
            settings.Processor.Platforms["ds"] = dsSettings;
            manager.SaveImportSettings(sourcePath, settings);

            FontAsset fontAsset = manager.BuildFontAssetForPlatform(sourcePath, "ds");

            Assert.NotNull(fontAsset.SourceTextureAsset);
            Assert.Equal(TextureAssetColorFormat.Indexed4, fontAsset.SourceTextureAsset.ColorFormat);
            Assert.Equal(TextureAssetAlphaPrecision.Binary, fontAsset.SourceTextureAsset.AlphaPrecision);
        }

        /// <summary>
        /// Ensures Nintendo DS font imports fall back to DS-safe font-atlas settings even when only the generic texture section exists.
        /// </summary>
        [Fact]
        public void BuildFontAssetForPlatform_WhenDsFontAtlasTextureSectionIsMissing_UsesDsSafeFontAtlasDefaults() {
            string sourcePath = Path.Combine(AssetsRootPath, "Fonts", "DemoBody.ttf");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath) ?? throw new InvalidOperationException("Font directory path could not be resolved."));
            File.WriteAllBytes(sourcePath, [0x01, 0x02, 0x03]);

            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(AssetsRootPath));
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"]));

            AssetImportSettings settings = manager.LoadOrCreateImportSettings(sourcePath);
            AssetPlatformProcessorSettings dsSettings = new AssetPlatformProcessorSettings {
                Texture = new TextureAssetProcessorSettings {
                    MaxResolution = 0,
                    ColorFormat = TextureAssetColorFormat.Rgba32,
                    AlphaPrecision = TextureAssetAlphaPrecision.A8
                }
            };
            settings.Processor.Platforms["ds"] = dsSettings;
            manager.SaveImportSettings(sourcePath, settings);

            FontAsset fontAsset = manager.BuildFontAssetForPlatform(sourcePath, "ds");

            Assert.NotNull(fontAsset.SourceTextureAsset);
            Assert.Equal(TextureAssetColorFormat.Indexed4, fontAsset.SourceTextureAsset.ColorFormat);
            Assert.Equal(TextureAssetAlphaPrecision.Binary, fontAsset.SourceTextureAsset.AlphaPrecision);
        }
    }
}
