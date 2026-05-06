using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies generated material references resolve through the generated-asset registry.
    /// </summary>
    public class EditorSceneAssetReferenceResolverTests : IDisposable {
        /// <summary>
        /// Temporary project root used by generated material resolver tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes an isolated project root and the core services required by the resolver.
        /// </summary>
        public EditorSceneAssetReferenceResolverTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-material-resolver-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets"));

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary test state and clears provider registrations.
        /// </summary>
        public void Dispose() {
            GeneratedAssetProviderRegistry.ResetForTests();
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures generated material references resolve through the generated-asset provider registry.
        /// </summary>
        [Fact]
        public void ResolveMaterial_WhenReferenceIsGenerated_UsesGeneratedAssetProviderRegistry() {
            TestRuntimeMaterial runtimeMaterial = new TestRuntimeMaterial();
            GeneratedAssetProviderRegistry.Register(new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedAsset("Standard", "Engine/Materials/Standard", AssetEntryKind.Material, "engine", EngineGeneratedMaterialCache.StandardAssetId)
                },
                new TestRuntimeModel(),
                runtimeMaterial));
            EditorSceneAssetReferenceResolver resolver = new EditorSceneAssetReferenceResolver(new ContentManager(TempProjectRootPath), TempProjectRootPath);

            RuntimeMaterial resolvedMaterial = resolver.ResolveMaterial(new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "Engine/Materials/Standard",
                ProviderId = "engine",
                AssetId = EngineGeneratedMaterialCache.StandardAssetId
            });

            Assert.Same(runtimeMaterial, resolvedMaterial);
        }

        /// <summary>
        /// Ensures filesystem-backed model references resolve through the processed model cache instead of deserializing the raw source file.
        /// </summary>
        [Fact]
        public void ResolveModel_WhenReferenceIsFileSystem_UsesImportedModelAsset() {
            string modelPath = Path.Combine(TempProjectRootPath, "assets", "Models", "Sponza.obj");
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath));
            File.WriteAllText(modelPath, "raw obj source");
            AssetImportManager assetImportManager = CreateAssetImportManager();
            TestRenderManager3D renderManager = Assert.IsType<TestRenderManager3D>(Core.Instance.RenderManager3D);
            EditorSceneAssetReferenceResolver resolver = new EditorSceneAssetReferenceResolver(
                new ContentManager(TempProjectRootPath),
                TempProjectRootPath,
                new EditorFileSystemModelResolver(assetImportManager));

            RuntimeModel runtimeModel = resolver.ResolveModel(new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = "Models/Sponza.obj"
            });

            Assert.NotNull(runtimeModel);
            ModelAsset builtModelAsset = Assert.Single(renderManager.BuiltModelAssets);
            Assert.Equal(new ushort[] { 0, 1, 2 }, builtModelAsset.Indices16);
        }

        /// <summary>
        /// Ensures filesystem-backed source font references resolve through the imported font cache instead of loading raw source bytes as a packaged font.
        /// </summary>
        [Fact]
        public void ResolveFont_WhenReferenceIsSourceFont_UsesImportedFontAsset() {
            string fontPath = Path.Combine(TempProjectRootPath, "assets", "Fonts", "DemoDiscTitle.ttf");
            Directory.CreateDirectory(Path.GetDirectoryName(fontPath));
            File.WriteAllBytes(fontPath, new byte[] { 1, 2, 3, 4 });
            AssetImportManager assetImportManager = CreateAssetImportManager();
            EditorSceneAssetReferenceResolver resolver = new EditorSceneAssetReferenceResolver(
                new ContentManager(TempProjectRootPath),
                TempProjectRootPath,
                new EditorFileSystemModelResolver(assetImportManager),
                new EditorFileSystemFontResolver(assetImportManager));

            FontAsset font = resolver.ResolveFont(new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = "Fonts/DemoDiscTitle.ttf"
            });

            Assert.Equal("ImportedTestFont", font.FontInfo.Name);
        }

        /// <summary>
        /// Creates one asset import manager that can import `.obj` source files for the current resolver test project.
        /// </summary>
        /// <returns>Configured asset import manager.</returns>
        AssetImportManager CreateAssetImportManager() {
            ContentManager contentManager = new ContentManager(Path.Combine(TempProjectRootPath, "assets"));
            AssetImportManager assetImportManager = new AssetImportManager(TempProjectRootPath, contentManager);
            assetImportManager.RegisterModelImporter(new ModelImporterRegistration("test-model", new TestModelImporter(), new[] { ".obj" }));
            assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), new[] { ".ttf" }));
            return assetImportManager;
        }
    }
}
