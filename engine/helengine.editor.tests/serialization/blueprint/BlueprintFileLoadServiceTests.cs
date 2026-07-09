using System.Reflection;
using helengine.directx11;
using helengine.editor;
using helengine.editor.tests.testing;
using helengine.ui;
using helengine.vulkan;
using Xunit;

namespace helengine.editor.tests.serialization.blueprint {
    /// <summary>
    /// Verifies loading `.hblueprint` files into live editor entities.
    /// </summary>
    public class BlueprintFileLoadServiceTests : IDisposable {
        /// <summary>
        /// Temporary project root used by blueprint file load tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes a temporary project root and the core services required for blueprint loading.
        /// </summary>
        public BlueprintFileLoadServiceTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-blueprint-file-load-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Blueprints"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scenes"));

            EditorCore core = new EditorCore(new Project {
                Name = "Blueprint File Load",
                Path = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"), new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(TempProjectRootPath)
            });
            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());
            shaderBackendRegistry.Register(new VulkanShaderBackend());
            EditorBuiltInShaderAssetLibrary.ConfigureShaderBackends(shaderBackendRegistry);
        }

        /// <summary>
        /// Deletes temporary project state after each test.
        /// </summary>
        public void Dispose() {
            EditorCameraVisualResources.ResetForTests();
            EditorPointLightVisualResources.ResetForTests();
            EditorDirectionalLightVisualResources.ResetForTests();
            EditorSpotLightVisualResources.ResetForTests();
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures a saved `.hblueprint` file materializes into exactly one editable root entity.
        /// </summary>
        [Fact]
        public void Load_WhenBlueprintFileExists_ReturnsEditableRootEntity() {
            SceneAssetReference modelReference = global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateEngineCubeModel();
            SceneAssetReference materialReference = global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateEngineStandardMaterial();
            string blueprintPath = WriteBlueprintAsset("Loaded.hblueprint", "Loaded Blueprint Root", modelReference, materialReference);
            BlueprintFileLoadService loadService = CreateLoadService(modelReference, materialReference);

            LoadedEditorBlueprintDocument loaded = loadService.Load(blueprintPath);

            Assert.NotNull(loaded.RootEntity);
            EditorEntity root = loaded.RootEntity;
            Assert.Equal("Loaded Blueprint Root", root.Name);
            Assert.False(root.Enabled);
            Assert.False(root.InternalEntity);
            EditorEntity child = Assert.IsType<EditorEntity>(Assert.Single(root.Children));
            Assert.Equal("Blueprint Child", child.Name);

            MeshComponent loadedMesh = Assert.IsType<MeshComponent>(Assert.Single(root.Components, component => component is MeshComponent));
            EntitySaveComponent saveComponent = GetSaveComponent(root);
            Assert.True(saveComponent.TryGetComponentState(loadedMesh, out EntityComponentSaveState loadedSaveState));
            Assert.True(loadedSaveState.TryGetAssetReference("Model", out SceneAssetReference loadedModelReference));
            Assert.True(loadedSaveState.TryGetAssetReference("Materials[0]", out SceneAssetReference loadedMaterialReference));
            Assert.Equal(modelReference.AssetId, loadedModelReference.AssetId);
            Assert.Equal(materialReference.AssetId, loadedMaterialReference.AssetId);
        }

        /// <summary>
        /// Ensures invalid blueprint files fail with a clear exception.
        /// </summary>
        [Fact]
        public void Load_WhenBlueprintFileIsInvalid_ThrowsInvalidOperationException() {
            string blueprintPath = Path.Combine(TempProjectRootPath, "assets", "Blueprints", "Broken.hblueprint");
            File.WriteAllText(blueprintPath, "not-a-blueprint");
            BlueprintFileLoadService loadService = CreateLoadService(
                global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateEngineCubeModel(),
                global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateEngineStandardMaterial());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => loadService.Load(blueprintPath));

            Assert.Contains("Blueprint load failed", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Creates a blueprint file-load service with runtime references registered for a saved mesh component.
        /// </summary>
        /// <param name="modelReference">Model reference to resolve during load.</param>
        /// <param name="materialReference">Material reference to resolve during load.</param>
        /// <returns>Configured blueprint file-load service.</returns>
        BlueprintFileLoadService CreateLoadService(SceneAssetReference modelReference, SceneAssetReference materialReference) {
            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            resolver.RegisterModel(modelReference, new TestRuntimeModel());
            resolver.RegisterMaterial(materialReference, new TestRuntimeMaterial());
            return new BlueprintFileLoadService(TempProjectRootPath, CreatePersistenceRegistry(), resolver);
        }

        /// <summary>
        /// Creates the component persistence registry used by blueprint save and load tests.
        /// </summary>
        /// <returns>Configured component persistence registry.</returns>
        ComponentPersistenceRegistry CreatePersistenceRegistry() {
            return new ComponentPersistenceRegistry();
        }

        /// <summary>
        /// Writes one blueprint file by reusing the scene serializer for the subtree payload.
        /// </summary>
        /// <param name="fileName">Blueprint file name to write.</param>
        /// <param name="entityName">Name assigned to the serialized blueprint root.</param>
        /// <param name="modelReference">Model reference persisted with the mesh component.</param>
        /// <param name="materialReference">Material reference persisted with the mesh component.</param>
        /// <returns>Absolute path to the written `.hblueprint` file.</returns>
        string WriteBlueprintAsset(string fileName, string entityName, SceneAssetReference modelReference, SceneAssetReference materialReference) {
            EditorEntity root = Assert.IsType<EditorEntity>(Core.Instance.EntityFactory.Create(entityName));
            root.LocalPosition = new float3(1f, 2f, 3f);
            root.LocalScale = new float3(2f, 2f, 2f);
            root.LocalOrientation = new float4(0f, 0.70710677f, 0f, 0.70710677f);

            MeshComponent meshComponent = new MeshComponent {
                Model = new TestRuntimeModel(),
                Materials = new RuntimeMaterial[] { new TestRuntimeMaterial() },
                RenderOrder3D = 4
            };
            root.AddComponent(meshComponent);

            EditorEntity child = Assert.IsType<EditorEntity>(Core.Instance.EntityFactory.Create("Blueprint Child"));
            root.AddChild(child);

            EntitySaveComponent saveComponent = GetSaveComponent(root);
            saveComponent.SetAssetReference(meshComponent, "Model", modelReference);
            saveComponent.SetAssetReference(meshComponent, "Materials[0]", materialReference);

            Type overrideStateType = ResolveRequiredType("helengine.EntityComponentPlatformOverrideState");
            object overrideState = Activator.CreateInstance(overrideStateType);
            overrideStateType.GetProperty("PlatformId").SetValue(overrideState, "windows");
            overrideStateType.GetProperty("Payload").SetValue(overrideState, new byte[] { 1, 2, 3, 4 });
            MethodInfo setPlatformOverrideMethod = typeof(EntityComponentSaveState).GetMethod("SetPlatformOverride", BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(setPlatformOverrideMethod);
            EntityComponentSaveState componentSaveState = saveComponent.GetOrCreateComponentState(meshComponent);
            setPlatformOverrideMethod.Invoke(componentSaveState, new[] { "windows", overrideState });

            SceneSaveService sceneSaveService = new SceneSaveService(TempProjectRootPath, CreatePersistenceRegistry());
            string tempScenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "BlueprintSource.helen");
            sceneSaveService.Save(tempScenePath);

            SceneAsset sceneAsset;
            using (FileStream stream = File.OpenRead(tempScenePath)) {
                sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            string blueprintPath = Path.Combine(TempProjectRootPath, "assets", "Blueprints", fileName);
            using (FileStream stream = File.Create(blueprintPath)) {
                AssetSerializer.Serialize(stream, new BlueprintAsset {
                    Id = "Blueprints/" + fileName,
                    RootEntity = Assert.Single(sceneAsset.RootEntities),
                    AssetReferences = sceneAsset.AssetReferences
                });
            }

            root.Enabled = false;
            child.Enabled = false;
            Core.Instance.ObjectManager.RemoveEntity(root);
            return blueprintPath;
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
        /// Resolves one required editor runtime type by full name.
        /// </summary>
        /// <param name="typeName">Full type name to resolve from the editor assembly.</param>
        /// <returns>Resolved runtime type.</returns>
        Type ResolveRequiredType(string typeName) {
            Type type = typeof(EntityComponentSaveState).Assembly.GetType(typeName, false);
            Assert.NotNull(type);
            return type;
        }
    }
}
