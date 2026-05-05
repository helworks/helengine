using helengine.editor;
using helengine.editor.tests.testing;
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

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
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

            IReadOnlyList<EditorEntity> loaded = loadService.Load(scenePath);

            EditorEntity root = Assert.Single(loaded);
            Assert.Equal("Loaded Cube", root.Name);
            Assert.False(root.Enabled);
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
            EditorEntity existing = new EditorEntity {
                Name = "Existing",
                LayerMask = EditorLayerMasks.SceneObjects
            };

            Assert.Throws<InvalidOperationException>(() => loadService.Load(scenePath));

            Assert.Contains(Core.Instance.ObjectManager.Entities, entity => ReferenceEquals(entity, existing));
            Assert.DoesNotContain(Core.Instance.ObjectManager.Entities, entity => entity is EditorEntity editorEntity && string.Equals(editorEntity.Name, "Transient Root", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures editor scene loading can materialize a menu-host component without requiring the game script assembly to be loaded.
        /// </summary>
        [Fact]
        public void Load_WhenSceneContainsMenuHostComponent_LoadsWithoutInitializingRuntimeMenuHost() {
            string scenePath = SaveMenuHostSceneAsset("MenuHost.helen", "Menu Root", "city.menu.DemoDiscMenuDefinitionProvider, city");
            SceneFileLoadService loadService = new SceneFileLoadService(TempProjectRootPath, CreateMenuHostPersistenceRegistry(), new TestSceneAssetReferenceResolver());

            IReadOnlyList<EditorEntity> loaded = loadService.Load(scenePath);

            EditorEntity root = Assert.Single(loaded);
            MenuHostComponent menuHostComponent = Assert.IsType<MenuHostComponent>(Assert.Single(root.Components, component => component is MenuHostComponent));
            Assert.Equal("city.menu.DemoDiscMenuDefinitionProvider, city", menuHostComponent.ProviderTypeName);
            Assert.False(menuHostComponent.IsInitialized);
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
        /// Creates the component persistence registry required to load menu-host scene records.
        /// </summary>
        /// <returns>Configured persistence registry containing menu-host support.</returns>
        ComponentPersistenceRegistry CreateMenuHostPersistenceRegistry() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            registry.Register(new MenuHostComponentPersistenceDescriptor());
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
            EditorEntity root = new EditorEntity {
                Name = entityName,
                LayerMask = EditorLayerMasks.SceneObjects
            };
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
            saveService.Save(scenePath);
            root.Enabled = false;
            Core.Instance.ObjectManager.RemoveEntity(root);
            return scenePath;
        }

        /// <summary>
        /// Writes one scene file containing a single menu-host entity with an authored provider type name.
        /// </summary>
        /// <param name="fileName">Scene file name to write.</param>
        /// <param name="entityName">Name assigned to the saved root entity.</param>
        /// <param name="providerTypeName">Assembly-qualified provider type stored in the menu-host payload.</param>
        /// <returns>Absolute path to the written `.helen` file.</returns>
        string SaveMenuHostSceneAsset(string fileName, string entityName, string providerTypeName) {
            if (string.IsNullOrWhiteSpace(fileName)) {
                throw new ArgumentException("Scene file name must be provided.", nameof(fileName));
            }
            if (string.IsNullOrWhiteSpace(entityName)) {
                throw new ArgumentException("Entity name must be provided.", nameof(entityName));
            }
            if (string.IsNullOrWhiteSpace(providerTypeName)) {
                throw new ArgumentException("Provider type name must be provided.", nameof(providerTypeName));
            }

            SceneAsset sceneAsset = new SceneAsset {
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = "menu-root",
                        Name = entityName,
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            new SceneComponentAssetRecord {
                                ComponentTypeId = MenuHostComponent.SerializedComponentTypeId,
                                ComponentIndex = 0,
                                Payload = WriteMenuHostComponentPayload(providerTypeName)
                            }
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };

            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", fileName);
            using FileStream stream = new FileStream(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
            AssetSerializer.Serialize(stream, sceneAsset);
            return scenePath;
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
        /// Writes one menu-host component payload using the current scene serialization contract.
        /// </summary>
        /// <param name="providerTypeName">Assembly-qualified provider type stored in the payload.</param>
        /// <returns>Serialized menu-host component payload.</returns>
        byte[] WriteMenuHostComponentPayload(string providerTypeName) {
            if (string.IsNullOrWhiteSpace(providerTypeName)) {
                throw new ArgumentException("Provider type name must be provided.", nameof(providerTypeName));
            }

            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(MenuHostComponent.CurrentVersion);
            writer.WriteString(providerTypeName);
            return stream.ToArray();
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
