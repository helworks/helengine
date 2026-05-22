using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies the built-in generated engine provider publishes primitive models and reuses cached runtime models.
    /// </summary>
    public class EngineGeneratedAssetProviderTests : IDisposable {
        /// <summary>
        /// Shared 3D renderer used to capture generated model builds.
        /// </summary>
        readonly TestRenderManager3D RenderManager3D;

        /// <summary>
        /// Initializes core services needed by the generated model cache.
        /// </summary>
        public EngineGeneratedAssetProviderTests() {
            RenderManager3D = new TestRenderManager3D();
            Core core = new Core();
            core.Initialize(RenderManager3D, null, null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Clears generated model cache state between tests.
        /// </summary>
        public void Dispose() {
            EngineGeneratedModelCache.ResetForTests();
            EngineGeneratedMaterialCache.ResetForTests();
        }

        /// <summary>
        /// Ensures the provider publishes the expected virtual folder tree and primitive model entries.
        /// </summary>
        [Fact]
        public void LoadEntries_WhenBrowsingEnginePaths_ReturnsExpectedVirtualEntries() {
            EngineGeneratedAssetProvider provider = new EngineGeneratedAssetProvider();
            List<AssetBrowserEntry> rootEntries = new List<AssetBrowserEntry>();
            List<AssetBrowserEntry> engineEntries = new List<AssetBrowserEntry>();
            List<AssetBrowserEntry> modelEntries = new List<AssetBrowserEntry>();

            provider.LoadEntries(string.Empty, rootEntries);
            provider.LoadEntries("Engine", engineEntries);
            provider.LoadEntries("Engine/Models", modelEntries);

            AssetBrowserEntry rootEntry = Assert.Single(rootEntries);
            Assert.Equal("Engine", rootEntry.Name);

            Assert.Equal(2, engineEntries.Count);
            Assert.Contains(engineEntries, entry => entry.Name == "Models");
            Assert.Contains(engineEntries, entry => entry.Name == "Materials");

            Assert.Equal(3, modelEntries.Count);
            Assert.Contains(modelEntries, entry => entry.Name == "Cube" && entry.AssetId == "engine:model:cube");
            Assert.Contains(modelEntries, entry => entry.Name == "Plane" && entry.AssetId == "engine:model:plane");
            Assert.Contains(modelEntries, entry => entry.Name == "Sphere" && entry.AssetId == "engine:model:sphere");
        }

        /// <summary>
        /// Ensures the provider publishes the generated materials directory and standard material entry.
        /// </summary>
        [Fact]
        public void LoadEntries_WhenBrowsingEngineMaterialPaths_ReturnsStandardMaterialEntry() {
            EngineGeneratedAssetProvider provider = new EngineGeneratedAssetProvider();
            List<AssetBrowserEntry> engineEntries = new List<AssetBrowserEntry>();
            List<AssetBrowserEntry> materialEntries = new List<AssetBrowserEntry>();

            provider.LoadEntries(EngineGeneratedAssetProvider.EngineRootPath, engineEntries);
            provider.LoadEntries(EngineGeneratedAssetProvider.EngineMaterialsPath, materialEntries);

            Assert.Contains(engineEntries, entry => entry.Name == "Materials" && entry.EntryKind == AssetEntryKind.Directory);
            AssetBrowserEntry standardEntry = Assert.Single(materialEntries);
            Assert.Equal("Standard", standardEntry.Name);
            Assert.Equal(AssetEntryKind.Material, standardEntry.EntryKind);
            Assert.Equal(EngineGeneratedMaterialCache.StandardAssetId, standardEntry.AssetId);
        }

        /// <summary>
        /// Ensures resolving the same generated primitive twice reuses the cached runtime model instance.
        /// </summary>
        [Fact]
        public void TryResolveRuntimeModel_WhenCalledTwice_ReusesTheCachedRuntimeModel() {
            EngineGeneratedAssetProvider provider = new EngineGeneratedAssetProvider();
            AssetBrowserEntry cubeEntry = AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", "engine:model:cube");

            Assert.True(provider.TryResolveRuntimeModel(cubeEntry, out RuntimeModel firstModel));
            Assert.True(provider.TryResolveRuntimeModel(cubeEntry, out RuntimeModel secondModel));

            Assert.Same(firstModel, secondModel);
            Assert.Single(RenderManager3D.BuiltModelAssets);
        }

        /// <summary>
        /// Ensures resolving the same generated material twice reuses the cached runtime material instance.
        /// </summary>
        [Fact]
        public void TryResolveRuntimeMaterial_WhenCalledTwice_ReusesTheCachedRuntimeMaterial() {
            EngineGeneratedAssetProvider provider = new EngineGeneratedAssetProvider();
            AssetBrowserEntry standardEntry = AssetBrowserEntry.CreateGeneratedAsset(
                "Standard",
                EngineGeneratedAssetProvider.StandardMaterialRelativePath,
                AssetEntryKind.Material,
                EngineGeneratedAssetProvider.ProviderIdValue,
                EngineGeneratedMaterialCache.StandardAssetId);

            Assert.True(provider.TryResolveRuntimeMaterial(standardEntry, out RuntimeMaterial firstMaterial));
            Assert.True(provider.TryResolveRuntimeMaterial(standardEntry, out RuntimeMaterial secondMaterial));

            Assert.Same(firstMaterial, secondMaterial);
        }

        /// <summary>
        /// Ensures the generated standard material writes a white base-color constant buffer before the runtime material is built.
        /// </summary>
        [Fact]
        public void TryResolveRuntimeMaterial_WhenResolvingStandardMaterial_WritesWhiteBaseColorBuffer() {
            EngineGeneratedAssetProvider provider = new EngineGeneratedAssetProvider();
            AssetBrowserEntry standardEntry = AssetBrowserEntry.CreateGeneratedAsset(
                "Standard",
                EngineGeneratedAssetProvider.StandardMaterialRelativePath,
                AssetEntryKind.Material,
                EngineGeneratedAssetProvider.ProviderIdValue,
                EngineGeneratedMaterialCache.StandardAssetId);

            Assert.True(provider.TryResolveRuntimeMaterial(standardEntry, out RuntimeMaterial runtimeMaterial));

            ShaderMaterialAsset materialAsset = Assert.Single(RenderManager3D.BuiltMaterialAssets);
            MaterialConstantBufferAsset baseColorBuffer = Assert.Single(materialAsset.ConstantBuffers);
            Assert.Equal("BaseColorBuffer", baseColorBuffer.Name);
            Assert.Equal(16, baseColorBuffer.Data.Length);
            Assert.NotNull(runtimeMaterial);
        }
    }
}
