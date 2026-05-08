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
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset"));

            Core core = CreateCore();

            Assert.NotNull(core.SceneManager);
        }

        /// <summary>
        /// Ensures core bootstrap initializes the runtime scene manager when packaged scene metadata exists beneath the cooked output root used by native players.
        /// </summary>
        [Fact]
        public void Initialize_whenRuntimeSceneCatalogExistsInsideCookedRoot_createsSceneManager() {
            WriteSceneCatalogInsideCookedRoot(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset"));

            Core core = CreateCore();

            Assert.NotNull(core.SceneManager);
        }

        /// <summary>
        /// Ensures single-mode loads track one built scene and dispatch lifecycle events in order.
        /// </summary>
        [Fact]
        public void LoadScene_whenModeIsSingle_tracksSceneAndRaisesLifecycleEvents() {
            WriteSceneAsset("cooked/scenes/Bootstrap.hasset", "root-bootstrap");
            WriteSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset"));
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
            Assert.Equal("cooked/scenes/Bootstrap.hasset", loadedScene.CookedRelativePath);
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));
            Assert.Equal(new[] { "loading:Scenes/Bootstrap.helen", "loaded:Scenes/Bootstrap.helen" }, raisedEvents);
            Assert.Equal("Scenes/Bootstrap.helen", loadedSceneId);
            Assert.Equal("cooked/scenes/Bootstrap.hasset", loadedCookedPath);
            Assert.Single(loadedRootEntities);
        }

        /// <summary>
        /// Ensures additive loads preserve previously tracked scenes.
        /// </summary>
        [Fact]
        public void LoadScene_whenModeIsAdditive_preservesPreviouslyLoadedScenes() {
            WriteSceneAsset("cooked/scenes/Bootstrap.hasset", "root-bootstrap");
            WriteSceneAsset("cooked/scenes/TestPlayableScene.hasset", "root-playable");
            WriteSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/TestPlayableScene.hasset"));
            Core core = CreateCore();

            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);
            core.SceneManager.LoadScene("Scenes/TestPlayableScene.helen", SceneLoadMode.Additive);

            Assert.Equal(2, core.SceneManager.LoadedScenes.Count);
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/TestPlayableScene.helen"));
        }

        /// <summary>
        /// Ensures single-mode scene transitions tear down the previous scene entities before loading the next scene.
        /// </summary>
        [Fact]
        public void LoadScene_whenModeIsSingleAfterPreviousSceneWasLoaded_disposesPreviousSceneEntities() {
            WriteSceneAsset(
                "cooked/scenes/Bootstrap.hasset",
                "root-bootstrap",
                CreateCameraComponentRecord(0));
            WriteSceneAsset(
                "cooked/scenes/TestPlayableScene.hasset",
                "root-playable",
                CreateCameraComponentRecord(1));
            WriteSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/TestPlayableScene.hasset"));
            Core core = CreateCore();

            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);

            Entity previousRoot = Assert.Single(core.SceneManager.LoadedScenes).RootEntities[0];
            CameraComponent previousCamera = Assert.IsType<CameraComponent>(Assert.Single(previousRoot.Components));
            Assert.Single(core.ObjectManager.Cameras);
            Assert.Single(core.ObjectManager.Entities);

            core.SceneManager.LoadScene("Scenes/TestPlayableScene.helen", SceneLoadMode.Single);

            LoadedSceneRecord loadedScene = Assert.Single(core.SceneManager.LoadedScenes);
            Entity loadedRoot = Assert.Single(loadedScene.RootEntities);
            CameraComponent loadedCamera = Assert.IsType<CameraComponent>(Assert.Single(loadedRoot.Components));
            Assert.Equal("Scenes/TestPlayableScene.helen", loadedScene.SceneId);
            Assert.Same(loadedCamera, Assert.Single(core.ObjectManager.Cameras));
            Assert.Same(loadedRoot, Assert.Single(core.ObjectManager.Entities));
            Assert.Empty(previousRoot.Components);
            Assert.Null(previousCamera.Parent);
            Assert.DoesNotContain(previousRoot, core.ObjectManager.Entities);
            Assert.DoesNotContain(previousCamera, core.ObjectManager.Cameras);
        }

        /// <summary>
        /// Ensures single-mode scene transitions tear down startup roots that were loaded directly before the runtime scene manager began tracking scenes.
        /// </summary>
        [Fact]
        public void LoadScene_whenModeIsSingleAndUntrackedStartupRootsExist_disposesTheUntrackedRoots() {
            WriteSceneAsset(
                "cooked/scenes/Bootstrap.hasset",
                "root-bootstrap",
                CreateCameraComponentRecord(0));
            WriteSceneAsset(
                "cooked/scenes/TestPlayableScene.hasset",
                "root-playable",
                CreateCameraComponentRecord(1));
            WriteSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/TestPlayableScene.hasset"));
            Core core = CreateCore();
            SceneAsset startupSceneAsset = core.ContentManager.Load<SceneAsset>("cooked/scenes/Bootstrap.hasset", RuntimeContentProcessorIds.SceneAsset);

            IReadOnlyList<Entity> startupRoots = core.SceneLoadService.Load(startupSceneAsset);
            Entity previousRoot = Assert.Single(startupRoots);
            CameraComponent previousCamera = Assert.IsType<CameraComponent>(Assert.Single(previousRoot.Components));
            Assert.Single(core.ObjectManager.Cameras);
            Assert.Single(core.ObjectManager.Entities);
            Assert.Empty(core.SceneManager.LoadedScenes);

            core.SceneManager.LoadScene("Scenes/TestPlayableScene.helen", SceneLoadMode.Single);

            LoadedSceneRecord loadedScene = Assert.Single(core.SceneManager.LoadedScenes);
            Entity loadedRoot = Assert.Single(loadedScene.RootEntities);
            CameraComponent loadedCamera = Assert.IsType<CameraComponent>(Assert.Single(loadedRoot.Components));
            Assert.Equal("Scenes/TestPlayableScene.helen", loadedScene.SceneId);
            Assert.Same(loadedCamera, Assert.Single(core.ObjectManager.Cameras));
            Assert.Same(loadedRoot, Assert.Single(core.ObjectManager.Entities));
            Assert.Empty(previousRoot.Components);
            Assert.Null(previousCamera.Parent);
            Assert.DoesNotContain(previousRoot, core.ObjectManager.Entities);
            Assert.DoesNotContain(previousCamera, core.ObjectManager.Cameras);
        }

        /// <summary>
        /// Ensures unload notifications expose the tracked root entities and remove scene bookkeeping.
        /// </summary>
        [Fact]
        public void UnloadScene_whenSceneIsTracked_raisesUnloadEventsWithRootEntitiesAndRemovesTheRecord() {
            WriteSceneAsset("cooked/scenes/Bootstrap.hasset", "root-bootstrap");
            WriteSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset"));
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
            WriteSceneCatalogFile(manifestPath, entries);
        }

        /// <summary>
        /// Writes one runtime scene catalog JSON file into the cooked-content root beneath the temporary content tree.
        /// </summary>
        /// <param name="entries">Catalog entries to persist.</param>
        void WriteSceneCatalogInsideCookedRoot(params RuntimeSceneCatalogEntry[] entries) {
            string manifestPath = Path.Combine(TempRootPath, "cooked", "runtime-scene-catalog.json");
            WriteSceneCatalogFile(manifestPath, entries);
        }

        /// <summary>
        /// Writes one runtime scene catalog JSON file into the supplied manifest path.
        /// </summary>
        /// <param name="manifestPath">Absolute file path that will receive the catalog JSON.</param>
        /// <param name="entries">Catalog entries to persist.</param>
        void WriteSceneCatalogFile(string manifestPath, params RuntimeSceneCatalogEntry[] entries) {
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath));
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
        void WriteSceneAsset(string relativePath, string rootEntityId, params SceneComponentAssetRecord[] components) {
            string fullPath = Path.Combine(TempRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            SceneAsset sceneAsset = new SceneAsset {
                Id = relativePath,
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = rootEntityId,
                        Name = rootEntityId,
                        Components = components ?? Array.Empty<SceneComponentAssetRecord>(),
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

        /// <summary>
        /// Creates one serialized camera component record for packaged scene-manager tests.
        /// </summary>
        /// <param name="drawOrder">Camera draw order to encode in the payload.</param>
        /// <returns>Serialized camera component record.</returns>
        SceneComponentAssetRecord CreateCameraComponentRecord(byte drawOrder) {
            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.CameraComponent",
                ComponentIndex = 0,
                Payload = WriteCameraComponentPayload(drawOrder)
            };
        }

        /// <summary>
        /// Writes one serialized packaged camera payload.
        /// </summary>
        /// <param name="drawOrder">Camera draw order to encode in the payload.</param>
        /// <returns>Serialized packaged camera payload.</returns>
        byte[] WriteCameraComponentPayload(byte drawOrder) {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteByte(drawOrder);
            writer.WriteUInt16(EditorLayerMasks.SceneObjects);
            writer.WriteSingle(0f);
            writer.WriteSingle(0f);
            writer.WriteSingle(1f);
            writer.WriteSingle(1f);
            writer.WriteByte(1);
            writer.WriteSingle(0f);
            writer.WriteSingle(0f);
            writer.WriteSingle(0f);
            writer.WriteSingle(1f);
            writer.WriteByte(1);
            writer.WriteSingle(1f);
            writer.WriteByte(1);
            writer.WriteByte(0);
            return stream.ToArray();
        }
    }
}
