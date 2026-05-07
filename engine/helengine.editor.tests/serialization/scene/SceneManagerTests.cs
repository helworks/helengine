using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies runtime scene-manager loading, tracking, and unload notifications for built scenes.
    /// </summary>
    public sealed class SceneManagerTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the scene-manager tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes one isolated temporary content root.
        /// </summary>
        public SceneManagerTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-manager-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
        }

        /// <summary>
        /// Deletes the temporary content root after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures core bootstrap initializes the runtime scene manager when packaged scene metadata exists.
        /// </summary>
        [Fact]
        public void Initialize_whenRuntimeSceneCatalogExists_createsSceneManager() {
            WriteSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/main.hasset"));

            Core core = CreateCore();

            Assert.NotNull(core.SceneManager);
        }

        /// <summary>
        /// Ensures single-mode loads track one built scene and dispatch lifecycle events in order.
        /// </summary>
        [Fact]
        public void LoadScene_whenModeIsSingle_tracksSceneAndRaisesLifecycleEvents() {
            WriteSceneAsset("cooked/scenes/main.hasset", "root-bootstrap");
            WriteSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/main.hasset"));
            Core core = CreateCore();
            List<string> raisedEvents = new List<string>();
            string loadedSceneId = string.Empty;
            string loadedCookedPath = string.Empty;
            IReadOnlyList<Entity> loadedRootEntities = Array.Empty<Entity>();

            core.SceneManager.SceneLoading += (_, eventArgs) => {
                raisedEvents.Add("loading:" + eventArgs.SceneId);
            };
            core.SceneManager.SceneLoaded += (_, eventArgs) => {
                raisedEvents.Add("loaded:" + eventArgs.SceneId);
                loadedSceneId = eventArgs.SceneId;
                loadedCookedPath = eventArgs.CookedRelativePath;
                loadedRootEntities = eventArgs.RootEntities;
            };

            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);

            LoadedSceneRecord loadedScene = Assert.Single(core.SceneManager.LoadedScenes);
            Assert.Equal("Scenes/Bootstrap.helen", loadedScene.SceneId);
            Assert.Equal("cooked/scenes/main.hasset", loadedScene.CookedRelativePath);
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));
            Assert.Equal(new[] { "loading:Scenes/Bootstrap.helen", "loaded:Scenes/Bootstrap.helen" }, raisedEvents);
            Assert.Equal("Scenes/Bootstrap.helen", loadedSceneId);
            Assert.Equal("cooked/scenes/main.hasset", loadedCookedPath);
            Assert.Single(loadedRootEntities);
        }

        /// <summary>
        /// Ensures additive loads preserve previously tracked scenes.
        /// </summary>
        [Fact]
        public void LoadScene_whenModeIsAdditive_preservesPreviouslyLoadedScenes() {
            WriteSceneAsset("cooked/scenes/main.hasset", "root-bootstrap");
            WriteSceneAsset("scenes/Scenes/TestPlayableScene.hasset", "root-playable");
            WriteSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/main.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "scenes/Scenes/TestPlayableScene.hasset"));
            Core core = CreateCore();

            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
            core.SceneManager.LoadScene("Scenes/TestPlayableScene.helen", SceneLoadMode.Additive);

            Assert.Equal(2, core.SceneManager.LoadedScenes.Count);
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/TestPlayableScene.helen"));
        }

        /// <summary>
        /// Ensures unload notifications expose the tracked root entities and remove scene bookkeeping.
        /// </summary>
        [Fact]
        public void UnloadScene_whenSceneIsTracked_raisesUnloadEventsWithRootEntitiesAndRemovesTheRecord() {
            WriteSceneAsset("cooked/scenes/main.hasset", "root-bootstrap");
            WriteSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/main.hasset"));
            Core core = CreateCore();
            List<string> raisedEvents = new List<string>();
            IReadOnlyList<Entity> unloadingRootEntities = Array.Empty<Entity>();

            core.SceneManager.SceneUnloading += (_, eventArgs) => {
                raisedEvents.Add("unloading:" + eventArgs.SceneId);
                unloadingRootEntities = eventArgs.RootEntities;
            };
            core.SceneManager.SceneUnloaded += (_, eventArgs) => {
                raisedEvents.Add("unloaded:" + eventArgs.SceneId);
            };

            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
            core.SceneManager.UnloadScene("Scenes/Bootstrap.helen");

            Assert.False(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));
            Assert.Empty(core.SceneManager.LoadedScenes);
            Assert.Equal(new[] { "unloading:Scenes/Bootstrap.helen", "unloaded:Scenes/Bootstrap.helen" }, raisedEvents);
            Assert.Single(unloadingRootEntities);
        }

        /// <summary>
        /// Writes one runtime scene catalog JSON file into the temporary content root.
        /// </summary>
        /// <param name="entries">Catalog entries to persist.</param>
        void WriteSceneCatalog(params RuntimeSceneCatalogEntry[] entries) {
            string manifestPath = Path.Combine(TempRootPath, "runtime-scene-catalog.json");
            using StreamWriter writer = new StreamWriter(manifestPath, false, System.Text.Encoding.UTF8);
            writer.WriteLine("{");
            writer.WriteLine("  \"Entries\": [");
            for (int index = 0; index < entries.Length; index++) {
                RuntimeSceneCatalogEntry entry = entries[index];
                writer.WriteLine("    {");
                writer.WriteLine("      \"SceneId\": \"" + entry.SceneId + "\",");
                writer.WriteLine("      \"CookedRelativePath\": \"" + entry.CookedRelativePath + "\"");
                writer.Write("    }");
                if (index < entries.Length - 1) {
                    writer.Write(",");
                }

                writer.WriteLine();
            }

            writer.WriteLine("  ]");
            writer.WriteLine("}");
        }

        /// <summary>
        /// Writes one packaged scene asset into the temporary content root.
        /// </summary>
        /// <param name="relativePath">Content-relative packaged scene path.</param>
        /// <param name="rootEntityId">Stable root entity identifier to persist.</param>
        void WriteSceneAsset(string relativePath, string rootEntityId) {
            string fullPath = Path.Combine(TempRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            SceneAsset sceneAsset = new SceneAsset {
                Id = relativePath,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = rootEntityId,
                        Name = rootEntityId,
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Creates one initialized core rooted at the temporary content path.
        /// </summary>
        /// <returns>Initialized core instance for runtime scene-manager tests.</returns>
        Core CreateCore() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend());
            return core;
        }
    }
}
