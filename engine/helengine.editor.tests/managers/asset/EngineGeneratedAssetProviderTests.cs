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
            core.Initialize(RenderManager3D, null, null);
        }

        /// <summary>
        /// Clears generated model cache state between tests.
        /// </summary>
        public void Dispose() {
            EngineGeneratedModelCache.ResetForTests();
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

            AssetBrowserEntry modelsEntry = Assert.Single(engineEntries);
            Assert.Equal("Models", modelsEntry.Name);

            Assert.Equal(2, modelEntries.Count);
            Assert.Contains(modelEntries, entry => entry.Name == "Cube" && entry.AssetId == "engine:model:cube");
            Assert.Contains(modelEntries, entry => entry.Name == "Plane" && entry.AssetId == "engine:model:plane");
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
    }
}
