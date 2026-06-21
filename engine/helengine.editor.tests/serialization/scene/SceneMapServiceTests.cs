using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies singleton scene-map behavior and startup redirect handling for scene resolution.
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
            string resolvedSceneId = SceneMapComponent.ResolveSceneId("MainMenuScene");

            Assert.Equal("MainMenuScene", resolvedSceneId);
        }

        /// <summary>
        /// Ensures one logical scene id resolves through the active singleton mapping table.
        /// </summary>
        [Fact]
        public void ResolveSceneId_WhenMappingExists_ReturnsMappedSceneId() {
            Entity rootEntity = CreateRootEntity();
            SceneMapComponent sceneMapComponent = new SceneMapComponent();
            sceneMapComponent.Mappings.Add("MainMenuScene", "AlternateMainMenuScene");

            rootEntity.AddComponent(sceneMapComponent);

            string resolvedSceneId = SceneMapComponent.ResolveSceneId("MainMenuScene");

            Assert.Equal("AlternateMainMenuScene", resolvedSceneId);
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
            WriteSceneAsset("cooked/scenes/MainMenuScene.hasset", 1u);
            WriteSceneAsset("cooked/scenes/AlternateMainMenuScene.hasset", 2u);

            Core core = CreateCore(CreateSceneCatalog(
                new RuntimeSceneCatalogEntry("MainMenuScene", "cooked/scenes/MainMenuScene.hasset"),
                new RuntimeSceneCatalogEntry("AlternateMainMenuScene", "cooked/scenes/AlternateMainMenuScene.hasset")));

            SceneMapComponent sceneMapComponent = new SceneMapComponent {
                InitialSceneId = "MainMenuScene"
            };
            sceneMapComponent.Mappings.Add("MainMenuScene", "AlternateMainMenuScene");
            AddLoadedScene(core.SceneManager, "Scenes/GeneratedBoot.helen", CreateRootEntityWithComponent(sceneMapComponent));

            core.Update(1d / 60d);
            core.Update(1d / 60d);

            Assert.True(core.SceneManager.IsSceneLoaded("AlternateMainMenuScene"));
            Assert.False(core.SceneManager.IsSceneLoaded("MainMenuScene"));
        }

        /// <summary>
        /// Ensures a persistent generated boot scene can repeatedly route from the mapped menu scene into cube-test and back without losing the singleton mapping or the active scene transition flow.
        /// </summary>
        [Fact]
        public void LoadScene_WhenPersistentBootSceneRoutesRepeatedCubeReturns_PreservesMappedMenuAcrossMultipleCycles() {
            WriteSceneAsset(
                "cooked/scenes/StartupScene.hasset",
                1u,
                true,
                CreateRuntimeSceneMapComponentRecord(
                    "MainMenuScene",
                    CreateMapping("MainMenuScene", "AlternateMainMenuScene")));
            WriteSceneAsset("cooked/scenes/AlternateMainMenuScene.hasset", 2u);
            WriteSceneAsset(
                "cooked/scenes/cube_test.hasset",
                3u,
                false);

            TestInputBackend inputBackend = new TestInputBackend();
            inputBackend.SetGamepadStates(new[] { CreateConnectedGamepadState() });
            Core core = CreateCore(
                CreateSceneCatalog(
                    new RuntimeSceneCatalogEntry("StartupScene", "cooked/scenes/StartupScene.hasset"),
                    new RuntimeSceneCatalogEntry("AlternateMainMenuScene", "cooked/scenes/AlternateMainMenuScene.hasset"),
                    new RuntimeSceneCatalogEntry("cube_test", "cooked/scenes/cube_test.hasset")),
                inputBackend);

            core.SceneManager.LoadScene("StartupScene", SceneLoadMode.Single);
            core.Update(1d / 60d);

            Assert.True(core.SceneManager.IsSceneLoaded("StartupScene"));
            Assert.True(core.SceneManager.IsSceneLoaded("AlternateMainMenuScene"));
            Assert.False(core.SceneManager.IsSceneLoaded("cube_test"));
            Assert.NotNull(SceneMapComponent.Instance);

            for (int cycleIndex = 0; cycleIndex < 3; cycleIndex++) {
                core.SceneManager.LoadScene("cube_test", SceneLoadMode.Single);
                Assert.True(core.SceneManager.IsSceneLoaded("StartupScene"));
                Assert.True(core.SceneManager.IsSceneLoaded("cube_test"));
                Assert.False(core.SceneManager.IsSceneLoaded("AlternateMainMenuScene"));

                core.SceneManager.LoadScene(SceneMapComponent.ResolveSceneId("MainMenuScene"), SceneLoadMode.Single);

                Assert.True(core.SceneManager.IsSceneLoaded("StartupScene"));
                Assert.True(core.SceneManager.IsSceneLoaded("AlternateMainMenuScene"));
                Assert.False(core.SceneManager.IsSceneLoaded("cube_test"));
                Assert.NotNull(SceneMapComponent.Instance);
                Assert.Equal(2, core.SceneManager.LoadedScenes.Count);
            }
        }

        /// <summary>
        /// Ensures repeated cube roundtrips preserve the expected menu-versus-cube owned-asset baseline when a persistent boot scene routes every return through the mapped menu scene.
        /// </summary>
        [Fact]
        public void LoadScene_WhenPersistentBootSceneRoutesRepeatedCubeReturns_ReleasesCubeOwnedAssetsBeforeReloadingMenu() {
            WriteFontAsset("fonts/default.hefont", CreateFont());
            WriteModelAsset("cooked/models/TestModel.hasset");
            WriteMaterialAsset("cooked/materials/TestMaterial.hasset", "ForwardStandardShader");
            WriteShaderAsset("cooked/shaders/ForwardStandardShader.dx11.hasset", "ForwardStandardShader");
            WriteShaderAsset("cooked/shaders/ForwardStandardShader.vulkan.hasset", "ForwardStandardShader");
            WriteSceneAsset(
                "cooked/scenes/StartupScene.hasset",
                1u,
                true,
                CreateRuntimeSceneMapComponentRecord(
                    "MainMenuScene",
                    CreateMapping("MainMenuScene", "AlternateMainMenuScene")));
            WriteSceneAsset(
                "cooked/scenes/AlternateMainMenuScene.hasset",
                2u,
                false,
                CreateTextComponentRecord("fonts/default.hefont"));
            WriteSceneAsset(
                "cooked/scenes/cube_test.hasset",
                3u,
                false,
                CreateMeshComponentRecord("cooked/models/TestModel.hasset", "cooked/materials/TestMaterial.hasset"));

            TestInputBackend inputBackend = new TestInputBackend();
            inputBackend.SetGamepadStates(new[] { CreateConnectedGamepadState() });
            Core core = CreateCore(
                CreateSceneCatalog(
                    new RuntimeSceneCatalogEntry("StartupScene", "cooked/scenes/StartupScene.hasset"),
                    new RuntimeSceneCatalogEntry("AlternateMainMenuScene", "cooked/scenes/AlternateMainMenuScene.hasset"),
                    new RuntimeSceneCatalogEntry("cube_test", "cooked/scenes/cube_test.hasset")),
                inputBackend);

            core.SceneManager.LoadScene("StartupScene", SceneLoadMode.Single);
            core.Update(1d / 60d);

            Assert.Equal(1, core.SceneManager.ActiveOwnedFontReferenceCount);
            Assert.Equal(0, core.SceneManager.ActiveOwnedTextureReferenceCount);
            Assert.Equal(0, core.SceneManager.ActiveOwnedMaterialReferenceCount);
            Assert.Equal(0, core.SceneManager.ActiveOwnedModelReferenceCount);

            for (int cycleIndex = 0; cycleIndex < 3; cycleIndex++) {
                core.SceneManager.LoadScene("cube_test", SceneLoadMode.Single);

                Assert.Equal(0, core.SceneManager.ActiveOwnedFontReferenceCount);
                Assert.Equal(0, core.SceneManager.ActiveOwnedTextureReferenceCount);
                Assert.Equal(1, core.SceneManager.ActiveOwnedMaterialReferenceCount);
                Assert.Equal(1, core.SceneManager.ActiveOwnedModelReferenceCount);

                core.SceneManager.LoadScene(SceneMapComponent.ResolveSceneId("MainMenuScene"), SceneLoadMode.Single);

                Assert.Equal(1, core.SceneManager.ActiveOwnedFontReferenceCount);
                Assert.Equal(0, core.SceneManager.ActiveOwnedTextureReferenceCount);
                Assert.Equal(0, core.SceneManager.ActiveOwnedMaterialReferenceCount);
                Assert.Equal(0, core.SceneManager.ActiveOwnedModelReferenceCount);
            }
        }

        /// <summary>
        /// Ensures repeated cube roundtrips preserve the expected owned-asset baseline when the menu and cube scene both use generated asset references that match the real project shape.
        /// </summary>
        [Fact]
        public void LoadScene_WhenPersistentBootSceneRoutesRepeatedGeneratedCubeReturns_ReleasesGeneratedOwnedAssetsBeforeReloadingMenu() {
            WriteFontAsset("generated/editor/fonts/ui.hefont", CreateFont());
            WriteModelAsset("Engine/Models/Cube");
            WriteMaterialAsset("Engine/Materials/Standard", "ForwardStandardShader");
            WriteShaderAsset("cooked/shaders/ForwardStandardShader.dx11.hasset", "ForwardStandardShader");
            WriteShaderAsset("cooked/shaders/ForwardStandardShader.vulkan.hasset", "ForwardStandardShader");
            WriteSceneAsset(
                "cooked/scenes/StartupScene.hasset",
                1u,
                true,
                CreateRuntimeSceneMapComponentRecord(
                    "MainMenuScene",
                    CreateMapping("MainMenuScene", "AlternateMainMenuScene")));
            WriteSceneAsset(
                "cooked/scenes/AlternateMainMenuScene.hasset",
                2u,
                false,
                CreateFpsComponentRecord("generated/editor/fonts/ui.hefont", "editor", "ui-font"));
            WriteSceneAsset(
                "cooked/scenes/cube_test.hasset",
                3u,
                false,
                CreateFpsComponentRecord("generated/editor/fonts/ui.hefont", "editor", "ui-font"),
                CreateMeshComponentRecord(
                    CreateGeneratedReference("Engine/Models/Cube", "engine", "engine:model:cube"),
                    CreateGeneratedReference("Engine/Materials/Standard", "engine", "engine:material:standard")));

            TestInputBackend inputBackend = new TestInputBackend();
            inputBackend.SetGamepadStates(new[] { CreateConnectedGamepadState() });
            Core core = CreateCore(
                CreateSceneCatalog(
                    new RuntimeSceneCatalogEntry("StartupScene", "cooked/scenes/StartupScene.hasset"),
                    new RuntimeSceneCatalogEntry("AlternateMainMenuScene", "cooked/scenes/AlternateMainMenuScene.hasset"),
                    new RuntimeSceneCatalogEntry("cube_test", "cooked/scenes/cube_test.hasset")),
                inputBackend);

            core.SceneManager.LoadScene("StartupScene", SceneLoadMode.Single);
            core.Update(1d / 60d);

            Assert.Equal(1, core.SceneManager.ActiveOwnedFontReferenceCount);
            Assert.Equal(0, core.SceneManager.ActiveOwnedTextureReferenceCount);
            Assert.Equal(0, core.SceneManager.ActiveOwnedMaterialReferenceCount);
            Assert.Equal(0, core.SceneManager.ActiveOwnedModelReferenceCount);

            for (int cycleIndex = 0; cycleIndex < 3; cycleIndex++) {
                core.SceneManager.LoadScene("cube_test", SceneLoadMode.Single);

                Assert.Equal(1, core.SceneManager.ActiveOwnedFontReferenceCount);
                Assert.Equal(0, core.SceneManager.ActiveOwnedTextureReferenceCount);
                Assert.Equal(1, core.SceneManager.ActiveOwnedMaterialReferenceCount);
                Assert.Equal(1, core.SceneManager.ActiveOwnedModelReferenceCount);

                core.SceneManager.LoadScene(SceneMapComponent.ResolveSceneId("MainMenuScene"), SceneLoadMode.Single);

                Assert.Equal(1, core.SceneManager.ActiveOwnedFontReferenceCount);
                Assert.Equal(0, core.SceneManager.ActiveOwnedTextureReferenceCount);
                Assert.Equal(0, core.SceneManager.ActiveOwnedMaterialReferenceCount);
                Assert.Equal(0, core.SceneManager.ActiveOwnedModelReferenceCount);
            }
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
        /// Creates one connected idle gamepad state that does not report any pressed buttons.
        /// </summary>
        /// <returns>Configured connected gamepad state with no active buttons.</returns>
        InputGamepadState CreateConnectedGamepadState() {
            return new InputGamepadState {
                Connected = true
            };
        }

        /// <summary>
        /// Creates one idle mouse state at the supplied pointer coordinates.
        /// </summary>
        /// <param name="x">Pointer X coordinate in window pixels.</param>
        /// <param name="y">Pointer Y coordinate in window pixels.</param>
        /// <returns>Mouse state with the primary button released.</returns>
        MouseState CreateReleasedMouseState(int x, int y) {
            return new MouseState(
                x,
                y,
                0,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released);
        }

        /// <summary>
        /// Creates one primary-button mouse state at the supplied pointer coordinates.
        /// </summary>
        /// <param name="x">Pointer X coordinate in window pixels.</param>
        /// <param name="y">Pointer Y coordinate in window pixels.</param>
        /// <returns>Mouse state with the primary button pressed.</returns>
        MouseState CreatePressedMouseState(int x, int y) {
            return new MouseState(
                x,
                y,
                0,
                ButtonState.Pressed,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released,
                ButtonState.Released);
        }

        /// <summary>
        /// Creates one pointer-visible camera used by pointer-routed return-button tests.
        /// </summary>
        /// <param name="viewport">Viewport rectangle in window pixels.</param>
        /// <param name="layerMask">Layer mask rendered by the camera.</param>
        /// <param name="cameraDrawOrder">Camera draw order used for pointer routing.</param>
        void CreateUiCamera(float4 viewport, ushort layerMask, byte cameraDrawOrder) {
            Entity cameraEntity = CreateRootEntity();
            cameraEntity.LayerMask = layerMask;
            cameraEntity.AddComponent(new CameraComponent {
                LayerMask = layerMask,
                CameraDrawOrder = cameraDrawOrder,
                Viewport = viewport
            });
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
        /// Writes one packaged scene asset into the temporary content root with the supplied dont-unload setting and serialized root components.
        /// </summary>
        /// <param name="relativePath">Content-relative packaged scene path.</param>
        /// <param name="rootEntityId">Stable root entity identifier to persist.</param>
        /// <param name="dontUnload">True when the packaged scene should survive normal single-scene transitions.</param>
        /// <param name="components">Serialized root components to persist.</param>
        void WriteSceneAsset(string relativePath, uint rootEntityId, bool dontUnload, params SceneComponentAssetRecord[] components) {
            string fullPath = Path.Combine(TempRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            SceneAsset sceneAsset = new SceneAsset {
                Id = relativePath,
                SceneSettings = new SceneSettingsAsset {
                    CanvasProfile = new SceneCanvasProfile(),
                    DontUnload = dontUnload
                },
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = rootEntityId,
                        Name = "Entity" + rootEntityId.ToString(),
                        Components = components ?? Array.Empty<SceneComponentAssetRecord>(),
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
        /// Creates one packaged runtime scene-map component record encoded in the shared automatic runtime payload shape used by player builds.
        /// </summary>
        /// <param name="initialSceneId">Logical startup scene id that should be resolved through the singleton map.</param>
        /// <param name="mappings">Scene-id mapping entries authored on the persistent helper scene.</param>
        /// <returns>Packaged runtime scene-map component record.</returns>
        SceneComponentAssetRecord CreateRuntimeSceneMapComponentRecord(string initialSceneId, params KeyValuePair<string, string>[] mappings) {
            SceneMapComponent component = new SceneMapComponent {
                InitialSceneId = initialSceneId
            };
            for (int index = 0; index < mappings.Length; index++) {
                component.Mappings.Add(mappings[index].Key, mappings[index].Value);
            }

            return new SceneComponentAssetRecord {
                ComponentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(SceneMapComponent)),
                ComponentIndex = 0,
                Payload = WriteAutomaticRuntimeComponentPayload(component, null)
            };
        }

        /// <summary>
        /// Creates one stable scene-id mapping pair used by runtime scene-map payload helpers.
        /// </summary>
        /// <param name="sourceSceneId">Logical scene id requested by gameplay or menu code.</param>
        /// <param name="targetSceneId">Mapped runtime scene id that should be loaded instead.</param>
        /// <returns>Stable scene-id mapping pair.</returns>
        KeyValuePair<string, string> CreateMapping(string sourceSceneId, string targetSceneId) {
            return new KeyValuePair<string, string>(sourceSceneId, targetSceneId);
        }

        /// <summary>
        /// Writes one packaged font asset into the temporary content root.
        /// </summary>
        /// <param name="relativePath">Content-relative packaged font path.</param>
        /// <param name="font">Packaged font asset to persist.</param>
        void WriteFontAsset(string relativePath, FontAsset font) {
            string fullPath = Path.Combine(TempRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            FontAssetBinarySerializer.Serialize(stream, font);
        }

        /// <summary>
        /// Creates one packaged font asset with a single runtime atlas texel.
        /// </summary>
        /// <returns>Packaged font asset used by repeated scene-map load tests.</returns>
        FontAsset CreateFont() {
            TextureAsset sourceTexture = new TextureAsset {
                Width = 1,
                Height = 1,
                Colors = new byte[] { 255, 255, 255, 255 },
                PaletteColors = new byte[] { 0, 0, 0, 255 }
            };

            FontAsset font = new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 1,
                    Height = 1
                },
                new Dictionary<char, FontChar>(),
                16f,
                1,
                1) {
                SourceTextureAsset = sourceTexture
            };
            return font;
        }

        /// <summary>
        /// Creates one serialized runtime text-component record that references the supplied packaged font path.
        /// </summary>
        /// <param name="fontRelativePath">Content-relative packaged font path used by the text component.</param>
        /// <returns>Serialized text component record.</returns>
        SceneComponentAssetRecord CreateTextComponentRecord(string fontRelativePath) {
            TextComponent textComponent = new TextComponent {
                Font = CreateFont(),
                Text = "Hello world",
                SelectionEnabled = false,
                Size = new int2(320, 64),
                Color = new byte4(12, 34, 56, 78),
                SourceRect = new float4(0.1f, 0.2f, 0.3f, 0.4f),
                Rotation = 0.25f,
                RenderOrder2D = 19,
                LayerMask = 7
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(nameof(TextComponent.Font), CreateFileReference(fontRelativePath));

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.TextComponent",
                ComponentIndex = 0,
                Payload = WriteAutomaticRuntimeComponentPayload(textComponent, saveState)
            };
        }

        /// <summary>
        /// Creates one serialized runtime FPS component record that references the supplied packaged font path.
        /// </summary>
        /// <param name="fontRelativePath">Content-relative packaged font path used by the FPS overlay.</param>
        /// <param name="providerId">Provider identifier carried by the generated font reference.</param>
        /// <param name="assetId">Asset identifier carried by the generated font reference.</param>
        /// <returns>Serialized FPS component record.</returns>
        SceneComponentAssetRecord CreateFpsComponentRecord(string fontRelativePath, string providerId, string assetId) {
            FPSComponent fpsComponent = new FPSComponent {
                Font = CreateFont(),
                RefreshIntervalSeconds = 0.5d,
                Padding = new int2(8, 6),
                RenderOrder2D = 250
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(nameof(FPSComponent.Font), CreateGeneratedReference(fontRelativePath, providerId, assetId));

            return new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.FPSComponent",
                ComponentIndex = 0,
                Payload = WriteAutomaticRuntimeComponentPayload(fpsComponent, saveState)
            };
        }

        /// <summary>
        /// Serializes one engine component through the packaged automatic reflected runtime payload path used by the player.
        /// </summary>
        /// <param name="component">Engine component to serialize.</param>
        /// <param name="saveState">Optional asset-reference state associated with the component.</param>
        /// <returns>Serialized automatic runtime component payload.</returns>
        byte[] WriteAutomaticRuntimeComponentPayload(Component component, EntityComponentSaveState saveState) {
            if (component == null) {
                throw new ArgumentNullException(nameof(component));
            }

            ScriptComponentReflectionSchemaBuilder schemaBuilder = new ScriptComponentReflectionSchemaBuilder();
            ScriptComponentReflectionSchema schema = schemaBuilder.Build(component.GetType());
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(AutomaticScriptComponentRuntimeDeserializer.CurrentVersion);
            writer.WriteInt32(schema.Members.Count);
            for (int index = 0; index < schema.Members.Count; index++) {
                ScriptComponentReflectionMember member = schema.Members[index];
                AutomaticScriptComponentPersistenceDescriptor.WriteSupportedMemberValue(writer, member, component, saveState);
            }

            return stream.ToArray();
        }

        /// <summary>
        /// Writes one packaged model asset into the temporary content root.
        /// </summary>
        /// <param name="relativePath">Content-relative packaged model path.</param>
        void WriteModelAsset(string relativePath) {
            string fullPath = Path.Combine(TempRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            ModelAsset modelAsset = new ModelAsset {
                Id = "TestModel",
                Positions = new[] {
                    new float3(0f, 0f, 0f),
                    new float3(1f, 0f, 0f),
                    new float3(0f, 1f, 0f)
                },
                Normals = new[] {
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f),
                    new float3(0f, 0f, 1f)
                },
                TexCoords = new[] {
                    new float2(0f, 0f),
                    new float2(1f, 0f),
                    new float2(0f, 1f)
                },
                Indices16 = new ushort[] { 0, 1, 2 },
                BoundsMin = new float3(0f, 0f, 0f),
                BoundsMax = new float3(1f, 1f, 0f),
                Submeshes = new[] {
                    new ModelSubmeshAsset {
                        MaterialSlotName = "Default",
                        IndexStart = 0,
                        IndexCount = 3
                    }
                }
            };

            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, modelAsset);
        }

        /// <summary>
        /// Writes one packaged material asset into the temporary content root.
        /// </summary>
        /// <param name="relativePath">Content-relative packaged material path.</param>
        /// <param name="shaderAssetId">Shader asset identifier referenced by the packaged material.</param>
        void WriteMaterialAsset(string relativePath, string shaderAssetId) {
            string fullPath = Path.Combine(TempRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            ShaderMaterialAsset materialAsset = new ShaderMaterialAsset {
                Id = "TestMaterial",
                ShaderAssetId = shaderAssetId,
                VertexProgram = "VS",
                PixelProgram = "PS",
                Variant = "Default"
            };

            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, materialAsset);
        }

        /// <summary>
        /// Writes one packaged shader asset into the temporary content root.
        /// </summary>
        /// <param name="relativePath">Content-relative packaged shader path.</param>
        /// <param name="shaderAssetId">Stable shader asset identifier stored in the package.</param>
        void WriteShaderAsset(string relativePath, string shaderAssetId) {
            string fullPath = Path.Combine(TempRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            ShaderAsset shaderAsset = new ShaderAsset {
                Id = shaderAssetId,
                Name = shaderAssetId,
                TargetName = "directx11",
                Programs = new[] {
                    new ShaderProgramAsset {
                        Name = "VS",
                        Stage = ShaderStage.Vertex,
                        EntryPoint = "VS",
                        Bindings = Array.Empty<ShaderBindingAsset>(),
                        Inputs = Array.Empty<ShaderVertexElementAsset>(),
                        Outputs = Array.Empty<ShaderVertexElementAsset>(),
                        Variants = Array.Empty<ShaderVariantAsset>()
                    },
                    new ShaderProgramAsset {
                        Name = "PS",
                        Stage = ShaderStage.Pixel,
                        EntryPoint = "PS",
                        Bindings = Array.Empty<ShaderBindingAsset>(),
                        Inputs = Array.Empty<ShaderVertexElementAsset>(),
                        Outputs = Array.Empty<ShaderVertexElementAsset>(),
                        Variants = Array.Empty<ShaderVariantAsset>()
                    }
                },
                Binaries = Array.Empty<ShaderBinaryAsset>()
            };

            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, shaderAsset);
        }

        /// <summary>
        /// Creates one serialized runtime mesh component record that references the supplied packaged model and material paths.
        /// </summary>
        /// <param name="modelRelativePath">Content-relative packaged model path used by the mesh component.</param>
        /// <param name="materialRelativePath">Content-relative packaged material path used by the mesh component.</param>
        /// <returns>Serialized mesh component record.</returns>
        SceneComponentAssetRecord CreateMeshComponentRecord(string modelRelativePath, string materialRelativePath) {
            return CreateMeshComponentRecord(
                CreateFileReference(modelRelativePath),
                CreateFileReference(materialRelativePath));
        }

        /// <summary>
        /// Creates one serialized runtime mesh component record that references the supplied packaged model and material references.
        /// </summary>
        /// <param name="modelReference">Packaged model reference used by the mesh component.</param>
        /// <param name="materialReference">Packaged material reference used by the mesh component.</param>
        /// <returns>Serialized mesh component record.</returns>
        SceneComponentAssetRecord CreateMeshComponentRecord(SceneAssetReference modelReference, SceneAssetReference materialReference) {
            MeshComponent meshComponent = new MeshComponent {
                Model = new TestRuntimeModel(),
                Materials = new[] { new TestRuntimeMaterial() },
                RenderOrder3D = 9
            };
            EntityComponentSaveState saveState = new EntityComponentSaveState();
            saveState.SetAssetReference(nameof(MeshComponent.Model), modelReference);
            saveState.SetAssetReference("Materials[0]", materialReference);
            return new SceneComponentAssetRecord {
                ComponentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(MeshComponent)),
                ComponentIndex = 0,
                Payload = WriteAutomaticRuntimeComponentPayload(meshComponent, saveState)
            };
        }

        /// <summary>
        /// Creates one packaged file-backed scene asset reference.
        /// </summary>
        /// <param name="relativePath">Content-relative packaged asset path.</param>
        /// <returns>File-backed scene asset reference.</returns>
        SceneAssetReference CreateFileReference(string relativePath) {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = relativePath,
                ProviderId = string.Empty,
                AssetId = string.Empty
            };
        }

        /// <summary>
        /// Creates one generated packaged scene asset reference.
        /// </summary>
        /// <param name="relativePath">Content-relative packaged asset path.</param>
        /// <param name="providerId">Provider identifier used to describe the generated asset family.</param>
        /// <param name="assetId">Stable generated asset identifier.</param>
        /// <returns>Generated scene asset reference.</returns>
        SceneAssetReference CreateGeneratedReference(string relativePath, string providerId, string assetId) {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = relativePath,
                ProviderId = providerId,
                AssetId = assetId
            };
        }

        /// <summary>
        /// Writes one packaged scene asset reference into a runtime component payload.
        /// </summary>
        /// <param name="writer">Writer receiving the serialized scene asset reference.</param>
        /// <param name="reference">Packaged scene asset reference to serialize.</param>
        void WriteSceneAssetReference(EngineBinaryWriter writer, SceneAssetReference reference) {
            if (writer == null) {
                throw new ArgumentNullException(nameof(writer));
            } else if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            }

            writer.WriteByte(1);
            writer.WriteInt32((int)reference.SourceKind);
            writer.WriteString(reference.RelativePath);
            writer.WriteString(reference.ProviderId);
            writer.WriteString(reference.AssetId);
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
