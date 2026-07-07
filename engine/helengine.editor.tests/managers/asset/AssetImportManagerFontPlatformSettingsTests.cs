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
    }
}
