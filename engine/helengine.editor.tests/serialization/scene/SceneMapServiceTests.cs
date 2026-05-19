using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies singleton scene-map behavior and startup redirect handling for menu scene resolution.
    /// </summary>
    public sealed class SceneMapServiceTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the scene-map tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes one isolated temporary content root and resets the singleton state.
        /// </summary>
        public SceneMapServiceTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-map-service-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
            ResetSceneMapComponentSingleton();
        }

        /// <summary>
        /// Deletes the temporary content root and resets singleton state after each test.
        /// </summary>
        public void Dispose() {
            ResetSceneMapComponentSingleton();
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures one logical scene id falls back unchanged when no singleton instance exists.
        /// </summary>
        [Fact]
        public void ResolveSceneId_WhenNoSingletonExists_ReturnsOriginalSceneId() {
            string resolvedSceneId = SceneMapComponent.ResolveSceneId("DemoDiscMainMenu");

            Assert.Equal("DemoDiscMainMenu", resolvedSceneId);
        }

        /// <summary>
        /// Ensures one logical scene id resolves through the active singleton mapping table.
        /// </summary>
        [Fact]
        public void ResolveSceneId_WhenMappingExists_ReturnsMappedSceneId() {
            Entity rootEntity = CreateRootEntity();
            SceneMapComponent sceneMapComponent = new SceneMapComponent();
            sceneMapComponent.Mappings.Add("DemoDiscMainMenu", "DemoDiscMainMenuDs");

            rootEntity.AddComponent(sceneMapComponent);

            string resolvedSceneId = SceneMapComponent.ResolveSceneId("DemoDiscMainMenu");

            Assert.Equal("DemoDiscMainMenuDs", resolvedSceneId);
        }

        /// <summary>
        /// Ensures one second active singleton fails immediately.
        /// </summary>
        [Fact]
        public void ComponentAdded_WhenSecondSceneMapComponentAppears_ThrowsInvalidOperationException() {
            Entity rootEntity = CreateRootEntity();
            SceneMapComponent first = new SceneMapComponent();
            SceneMapComponent second = new SceneMapComponent();

            rootEntity.AddComponent(first);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => rootEntity.AddComponent(second));

            Assert.Contains("Only one active SceneMapComponent may exist at a time.", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures disposing the owned singleton clears the active instance.
        /// </summary>
        [Fact]
        public void Dispose_WhenSingletonOwnsInstance_ClearsActiveInstance() {
            Entity rootEntity = CreateRootEntity();
            SceneMapComponent sceneMapComponent = new SceneMapComponent();
            rootEntity.AddComponent(sceneMapComponent);

            sceneMapComponent.Dispose();

            Assert.Null(SceneMapComponent.Instance);
        }

        /// <summary>
        /// Ensures an authored initial scene id triggers one startup redirect through the mapped target.
        /// </summary>
        [Fact]
        public void Update_WhenInitialSceneIdIsPresent_LoadsResolvedSceneIdOnce() {
            WriteSceneAsset("cooked/scenes/DemoDiscMainMenu.hasset", 1u);
            WriteSceneAsset("cooked/scenes/DemoDiscMainMenuDs.hasset", 2u);

            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("DemoDiscMainMenu", "cooked/scenes/DemoDiscMainMenu.hasset"),
                new RuntimeSceneCatalogEntry("DemoDiscMainMenuDs", "cooked/scenes/DemoDiscMainMenuDs.hasset")));

            SceneMapComponent sceneMapComponent = new SceneMapComponent {
                InitialSceneId = "DemoDiscMainMenu"
            };
            sceneMapComponent.Mappings.Add("DemoDiscMainMenu", "DemoDiscMainMenuDs");
            AddLoadedScene(core.SceneManager, "Scenes/GeneratedBoot.helen", CreateRootEntityWithComponent(sceneMapComponent));

            core.Update(1d / 60d);
            core.Update(1d / 60d);

            Assert.True(core.SceneManager.IsSceneLoaded("DemoDiscMainMenuDs"));
            Assert.False(core.SceneManager.IsSceneLoaded("DemoDiscMainMenu"));
        }

        /// <summary>
        /// Ensures return-to-menu resolves the logical menu id through the singleton mapping table.
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
        /// Ensures return-to-menu falls back to the original scene id when no mapping exists.
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
        /// <returns>Initialized core instance for scene-map tests.</returns>
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
        /// Creates one initialized root entity with no attached components.
        /// </summary>
        /// <returns>Initialized empty root entity.</returns>
        Entity CreateRootEntity() {
            Entity entity = new Entity();
            entity.InitComponents();
            entity.InitChildren();
            entity.InitializeHierarchy();
            return entity;
        }

        /// <summary>
        /// Creates one initialized root entity with the supplied component attached.
        /// </summary>
        /// <param name="component">Component to attach to the root entity.</param>
        /// <returns>Initialized root entity containing the component.</returns>
        Entity CreateRootEntityWithComponent(Component component) {
            Entity entity = CreateRootEntity();
            entity.AddComponent(component);
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
        /// Appends one loaded-scene record directly to the scene manager for singleton-level tests.
        /// </summary>
        /// <param name="sceneManager">Scene manager that should receive the loaded scene record.</param>
        /// <param name="sceneId">Stable scene id stored on the record.</param>
        /// <param name="rootEntity">Root entity owned by the loaded record.</param>
        void AddLoadedScene(SceneManager sceneManager, string sceneId, Entity rootEntity) {
            if (sceneManager == null) {
                throw new ArgumentNullException(nameof(sceneManager));
            } else if (rootEntity == null) {
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

        /// <summary>
        /// Resets static singleton and startup redirect state between tests.
        /// </summary>
        static void ResetSceneMapComponentSingleton() {
            PropertyInfo instanceProperty = typeof(SceneMapComponent).GetProperty(nameof(SceneMapComponent.Instance), BindingFlags.Static | BindingFlags.Public);
            MethodInfo instanceSetter = instanceProperty.GetSetMethod(true);
            instanceSetter.Invoke(null, [null]);

            FieldInfo startupSceneWasRequestedField = typeof(SceneMapComponent).GetField("StartupSceneWasRequested", BindingFlags.Static | BindingFlags.NonPublic);
            if (startupSceneWasRequestedField != null) {
                startupSceneWasRequestedField.SetValue(null, false);
            }
        }
    }
}
