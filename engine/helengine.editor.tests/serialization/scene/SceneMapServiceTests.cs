using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies runtime scene-map lookup behavior against currently loaded scene records.
    /// </summary>
    public sealed class SceneMapServiceTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the scene-map service tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes one isolated temporary content root.
        /// </summary>
        public SceneMapServiceTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-map-service-tests", Guid.NewGuid().ToString("N"));
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
        /// Ensures the runtime scene-map service returns the original scene id when no scene-map component is loaded.
        /// </summary>
        [Fact]
        public void MapSceneId_WhenNoSceneMapComponentIsLoaded_ReturnsOriginalSceneId() {
            Core core = CreateCore();

            string mappedSceneId = core.SceneMapService.MapSceneId("MainMenu");

            Assert.Equal("MainMenu", mappedSceneId);
        }

        /// <summary>
        /// Ensures the runtime scene-map service returns the mapped scene id when the key exists.
        /// </summary>
        [Fact]
        public void MapSceneId_WhenMappingExists_ReturnsMappedSceneId() {
            Core core = CreateCore();
            SceneMapComponent sceneMapComponent = new SceneMapComponent();
            sceneMapComponent.Mappings.Add("MainMenu", "DemoDiscMainMenuDs");
            AddLoadedScene(core.SceneManager, "Scenes/Persistent.helen", CreateRootEntityWithComponent(sceneMapComponent));

            string mappedSceneId = core.SceneMapService.MapSceneId("MainMenu");

            Assert.Equal("DemoDiscMainMenuDs", mappedSceneId);
        }

        /// <summary>
        /// Ensures the runtime scene-map service returns the original scene id when the key is not present.
        /// </summary>
        [Fact]
        public void MapSceneId_WhenMappingIsMissing_ReturnsOriginalSceneId() {
            Core core = CreateCore();
            SceneMapComponent sceneMapComponent = new SceneMapComponent();
            sceneMapComponent.Mappings.Add("OptionsMenu", "OptionsMenuDs");
            AddLoadedScene(core.SceneManager, "Scenes/Persistent.helen", CreateRootEntityWithComponent(sceneMapComponent));

            string mappedSceneId = core.SceneMapService.MapSceneId("MainMenu");

            Assert.Equal("MainMenu", mappedSceneId);
        }

        /// <summary>
        /// Ensures the runtime scene-map service throws when multiple scene-map components are loaded globally.
        /// </summary>
        [Fact]
        public void MapSceneId_WhenMultipleSceneMapComponentsAreLoaded_ThrowsInvalidOperationException() {
            Core core = CreateCore();
            AddLoadedScene(core.SceneManager, "Scenes/Persistent.helen", CreateRootEntityWithComponent(new SceneMapComponent()));
            AddLoadedScene(core.SceneManager, "Scenes/SecondPersistent.helen", CreateRootEntityWithComponent(new SceneMapComponent()));

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => core.SceneMapService.MapSceneId("MainMenu"));

            Assert.Contains("SceneMapComponent", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the runtime scene-map service rejects empty scene ids.
        /// </summary>
        [Fact]
        public void MapSceneId_WhenSceneIdIsEmpty_ThrowsArgumentException() {
            Core core = CreateCore();

            Assert.Throws<ArgumentException>(() => core.SceneMapService.MapSceneId(string.Empty));
        }

        /// <summary>
        /// Ensures the demo-disc return-to-menu component loads the mapped scene id when one scene-map entry exists.
        /// </summary>
        [Fact]
        public void Update_WhenReturnToMenuIsTriggeredAndMappingExists_LoadsMappedSceneId() {
            WriteSceneAsset("cooked/scenes/DemoDiscMainMenu.hasset", 1u);
            WriteSceneAsset("cooked/scenes/DemoDiscMainMenuDs.hasset", 2u);

            TestInputBackend inputBackend = new TestInputBackend();
            inputBackend.Gamepads = new[] { CreatePressedGamepadState(InputGamepadButton.Select) };
            inputBackend.GamepadCount = 1;

            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("DemoDiscMainMenu", "cooked/scenes/DemoDiscMainMenu.hasset"),
                new RuntimeSceneCatalogEntry("DemoDiscMainMenuDs", "cooked/scenes/DemoDiscMainMenuDs.hasset")), inputBackend);

            SceneMapComponent sceneMapComponent = new SceneMapComponent();
            sceneMapComponent.Mappings.Add("DemoDiscMainMenu", "DemoDiscMainMenuDs");
            AddLoadedScene(core.SceneManager, "Scenes/Persistent.helen", CreateRootEntityWithComponent(sceneMapComponent));
            CreateRootEntityWithComponent(new DemoDiscReturnToMenuRuntimeComponent());

            core.Update(1d / 60d);

            Assert.True(core.SceneManager.IsSceneLoaded("DemoDiscMainMenuDs"));
            Assert.False(core.SceneManager.IsSceneLoaded("DemoDiscMainMenu"));
        }

        /// <summary>
        /// Ensures the demo-disc return-to-menu component falls back to the original scene id when no mapping exists.
        /// </summary>
        [Fact]
        public void Update_WhenReturnToMenuIsTriggeredAndNoMappingExists_LoadsOriginalSceneId() {
            WriteSceneAsset("cooked/scenes/DemoDiscMainMenu.hasset", 1u);

            TestInputBackend inputBackend = new TestInputBackend();
            inputBackend.Gamepads = new[] { CreatePressedGamepadState(InputGamepadButton.Select) };
            inputBackend.GamepadCount = 1;

            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("DemoDiscMainMenu", "cooked/scenes/DemoDiscMainMenu.hasset")), inputBackend);

            CreateRootEntityWithComponent(new DemoDiscReturnToMenuRuntimeComponent());

            core.Update(1d / 60d);

            Assert.True(core.SceneManager.IsSceneLoaded("DemoDiscMainMenu"));
        }

        /// <summary>
        /// Creates one initialized core rooted at the temporary content path.
        /// </summary>
        /// <param name="sceneCatalog">Optional runtime scene catalog exposed to the core bootstrap path.</param>
        /// <param name="inputBackend">Input backend supplying deterministic test input.</param>
        /// <returns>Initialized core instance for scene-map service tests.</returns>
        Core CreateCore(RuntimeSceneCatalog sceneCatalog = null, TestInputBackend inputBackend = null) {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath,
                SceneCatalog = sceneCatalog,
                ScenePathResolver = new TestSceneIdPathResolver(new Dictionary<string, string>(StringComparer.Ordinal) {
                    { "Scenes/AuthoredMenu.helen", "Scenes/AuthoredMenu.helen" }
                })
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), inputBackend ?? new TestInputBackend(), new PlatformInfo("test", "test-version"));
            return core;
        }

        /// <summary>
        /// Creates one runtime scene catalog for the supplied entries.
        /// </summary>
        /// <param name="entries">Runtime scene entries that should be exposed to the core bootstrap path.</param>
        /// <returns>Runtime scene catalog instance.</returns>
        RuntimeSceneCatalog CreateSceneCatalog(params RuntimeSceneCatalogEntry[] entries) {
            return new RuntimeSceneCatalog(entries);
        }

        /// <summary>
        /// Creates one initialized root entity with the supplied component attached.
        /// </summary>
        /// <param name="component">Component to attach to the root entity.</param>
        /// <returns>Initialized root entity containing the component.</returns>
        Entity CreateRootEntityWithComponent(Component component) {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();
            entity.AddComponent(component);
            entity.InitializeHierarchy();
            return entity;
        }

        /// <summary>
        /// Creates one connected gamepad state with a single pressed button.
        /// </summary>
        /// <param name="button">Gamepad button that should report as down.</param>
        /// <returns>Configured connected gamepad state.</returns>
        InputGamepadState CreatePressedGamepadState(InputGamepadButton button) {
            InputGamepadState state = new InputGamepadState {
                Connected = true
            };
            state.SetButtonDown(button, true);
            return state;
        }

        /// <summary>
        /// Writes one packaged scene asset into the temporary content root.
        /// </summary>
        /// <param name="relativePath">Content-relative packaged scene path.</param>
        /// <param name="rootEntityId">Stable root entity identifier to persist.</param>
        void WriteSceneAsset(string relativePath, uint rootEntityId) {
            string fullPath = Path.Combine(TempRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            SceneAsset sceneAsset = new SceneAsset {
                Id = relativePath,
                SceneSettings = new SceneSettingsAsset {
                    CanvasProfile = new SceneCanvasProfile()
                },
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = rootEntityId,
                        Name = "Entity" + rootEntityId.ToString(),
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Appends one loaded-scene record directly to the scene manager for service-level tests.
        /// </summary>
        /// <param name="sceneManager">Scene manager that should receive the loaded scene record.</param>
        /// <param name="sceneId">Stable scene id stored on the record.</param>
        /// <param name="rootEntity">Root entity owned by the loaded record.</param>
        void AddLoadedScene(SceneManager sceneManager, string sceneId, Entity rootEntity) {
            if (sceneManager == null) {
                throw new ArgumentNullException(nameof(sceneManager));
            }
            if (rootEntity == null) {
                throw new ArgumentNullException(nameof(rootEntity));
            }

            LoadedSceneRecord loadedSceneRecord = new LoadedSceneRecord(
                sceneId,
                "Scenes/Persistent.hasset",
                new[] { rootEntity },
                new RuntimeSceneOwnedAssetSet(Array.Empty<RuntimeTexture>(), Array.Empty<FontAsset>(), Array.Empty<RuntimeModel>(), Array.Empty<RuntimeMaterial>()),
                true);

            FieldInfo recordsField = typeof(SceneManager).GetField("LoadedSceneRecords", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo recordsByIdField = typeof(SceneManager).GetField("LoadedSceneRecordsById", BindingFlags.Instance | BindingFlags.NonPublic);
            List<LoadedSceneRecord> loadedSceneRecords = Assert.IsType<List<LoadedSceneRecord>>(recordsField.GetValue(sceneManager));
            Dictionary<string, LoadedSceneRecord> loadedSceneRecordsById = Assert.IsType<Dictionary<string, LoadedSceneRecord>>(recordsByIdField.GetValue(sceneManager));
            loadedSceneRecords.Add(loadedSceneRecord);
            loadedSceneRecordsById.Add(sceneId, loadedSceneRecord);
        }
    }
}
