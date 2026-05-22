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
            Assert.True(manager.IsModelExtension(".x"));
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
        /// Ensures importer-generated material assets are written next to the source model using their deterministic relative paths.
        /// </summary>
        [Fact]
        public void ImportModel_WhenImporterReturnsGeneratedMaterials_WritesSiblingHassetAssets() {
            string sourcePath = WriteSourceModel("sponza.obj");
            TestModelImporter modelImporter = new TestModelImporter {
                GeneratedMaterials = new[] {
                    CreateGeneratedMaterial("Fabric", "sponza/Fabric.hasset", "Textures/Fabric.png"),
                    CreateGeneratedMaterial("Wood", "sponza/Wood.hasset", "Textures/Wood.png")
                }
            };
            AssetImportManager manager = CreateManager(modelImporter);

            manager.ImportModel(sourcePath);

            string firstMaterialPath = Path.Combine(AssetsRootPath, "sponza", "Fabric.hasset");
            string secondMaterialPath = Path.Combine(AssetsRootPath, "sponza", "Wood.hasset");
            MaterialAssetSettingsService settingsService = new MaterialAssetSettingsService();
            Assert.True(File.Exists(firstMaterialPath));
            Assert.True(File.Exists(secondMaterialPath));
            Assert.Equal("Textures/Fabric.png", settingsService.LoadMaterialAsset(firstMaterialPath, "windows").DiffuseTextureAssetId);
            Assert.Equal("Textures/Wood.png", settingsService.LoadMaterialAsset(secondMaterialPath, "windows").DiffuseTextureAssetId);
        }

        /// <summary>
        /// Ensures reimporting a model rewrites previously generated material assets in place.
        /// </summary>
        [Fact]
        public void ImportModel_WhenReimportingGeneratedMaterials_UpdatesExistingHassetInPlace() {
            string sourcePath = WriteSourceModel("sponza.obj");
            TestModelImporter modelImporter = new TestModelImporter {
                GeneratedMaterials = new[] {
                    CreateGeneratedMaterial("Fabric", "sponza/Fabric.hasset", "Textures/FabricA.png")
                }
            };
            AssetImportManager manager = CreateManager(modelImporter);

            manager.ImportModel(sourcePath);
            modelImporter.GeneratedMaterials = new[] {
                CreateGeneratedMaterial("Fabric", "sponza/Fabric.hasset", "Textures/FabricB.png")
            };

            manager.ImportModel(sourcePath);

            string materialPath = Path.Combine(AssetsRootPath, "sponza", "Fabric.hasset");
            MaterialAssetSettingsService settingsService = new MaterialAssetSettingsService();
            Assert.True(File.Exists(materialPath));
            Assert.Equal("Textures/FabricB.png", settingsService.LoadMaterialAsset(materialPath, "windows").DiffuseTextureAssetId);
        }

        /// <summary>
        /// Ensures platform-specific model processor settings flip triangle winding during model import.
        /// </summary>
        [Fact]
        public void ImportModel_WhenWindowsProcessorSettingsFlipWinding_ReversesTriangleIndices() {
            string sourcePath = WriteSourceModel("flip-winding.obj");
            TestModelImporter modelImporter = new TestModelImporter();
            AssetImportManager manager = CreateManager(modelImporter);
            ModelAssetImportSettings settings = manager.LoadOrCreateModelImportSettings(sourcePath);
            settings.Processor.Platforms["windows"] = new ModelAssetProcessorSettings {
                FlipWinding = true
            };
            manager.SaveModelImportSettings(sourcePath, settings);

            ModelAsset importedAsset = manager.ImportModel(sourcePath);

            Assert.Equal(new ushort[] { 0, 2, 1 }, importedAsset.Indices16);
        }

        /// <summary>
        /// Ensures missing model sidecars are created as typed model settings documents.
        /// </summary>
        [Fact]
        public void LoadOrCreateModelImportSettings_WhenModelSidecarMissing_ReturnsTypedDefaults() {
            string sourcePath = WriteSourceModel("typed-defaults.obj");
            AssetImportManager manager = CreateManager(new TestModelImporter());

            ModelAssetImportSettings settings = manager.LoadOrCreateModelImportSettings(sourcePath);

            Assert.Equal("test-model", settings.Importer.ImporterId);
            Assert.NotNull(settings.Processor);
            Assert.Empty(settings.Processor.Platforms);
        }

        /// <summary>
        /// Ensures typed model sidecars drive the platform-specific model processor path.
        /// </summary>
        [Fact]
        public void ImportModel_WhenWindowsProcessorSettingsFlipWinding_UsesTypedModelSettings() {
            string sourcePath = WriteSourceModel("typed-flip.obj");
            AssetImportManager manager = CreateManager(new TestModelImporter());
            ModelAssetImportSettings settings = manager.LoadOrCreateModelImportSettings(sourcePath);
            settings.Processor.Platforms["windows"] = new ModelAssetProcessorSettings {
                FlipWinding = true
            };
            manager.SaveModelImportSettings(sourcePath, settings);

            ModelAsset importedAsset = manager.ImportModel(sourcePath);

            Assert.Equal(new ushort[] { 0, 2, 1 }, importedAsset.Indices16);
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
        /// Ensures changing model processor settings invalidates the cached processed model output.
        /// </summary>
        [Fact]
        public void TryLoadModelAsset_WhenWindowsFlipWindingChanges_ReimportsModel() {
            string sourcePath = WriteSourceModel("processor-change.obj");
            TestModelImporter modelImporter = new TestModelImporter();
            AssetImportManager manager = CreateManager(modelImporter);
            ModelAssetImportSettings settings = manager.LoadOrCreateModelImportSettings(sourcePath);
            settings.Processor.Platforms["windows"] = new ModelAssetProcessorSettings {
                FlipWinding = false
            };
            manager.SaveModelImportSettings(sourcePath, settings);

            ModelAsset firstAsset = manager.ImportModel(sourcePath);
            settings = manager.LoadOrCreateModelImportSettings(sourcePath);
            settings.Processor.Platforms["windows"].FlipWinding = true;
            manager.SaveModelImportSettings(sourcePath, settings);

            bool loaded = manager.TryLoadModelAsset(sourcePath, out ModelAsset secondAsset);

            Assert.True(loaded);
            Assert.NotNull(secondAsset);
            Assert.Equal(new ushort[] { 0, 1, 2 }, firstAsset.Indices16);
            Assert.Equal(new ushort[] { 0, 2, 1 }, secondAsset.Indices16);
            Assert.Equal(2, modelImporter.ImportCount);
        }

        /// <summary>
        /// Ensures stale cached model assets with an older payload version are deleted and regenerated.
        /// </summary>
        [Fact]
        public void TryLoadModelAsset_WhenCacheUsesUnsupportedVersion_ReimportsModel() {
            string sourcePath = WriteSourceModel("stale-cache.obj");
            TestModelImporter modelImporter = new TestModelImporter();
            AssetImportManager manager = CreateManager(modelImporter);
            ModelAssetImportSettings settings = manager.LoadOrCreateModelImportSettings(sourcePath);
            string outputPath = Path.Combine(CacheRootPath, settings.Importer.AssetId);

            using (FileStream stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, new ModelAsset {
                    Id = settings.Importer.AssetId,
                    Positions = new[] { float3.Zero },
                    Normals = new[] { new float3(0f, 1f, 0f) },
                    TexCoords = new[] { new float2(0f, 0f) },
                    Indices16 = new ushort[] { 0 }
                });
            }

            byte[] staleCache = File.ReadAllBytes(outputPath);
            staleCache[5] = 0;
            File.WriteAllBytes(outputPath, staleCache);

            bool loaded = manager.TryLoadModelAsset(sourcePath, out ModelAsset asset);

            Assert.True(loaded);
            Assert.NotNull(asset);
            Assert.Equal(1, modelImporter.ImportCount);
            Assert.Equal(new ushort[] { 0, 1, 2 }, asset.Indices16);
        }

        /// <summary>
        /// Ensures invalid cached payloads are reported instead of being silently replaced.
        /// </summary>
        [Fact]
        public void TryLoadModelAsset_WhenCacheContainsTexture_Throws() {
            string sourcePath = WriteSourceModel("invalid-cache.obj");
            AssetImportManager manager = CreateManager(new TestModelImporter());
            ModelAssetImportSettings settings = manager.LoadOrCreateModelImportSettings(sourcePath);
            string outputPath = Path.Combine(CacheRootPath, settings.Importer.AssetId);
            using (FileStream stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, new TextureAsset {
                    Id = settings.Importer.AssetId,
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
            ModelAssetImportSettings settings = manager.LoadOrCreateModelImportSettings(sourcePath);

            List<string> importedAssets = manager.ImportModelsMissingCache();

            string outputPath = Path.Combine(CacheRootPath, settings.Importer.AssetId);
            Assert.Equal(new[] { outputPath }, importedAssets);
            Assert.Equal(1, modelImporter.ImportCount);
            Assert.True(File.Exists(outputPath));
        }

        /// <summary>
        /// Ensures startup cache scans log one failing model import and continue importing later models.
        /// </summary>
        [Fact]
        public void ImportModelsMissingCache_WhenOneModelImportFails_LogsErrorAndContinues() {
            string brokenSourcePath = WriteSourceModel("broken.obj", "broken marker");
            string validSourcePath = WriteSourceModel("valid.obj", "valid model");
            ConditionalThrowingModelImporter modelImporter = new ConditionalThrowingModelImporter("broken marker", "broken import");
            AssetImportManager manager = CreateManager(modelImporter);
            ModelAssetImportSettings brokenSettings = manager.LoadOrCreateModelImportSettings(brokenSourcePath);
            ModelAssetImportSettings validSettings = manager.LoadOrCreateModelImportSettings(validSourcePath);
            List<LogEntry> loggedErrors = new List<LogEntry>();
            Action<LogEntry> handleErrorLogged = loggedErrors.Add;

            Logger.ErrorLogged += handleErrorLogged;
            try {
                List<string> importedAssets = manager.ImportModelsMissingCache();

                string brokenOutputPath = Path.Combine(CacheRootPath, brokenSettings.Importer.AssetId);
                string validOutputPath = Path.Combine(CacheRootPath, validSettings.Importer.AssetId);
                Assert.Equal(new[] { validOutputPath }, importedAssets);
                Assert.False(File.Exists(brokenOutputPath));
                Assert.True(File.Exists(validOutputPath));
                Assert.Equal(2, modelImporter.ImportCount);
                Assert.Single(loggedErrors);
                Assert.Contains("broken.obj", loggedErrors[0].Message);
                Assert.Contains("broken import", loggedErrors[0].Message);
            } finally {
                Logger.ErrorLogged -= handleErrorLogged;
            }
        }

        /// <summary>
        /// Ensures startup model-cache scans do not overwrite authored material documents or platform override files.
        /// </summary>
        [Fact]
        public void ImportModelsMissingCache_WhenMaterialAssetSidecarExists_DoesNotRewriteMaterialSettings() {
            string materialSourcePath = WriteMaterialAsset("Generated/Cube00.hasset");
            AssetImportManager manager = CreateManager(new TestModelImporter());
            MaterialAssetImportSettings expectedSettings = CreateMaterialImportSettings("Materials/Generated/Cube00.hasset", "#FF4040FF");
            MaterialAssetSettingsService settingsService = new MaterialAssetSettingsService();

            settingsService.Save(materialSourcePath, expectedSettings);

            List<string> importedAssets = manager.ImportModelsMissingCache();

            Assert.Empty(importedAssets);
            Assert.True(settingsService.TryLoadPlatformSettings(materialSourcePath, "windows", out MaterialAssetProcessorSettings actualSettings));
            Assert.True(File.Exists(materialSourcePath));
            Assert.Equal(expectedSettings.Processor.Platforms["windows"].SchemaId, actualSettings.SchemaId);
            Assert.Equal(
                expectedSettings.Processor.Platforms["windows"].FieldValues["base-color"],
                actualSettings.FieldValues["base-color"]);
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
            manager.RegisterModelImporter(new ModelImporterRegistration("test-model", modelImporter, new[] { ".obj", ".x" }));
            return manager;
        }

        /// <summary>
        /// Creates a configured asset import manager with one conditional model importer.
        /// </summary>
        /// <param name="modelImporter">Model importer instance to register.</param>
        /// <returns>Configured asset import manager.</returns>
        AssetImportManager CreateManager(ConditionalThrowingModelImporter modelImporter) {
            if (modelImporter == null) {
                throw new ArgumentNullException(nameof(modelImporter));
            }

            ContentManager contentManager = new ContentManager(AssetsRootPath);
            AssetImportManager manager = new AssetImportManager(ProjectRootPath, contentManager);
            manager.RegisterModelImporter(new ModelImporterRegistration("test-model", modelImporter, new[] { ".obj", ".x" }));
            return manager;
        }

        /// <summary>
        /// Writes a minimal source model file for importer manager tests.
        /// </summary>
        /// <param name="fileName">Source file name to create inside the assets folder.</param>
        /// <returns>Absolute path to the source file.</returns>
        string WriteSourceModel(string fileName) {
            return WriteSourceModel(fileName, "test model source");
        }

        /// <summary>
        /// Writes one minimal serialized material asset inside the temporary assets folder.
        /// </summary>
        /// <param name="relativePath">Asset-relative material path to create.</param>
        /// <returns>Absolute path to the serialized material asset.</returns>
        string WriteMaterialAsset(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            string sourcePath = Path.Combine(AssetsRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string directoryPath = Path.GetDirectoryName(sourcePath);
            if (!string.IsNullOrWhiteSpace(directoryPath)) {
                Directory.CreateDirectory(directoryPath);
            }

            MaterialAssetSettingsService settingsService = new MaterialAssetSettingsService();
            settingsService.Save(sourcePath, CreateMaterialImportSettings("Materials/Generated/Cube00.hasset", "#FFFFFFFF"));
            return sourcePath;
        }

        /// <summary>
        /// Creates one representative material import-settings payload for a generated standard material.
        /// </summary>
        /// <param name="assetId">Stable asset id stored on the settings sidecar.</param>
        /// <param name="baseColor">Authored base-color string stored for the Windows platform.</param>
        /// <returns>Typed material import settings ready for serialization.</returns>
        MaterialAssetImportSettings CreateMaterialImportSettings(string assetId, string baseColor) {
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Asset id must be provided.", nameof(assetId));
            } else if (string.IsNullOrWhiteSpace(baseColor)) {
                throw new ArgumentException("Base color must be provided.", nameof(baseColor));
            }

            return new MaterialAssetImportSettings {
                Importer = new AssetImporterSettings {
                    ImporterId = "helengine.material",
                    SourceChecksum = string.Empty,
                    AssetId = assetId
                },
                Processor = new MaterialAssetProcessorPlatformSettings {
                    Platforms = new Dictionary<string, MaterialAssetProcessorSettings> {
                        ["windows"] = new MaterialAssetProcessorSettings {
                            SchemaId = "standard-shader",
                            FieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                                ["use-custom-shader"] = "false",
                                ["texture-id"] = string.Empty,
                                ["casts-shadow"] = "true",
                                ["receives-shadow"] = "true",
                                ["base-color"] = baseColor
                            }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Writes a model source file with explicit contents for importer manager tests.
        /// </summary>
        /// <param name="fileName">Source file name to create inside the assets folder.</param>
        /// <param name="contents">Text content to write into the source file.</param>
        /// <returns>Absolute path to the source file.</returns>
        string WriteSourceModel(string fileName, string contents) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("File name must be provided.", nameof(fileName));
            }

            if (string.IsNullOrWhiteSpace(contents)) {
                throw new ArgumentException("Contents must be provided.", nameof(contents));
            }

            string sourcePath = Path.Combine(AssetsRootPath, fileName);
            File.WriteAllText(sourcePath, contents);
            return sourcePath;
        }

        /// <summary>
        /// Creates one generated material asset description used by importer-manager tests.
        /// </summary>
        /// <param name="materialName">Stable material name.</param>
        /// <param name="relativePath">Relative path where the generated material should be written.</param>
        /// <param name="diffuseTextureAssetId">Diffuse texture asset id to serialize.</param>
        /// <returns>Generated material asset description.</returns>
        ImportedModelMaterialAsset CreateGeneratedMaterial(string materialName, string relativePath, string diffuseTextureAssetId) {
            if (string.IsNullOrWhiteSpace(materialName)) {
                throw new ArgumentException("Material name must be provided.", nameof(materialName));
            } else if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }

            return new ImportedModelMaterialAsset(
                materialName,
                relativePath,
                new ShaderMaterialAsset {
                    Id = relativePath,
                    ShaderAssetId = BuiltInMaterialIds.StandardMaterialShaderAssetId,
                    VertexProgram = "ForwardStandardShader.vs",
                    PixelProgram = "ForwardStandardShader.ps",
                    Variant = "default",
                    DiffuseTextureAssetId = diffuseTextureAssetId
                });
        }

    }
}
