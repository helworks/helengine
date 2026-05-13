using helengine.editor;
using helengine.editor.tests.testing;
using helengine.ui;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies loading `.helen` scene files into live editor entities.
    /// </summary>
    public class SceneFileLoadServiceTests : IDisposable {
        /// <summary>
        /// Temporary project root used by scene load tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes a temporary project root and the core services required for scene loading.
        /// </summary>
        public SceneFileLoadServiceTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-file-load-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scenes"));

            EditorCore core = new EditorCore(new Project {
                Name = "Scene File Load",
                Path = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"), new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
        }

        /// <summary>
        /// Deletes temporary project state after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a saved `.helen` file can be materialized back into editor entities.
        /// </summary>
        [Fact]
        public void Load_WhenSceneFileExists_ReturnsRootEntities() {
            SceneAssetReference modelReference = CreateGeneratedModelReference();
            SceneAssetReference materialReference = CreateGeneratedMaterialReference();
            string scenePath = SaveSceneAsset("Loaded.helen", "Loaded Cube", modelReference, materialReference);
            SceneFileLoadService loadService = CreateLoadService(modelReference, materialReference);

            LoadedEditorSceneDocument loaded = loadService.Load(scenePath);

            EditorEntity root = Assert.Single(loaded.RootEntities);
            Assert.Equal("Loaded Cube", root.Name);
            Assert.False(root.Enabled);
            Assert.Equal(SceneCanvasProfile.DefaultWidth, loaded.SceneSettings.CanvasProfile.Width);
            Assert.Equal(SceneCanvasProfile.DefaultHeight, loaded.SceneSettings.CanvasProfile.Height);
        }

        /// <summary>
        /// Ensures invalid scene files fail with a clear exception.
        /// </summary>
        [Fact]
        public void Load_WhenSceneFileIsInvalid_ThrowsInvalidOperationException() {
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "Broken.helen");
            File.WriteAllText(scenePath, "not-a-helen");
            SceneFileLoadService loadService = CreateLoadService(CreateGeneratedModelReference(), CreateGeneratedMaterialReference());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => loadService.Load(scenePath));

            Assert.Contains("Scene load failed", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures entities created during a failed materialization are removed from the live object manager.
        /// </summary>
        [Fact]
        public void Load_WhenMaterializationFails_CleansNewEntitiesAndPreservesExistingScene() {
            SceneAssetReference modelReference = CreateGeneratedModelReference();
            SceneAssetReference materialReference = CreateGeneratedMaterialReference();
            string scenePath = SaveSceneAsset("BrokenMaterialization.helen", "Transient Root", modelReference, materialReference);
            ComponentPersistenceRegistry registry = CreatePersistenceRegistry();
            SceneFileLoadService loadService = new SceneFileLoadService(TempProjectRootPath, registry, new TestSceneAssetReferenceResolver());
            EditorEntity existing = Assert.IsType<EditorEntity>(Core.Instance.EntityFactory.Create("Existing"));

            Assert.Throws<InvalidOperationException>(() => loadService.Load(scenePath));

            Assert.Contains(Core.Instance.ObjectManager.Entities, entity => ReferenceEquals(entity, existing));
            Assert.DoesNotContain(Core.Instance.ObjectManager.Entities, entity => entity is EditorEntity editorEntity && string.Equals(editorEntity.Name, "Transient Root", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures editor scene loading materializes baked demo menu metadata without executing the runtime menu lifecycle.
        /// </summary>
        [Fact]
        public void Load_WhenSceneContainsBakedDemoMenu_LoadsWithoutExecutingRuntimeLifecycle() {
            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            RegisterDemoMenuFonts(resolver);
            string scenePath = SaveBakedMenuSceneAsset("MenuRoot.helen", "city.menu.DemoDiscMenuDefinitionProvider, city");
            SceneFileLoadService loadService = new SceneFileLoadService(TempProjectRootPath, CreateDemoMenuPersistenceRegistry(), resolver);

            LoadedEditorSceneDocument loaded = loadService.Load(scenePath);

            Assert.Equal(2, loaded.RootEntities.Length);
            EditorEntity root = Assert.Single(loaded.RootEntities, entity => entity.Components.Any(component => component is MenuComponent));
            MenuComponent demoMenuBuildComponent = Assert.IsType<MenuComponent>(Assert.Single(root.Components, component => component is MenuComponent));
            Assert.Equal("city.menu.DemoDiscMenuDefinitionProvider, city", demoMenuBuildComponent.ProviderTypeName);
            Assert.False(demoMenuBuildComponent.IsInitialized);
            Assert.Single(root.Children);
            Assert.NotEmpty(root.Children[0].Children);
        }

        /// <summary>
        /// Ensures scene loading returns the authored scene canvas profile stored in the scene file.
        /// </summary>
        [Fact]
        public void Load_WhenSceneFileContainsCustomCanvasProfile_ReturnsSceneSettings() {
            SceneAssetReference modelReference = CreateGeneratedModelReference();
            SceneAssetReference materialReference = CreateGeneratedMaterialReference();
            SceneSettingsAsset sceneSettings = new SceneSettingsAsset {
                CanvasProfile = new SceneCanvasProfile {
                    Width = 1600,
                    Height = 900
                }
            };
            string scenePath = SaveSceneAsset("CanvasProfile.helen", "Loaded Cube", modelReference, materialReference, sceneSettings);
            SceneFileLoadService loadService = CreateLoadService(modelReference, materialReference);

            LoadedEditorSceneDocument loaded = loadService.Load(scenePath);

            Assert.Equal(1600, loaded.SceneSettings.CanvasProfile.Width);
            Assert.Equal(900, loaded.SceneSettings.CanvasProfile.Height);
        }

        /// <summary>
        /// Creates a scene-load service with runtime references registered for a saved mesh component.
        /// </summary>
        /// <param name="modelReference">Model reference to resolve during load.</param>
        /// <param name="materialReference">Material reference to resolve during load.</param>
        /// <returns>Configured scene file load service.</returns>
        SceneFileLoadService CreateLoadService(SceneAssetReference modelReference, SceneAssetReference materialReference) {
            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            resolver.RegisterModel(modelReference, new TestRuntimeModel());
            resolver.RegisterMaterial(materialReference, new TestRuntimeMaterial());
            return new SceneFileLoadService(TempProjectRootPath, CreatePersistenceRegistry(), resolver);
        }

        /// <summary>
        /// Creates the component persistence registry used by scene save and load tests.
        /// </summary>
        /// <returns>Configured component persistence registry.</returns>
        ComponentPersistenceRegistry CreatePersistenceRegistry() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            registry.Register(new MeshComponentPersistenceDescriptor());
            return registry;
        }

        /// <summary>
        /// Creates the component persistence registry required to load baked demo menu scene records.
        /// </summary>
        /// <returns>Configured persistence registry containing baked menu support.</returns>
        ComponentPersistenceRegistry CreateDemoMenuPersistenceRegistry() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            registry.Register(new CameraComponentPersistenceDescriptor());
            registry.Register(new MenuComponentPersistenceDescriptor());
            registry.Register(new MenuPanelComponentPersistenceDescriptor());
            registry.Register(new MenuItemComponentPersistenceDescriptor());
            registry.Register(new MenuSelectedDescriptionComponentPersistenceDescriptor());
            registry.Register(new RoundedRectComponentPersistenceDescriptor());
            registry.Register(new TextComponentPersistenceDescriptor());
            registry.Register(new FPSComponentPersistenceDescriptor());
            return registry;
        }

        /// <summary>
        /// Saves one scene file containing a single mesh-backed user entity.
        /// </summary>
        /// <param name="fileName">Scene file name to write.</param>
        /// <param name="entityName">Name assigned to the saved root entity.</param>
        /// <param name="modelReference">Model reference persisted with the mesh component.</param>
        /// <param name="materialReference">Material reference persisted with the mesh component.</param>
        /// <returns>Absolute path to the written `.helen` file.</returns>
        string SaveSceneAsset(string fileName, string entityName, SceneAssetReference modelReference, SceneAssetReference materialReference) {
            return SaveSceneAsset(fileName, entityName, modelReference, materialReference, new SceneSettingsAsset());
        }

        /// <summary>
        /// Saves one scene file containing a single mesh-backed user entity and explicit scene settings.
        /// </summary>
        /// <param name="fileName">Scene file name to write.</param>
        /// <param name="entityName">Name assigned to the saved root entity.</param>
        /// <param name="modelReference">Model reference persisted with the mesh component.</param>
        /// <param name="materialReference">Material reference persisted with the mesh component.</param>
        /// <param name="sceneSettings">Scene-level settings that should be persisted.</param>
        /// <returns>Absolute path to the written `.helen` file.</returns>
        string SaveSceneAsset(string fileName, string entityName, SceneAssetReference modelReference, SceneAssetReference materialReference, SceneSettingsAsset sceneSettings) {
            EditorEntity root = Assert.IsType<EditorEntity>(Core.Instance.EntityFactory.Create(entityName));
            MeshComponent meshComponent = new MeshComponent {
                Model = new TestRuntimeModel(),
                Material = new TestRuntimeMaterial()
            };
            root.AddComponent(meshComponent);
            EntitySaveComponent saveComponent = GetSaveComponent(root);
            saveComponent.SetAssetReference(meshComponent, "Model", modelReference);
            saveComponent.SetAssetReference(meshComponent, "Material", materialReference);

            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, CreatePersistenceRegistry());
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", fileName);
            saveService.Save(scenePath, sceneSettings);
            root.Enabled = false;
            Core.Instance.ObjectManager.RemoveEntity(root);
            return scenePath;
        }

        /// <summary>
        /// Writes one scene file containing a baked demo menu root and generated hierarchy.
        /// </summary>
        /// <param name="fileName">Scene file name to write.</param>
        /// <param name="providerTypeName">Assembly-qualified provider type stored in the baked root metadata.</param>
        /// <returns>Absolute path to the written `.helen` file.</returns>
        string SaveBakedMenuSceneAsset(string fileName, string providerTypeName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("Scene file name must be provided.", nameof(fileName));
            }
            if (string.IsNullOrWhiteSpace(providerTypeName)) {
                throw new ArgumentException("Provider type name must be provided.", nameof(providerTypeName));
            }

            DemoMenuSceneAssetFactory factory = new DemoMenuSceneAssetFactory();
            SceneAsset sceneAsset = factory.BuildSceneAsset("Scenes/TestMenu.helen", providerTypeName, BuildDemoMenuDefinition());

            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", fileName);
            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
            return scenePath;
        }

        /// <summary>
        /// Registers deterministic font assets for the baked demo menu references.
        /// </summary>
        /// <param name="resolver">Resolver that should receive the registered font assets.</param>
        void RegisterDemoMenuFonts(TestSceneAssetReferenceResolver resolver) {
            if (resolver == null) {
                throw new ArgumentNullException(nameof(resolver));
            }

            FontAsset font = CreateFont();
            resolver.RegisterFont(
                new SceneAssetReference {
                    SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                    RelativePath = "Fonts/DemoDiscTitle.hefont",
                    ProviderId = string.Empty,
                    AssetId = string.Empty
                },
                font);
            resolver.RegisterFont(
                new SceneAssetReference {
                    SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                    RelativePath = "Fonts/DemoDiscBody.hefont",
                    ProviderId = string.Empty,
                    AssetId = string.Empty
                },
                font);
        }

        /// <summary>
        /// Creates one small deterministic baked menu definition for editor scene load tests.
        /// </summary>
        /// <returns>Demo menu definition used by the scene load fixture.</returns>
        MenuDefinition BuildDemoMenuDefinition() {
            return new MenuDefinition(
                "Demo",
                "Preview",
                "main",
                "Fonts/DemoDiscTitle.hefont",
                "Fonts/DemoDiscBody.hefont",
                new byte4(10, 10, 20, 255),
                new byte4(30, 30, 50, 255),
                new byte4(60, 60, 90, 255),
                new byte4(120, 120, 255, 255),
                new byte4(80, 180, 200, 255),
                new byte4(255, 255, 255, 255),
                new byte4(210, 210, 220, 255),
                new[] {
                    new MenuPanelDefinition(
                        "main",
                        "Main Menu",
                        "Scene load test panel.",
                        4,
                        new[] {
                            new MenuItemDefinition("select-scene", "Select Scene", "Loads a scene.", true, new MenuActionDefinition(MenuActionKind.LoadScene, "Scenes/TestPlayableScene.helen")),
                            new MenuItemDefinition("back", "Back", "Returns.", true, new MenuActionDefinition(MenuActionKind.Back, string.Empty))
                        })
                });
        }

        /// <summary>
        /// Writes one minimal playable scene asset referenced by the test menu definition.
        /// </summary>
        /// <param name="relativeScenePath">Project-relative scene path to create beneath the temporary assets root.</param>
        void WritePlayableSceneAsset(string relativeScenePath) {
            if (string.IsNullOrWhiteSpace(relativeScenePath)) {
                throw new ArgumentException("Relative scene path must be provided.", nameof(relativeScenePath));
            }

            string scenePath = Path.Combine(TempProjectRootPath, relativeScenePath.Replace('/', Path.DirectorySeparatorChar));
            string sceneDirectoryPath = Path.GetDirectoryName(scenePath);
            if (string.IsNullOrWhiteSpace(sceneDirectoryPath)) {
                throw new InvalidOperationException("Playable scene directory could not be resolved.");
            }

            Directory.CreateDirectory(sceneDirectoryPath);
            SceneAsset sceneAsset = new SceneAsset {
                RootEntities = Array.Empty<SceneEntityAsset>()
            };

            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
        }

        /// <summary>
        /// Retrieves the hidden save component attached to one editor entity.
        /// </summary>
        /// <param name="entity">Entity whose save component should be returned.</param>
        /// <returns>Attached hidden save component.</returns>
        EntitySaveComponent GetSaveComponent(EditorEntity entity) {
            return Assert.IsType<EntitySaveComponent>(Assert.Single(entity.Components, component => component is EntitySaveComponent));
        }

        /// <summary>
        /// Writes one deterministic packaged font asset to the temporary content root.
        /// </summary>
        /// <param name="relativePath">Project-relative font asset path.</param>
        /// <param name="fontAsset">Font asset written to disk.</param>
        void WriteFontAsset(string relativePath, FontAsset fontAsset) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }
            if (fontAsset == null) {
                throw new ArgumentNullException(nameof(fontAsset));
            }

            string fullPath = Path.Combine(TempProjectRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath)) {
                throw new InvalidOperationException("Font asset directory could not be resolved.");
            }

            Directory.CreateDirectory(directoryPath);
            using FileStream stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            FontAssetBinarySerializer.Serialize(stream, fontAsset);
        }

        /// <summary>
        /// Creates one deterministic font asset containing the glyphs required by the menu preview labels.
        /// </summary>
        /// <returns>Font asset with stable glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            for (char character = 'A'; character <= 'Z'; character++) {
                characters[character] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f);
            }

            for (char character = 'a'; character <= 'z'; character++) {
                characters[character] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f);
            }

            characters['i'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f);
            characters['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f);
            characters['r'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f);
            characters['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f);
            characters['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f);
            characters['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f);
            characters[' '] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f);
            characters['.'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f);

            FontAsset fontAsset = new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
            fontAsset.SourceTextureAsset = new TextureAsset {
                Width = 64,
                Height = 64,
                Colors = new byte[64 * 64 * 4]
            };
            return fontAsset;
        }

        /// <summary>
        /// Creates one generated model reference used by the saved mesh component.
        /// </summary>
        /// <returns>Stable generated model reference.</returns>
        SceneAssetReference CreateGeneratedModelReference() {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "Engine/Models/Cube",
                ProviderId = "engine",
                AssetId = EngineGeneratedModelCache.CubeAssetId
            };
        }

        /// <summary>
        /// Creates one generated material reference used by the saved mesh component.
        /// </summary>
        /// <returns>Stable generated material reference.</returns>
        SceneAssetReference CreateGeneratedMaterialReference() {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = EngineGeneratedAssetProvider.StandardMaterialRelativePath,
                ProviderId = EngineGeneratedAssetProvider.ProviderIdValue,
                AssetId = EngineGeneratedMaterialCache.StandardAssetId
            };
        }
    }
}
