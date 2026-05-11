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
        /// Ensures core bootstrap initializes the runtime scene manager when packaged scene metadata is injected.
        /// </summary>
        [Fact]
        public void Initialize_whenRuntimeSceneCatalogIsProvided_createsSceneManager() {
            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset")));

            Assert.NotNull(core.SceneManager);
        }

        /// <summary>
        /// Ensures core bootstrap leaves the runtime scene manager unavailable when no packaged scene metadata is injected.
        /// </summary>
        [Fact]
        public void Initialize_whenRuntimeSceneCatalogIsNotProvided_leavesSceneManagerNull() {
            Core core = CreateCore();

            Assert.Null(core.SceneManager);
        }

        /// <summary>
        /// Ensures single-mode loads track one built scene and dispatch lifecycle events in order.
        /// </summary>
        [Fact]
        public void LoadScene_whenModeIsSingle_tracksSceneAndRaisesLifecycleEvents() {
            WriteSceneAsset("cooked/scenes/Bootstrap.hasset", "root-bootstrap");
            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset")));
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
            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/TestPlayableScene.hasset")));

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
            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/TestPlayableScene.hasset")));

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
            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/TestPlayableScene.hasset")));
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
        /// Ensures scene transitions requested from inside an update component are deferred until the active update loop completes.
        /// </summary>
        [Fact]
        public void LoadScene_whenRequestedDuringUpdate_defersSceneDisposalUntilAfterTheUpdateMethodReturns() {
            WriteSceneAsset("cooked/scenes/Bootstrap.hasset", "root-bootstrap");
            WriteSceneAsset("cooked/scenes/TestPlayableScene.hasset", "root-playable");
            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset"),
                new RuntimeSceneCatalogEntry("Scenes/TestPlayableScene.helen", "cooked/scenes/TestPlayableScene.hasset")));

            core.SceneManager.LoadScene("Scenes/Bootstrap.helen", SceneLoadMode.Single);

            Entity bootstrapRoot = Assert.Single(core.SceneManager.LoadedScenes).RootEntities[0];
            TestSceneLoadTriggerComponent triggerComponent = new TestSceneLoadTriggerComponent {
                TargetSceneId = "Scenes/TestPlayableScene.helen"
            };
            bootstrapRoot.AddComponent(triggerComponent);

            core.Update();

            Assert.True(triggerComponent.HasRequestedLoad);
            Assert.True(triggerComponent.WasStillAttachedAfterRequest);
            Assert.True(core.SceneManager.IsSceneLoaded("Scenes/TestPlayableScene.helen"));
            Assert.False(core.SceneManager.IsSceneLoaded("Scenes/Bootstrap.helen"));
        }

        /// <summary>
        /// Ensures unload notifications expose the tracked root entities and remove scene bookkeeping.
        /// </summary>
        [Fact]
        public void UnloadScene_whenSceneIsTracked_raisesUnloadEventsWithRootEntitiesAndRemovesTheRecord() {
            WriteSceneAsset("cooked/scenes/Bootstrap.hasset", "root-bootstrap");
            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("Scenes/Bootstrap.helen", "cooked/scenes/Bootstrap.hasset")));
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
        /// Creates one runtime scene catalog for the supplied entries.
        /// </summary>
        /// <param name="entries">Runtime scene entries to expose to the core bootstrap path.</param>
        /// <returns>Runtime scene catalog instance.</returns>
        RuntimeSceneCatalog CreateSceneCatalog(params RuntimeSceneCatalogEntry[] entries) {
            return new RuntimeSceneCatalog(entries);
        }

        /// <summary>
        /// Creates one initialized core rooted at the temporary content path.
        /// </summary>
        /// <param name="sceneCatalog">Optional runtime scene catalog to inject before initialization.</param>
        /// <returns>Initialized core instance for runtime scene-manager tests.</returns>
        Core CreateCore(RuntimeSceneCatalog sceneCatalog = null) {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath,
                SceneCatalog = sceneCatalog
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
