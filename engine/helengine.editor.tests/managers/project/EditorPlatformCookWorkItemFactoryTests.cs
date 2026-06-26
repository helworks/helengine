using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;
using Xunit;

namespace helengine.editor.tests.managers.project {
    /// <summary>
    /// Verifies builder-owned texture work items preserve the full shared texture settings contract.
    /// </summary>
    public sealed class EditorPlatformCookWorkItemFactoryTests : IDisposable {
        /// <summary>
        /// Temporary project root used to host test source assets.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes one isolated project root with a builder-owned texture source file.
        /// </summary>
        public EditorPlatformCookWorkItemFactoryTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-platform-cook-work-item-factory-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Images"));
            File.WriteAllBytes(Path.Combine(ProjectRootPath, "assets", "Images", "logo.png"), [0, 1, 2, 3]);
        }

        /// <summary>
        /// Deletes the isolated project root after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures builder-owned texture work items serialize the selected indexing method for indexed formats.
        /// </summary>
        [Fact]
        public void CreateTextureWorkItem_WhenIndexedSettingsAreProvided_SerializesIndexingMethod() {
            PlatformCookWorkItem workItem = EditorPlatformCookWorkItemFactory.CreateTextureWorkItem(
                CreateTexturePlatformDefinition(),
                "ds",
                ProjectRootPath,
                "Images/logo.png",
                "COOKED/I/LOGO.HAS",
                CreateTextureImportSettings(TextureAssetColorFormat.Indexed8, TextureAssetIndexingMethod.QuantizedIndexed.ToString()),
                new AssetFileHasher());

            Assert.Contains("\"indexingMethod\":\"QuantizedIndexed\"", workItem.SerializedPlatformSettings);
        }

        /// <summary>
        /// Ensures builder-owned font-atlas texture work items serialize the dedicated font-atlas texture settings instead of the generic image-texture settings.
        /// </summary>
        [Fact]
        public void CreateGeneratedFontAtlasTextureWorkItem_WhenFontAtlasSettingsAreProvided_SerializesFontAtlasTextureSection() {
            string sourceAtlasPath = Path.Combine(ProjectRootPath, "assets", "Images", "logo.png");
            PlatformCookWorkItem workItem = EditorPlatformCookWorkItemFactory.CreateGeneratedFontAtlasTextureWorkItem(
                CreateFontAtlasTexturePlatformDefinition(),
                "ds",
                sourceAtlasPath,
                "cooked/fonts/demodiscbody.hetex",
                "fonts/demodiscbody",
                CreateFontImportSettings(),
                new AssetFileHasher());

            Assert.Contains("\"colorFormat\":\"Indexed4\"", workItem.SerializedPlatformSettings);
            Assert.Contains("\"alphaPrecision\":\"Binary\"", workItem.SerializedPlatformSettings);
        }

        /// <summary>
        /// Ensures builder-owned font-atlas texture work items fall back to the platform capability defaults when the asset does not define one explicit font-atlas texture section.
        /// </summary>
        [Fact]
        public void CreateGeneratedFontAtlasTextureWorkItem_WhenFontAtlasSectionIsMissing_UsesCapabilityDefaults() {
            string sourceAtlasPath = Path.Combine(ProjectRootPath, "assets", "Images", "logo.png");
            AssetImportSettings settings = new AssetImportSettings();
            settings.Importer.ImporterId = "gdi-font";
            settings.Importer.AssetId = "fonts/demodiscbody";
            settings.Processor.Platforms["ds"] = new AssetPlatformProcessorSettings {
                Texture = new TextureAssetProcessorSettings {
                    MaxResolution = 256,
                    ColorFormat = TextureAssetColorFormat.Rgba32,
                    AlphaPrecision = TextureAssetAlphaPrecision.A8
                }
            };

            PlatformCookWorkItem workItem = EditorPlatformCookWorkItemFactory.CreateGeneratedFontAtlasTextureWorkItem(
                CreateFontAtlasTexturePlatformDefinition(),
                "ds",
                sourceAtlasPath,
                "cooked/fonts/demodiscbody.hetex",
                "fonts/demodiscbody",
                settings,
                new AssetFileHasher());

            Assert.Contains("\"colorFormat\":\"Indexed4\"", workItem.SerializedPlatformSettings);
            Assert.Contains("\"alphaPrecision\":\"Binary\"", workItem.SerializedPlatformSettings);
        }

        /// <summary>
        /// Creates one platform definition that publishes a builder-owned indexed texture capability.
        /// </summary>
        /// <returns>Platform definition for the test platform.</returns>
        PlatformDefinition CreateTexturePlatformDefinition() {
            return new PlatformDefinition(
                "ds",
                "Nintendo DS",
                Array.Empty<PlatformBuildProfileDefinition>(),
                Array.Empty<PlatformGraphicsProfileDefinition>(),
                Array.Empty<PlatformAssetRequirementDefinition>(),
                Array.Empty<PlatformMaterialSchemaDefinition>(),
                Array.Empty<PlatformComponentSupportRule>(),
                Array.Empty<PlatformCodegenProfileDefinition>(),
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>(),
                assetCookCapabilities: [
                    new PlatformAssetCookCapabilityDefinition(
                        "texture",
                        "runtime-texture",
                        PlatformAssetCookOwnershipKind.BuilderOwned,
                        "ds-texture",
                        textureFormatCapabilities: new PlatformTextureFormatCapabilityDefinition(
                            [TextureAssetColorFormat.Indexed8.ToString()],
                            [TextureAssetAlphaPrecision.A8],
                            [new PlatformTextureFormatCombinationDefinition(TextureAssetColorFormat.Indexed8.ToString(), TextureAssetAlphaPrecision.A8)]))
                ]);
        }

        /// <summary>
        /// Creates one platform definition that publishes a builder-owned indexed font-atlas texture capability.
        /// </summary>
        /// <returns>Platform definition for the test platform.</returns>
        PlatformDefinition CreateFontAtlasTexturePlatformDefinition() {
            return new PlatformDefinition(
                "ds",
                "Nintendo DS",
                Array.Empty<PlatformBuildProfileDefinition>(),
                Array.Empty<PlatformGraphicsProfileDefinition>(),
                Array.Empty<PlatformAssetRequirementDefinition>(),
                Array.Empty<PlatformMaterialSchemaDefinition>(),
                Array.Empty<PlatformComponentSupportRule>(),
                Array.Empty<PlatformCodegenProfileDefinition>(),
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>(),
                assetCookCapabilities: [
                    new PlatformAssetCookCapabilityDefinition(
                        "font-atlas-texture",
                        "runtime-texture",
                        PlatformAssetCookOwnershipKind.BuilderOwned,
                        "ds-font-atlas-texture",
                        "{\"maxResolution\":128,\"colorFormat\":\"Indexed4\",\"alphaPrecision\":\"Binary\"}",
                        textureFormatCapabilities: new PlatformTextureFormatCapabilityDefinition(
                            [TextureAssetColorFormat.Indexed4.ToString()],
                            [TextureAssetAlphaPrecision.Binary],
                            [new PlatformTextureFormatCombinationDefinition(TextureAssetColorFormat.Indexed4.ToString(), TextureAssetAlphaPrecision.Binary)]))
                ]);
        }

        /// <summary>
        /// Creates one texture import settings document for the indexed work-item contract test.
        /// </summary>
        /// <param name="colorFormat">Texture format that should be serialized into the work item.</param>
        /// <param name="indexingMethodId">Indexing method that should be serialized into the work item.</param>
        /// <returns>Configured texture import settings document.</returns>
        TextureAssetImportSettings CreateTextureImportSettings(TextureAssetColorFormat colorFormat, string indexingMethodId) {
            TextureAssetImportSettings settings = new TextureAssetImportSettings();
            settings.Importer.ImporterId = "pfim";
            settings.Importer.AssetId = "images/logo";
            settings.Processor.Platforms["ds"] = new TextureAssetProcessorSettings {
                MaxResolution = 128,
                ColorFormat = colorFormat,
                AlphaPrecision = TextureAssetAlphaPrecision.A8,
                IndexingMethodId = indexingMethodId
            };
            return settings;
        }

        /// <summary>
        /// Creates one font import settings document whose generic texture settings differ from the dedicated font-atlas texture settings.
        /// </summary>
        /// <returns>Configured font import settings document.</returns>
        AssetImportSettings CreateFontImportSettings() {
            AssetImportSettings settings = new AssetImportSettings();
            settings.Importer.ImporterId = "gdi-font";
            settings.Importer.AssetId = "fonts/demodiscbody";
            settings.Processor.Platforms["ds"] = new AssetPlatformProcessorSettings {
                Texture = new TextureAssetProcessorSettings {
                    MaxResolution = 256,
                    ColorFormat = TextureAssetColorFormat.Rgba32,
                    AlphaPrecision = TextureAssetAlphaPrecision.A8
                },
                FontAtlasTexture = new TextureAssetProcessorSettings {
                    MaxResolution = 128,
                    ColorFormat = TextureAssetColorFormat.Indexed4,
                    AlphaPrecision = TextureAssetAlphaPrecision.Binary
                }
            };
            return settings;
        }
    }
}
