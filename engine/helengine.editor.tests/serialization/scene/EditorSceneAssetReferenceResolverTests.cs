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
    }
}
