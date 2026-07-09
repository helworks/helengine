using System.Reflection;
using helengine.directx11;
using helengine.editor;
using helengine.editor.tests.testing;
using helengine.ui;
using helengine.vulkan;
using Xunit;

namespace helengine.editor.tests.serialization.blueprint {
    /// <summary>
    /// Verifies blueprint save and load services for user-authored editor entities.
    /// </summary>
    public class BlueprintSaveServiceTests : IDisposable {
        /// <summary>
        /// Temporary project root used for blueprint save outputs.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes a temporary project root and the core services required for blueprint serialization.
        /// </summary>
        public BlueprintSaveServiceTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-blueprint-save-service-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Blueprints"));

            EditorCore core = new EditorCore(new Project {
                Name = "Blueprint Save Service",
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
        /// Ensures blueprint save writes one `.hblueprint` file and round-trips stable entity ids, component keys, asset references, and platform overrides.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenBlueprintContainsSingleUserRoot_RoundTripsRootAndMetadata() {
            SceneAssetReference modelReference = global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateEngineCubeModel();
            SceneAssetReference materialReference = global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateEngineStandardMaterial();

            EditorEntity root = CreateUserEntity("Blueprint Root", new float3(1f, 2f, 3f), new float3(2f, 2f, 2f), new float4(0f, 0.70710677f, 0f, 0.70710677f));
            MeshComponent meshComponent = new MeshComponent {
                Model = new TestRuntimeModel(),
                Materials = new RuntimeMaterial[] { new TestRuntimeMaterial() },
                RenderOrder3D = 7
            };
            root.AddComponent(meshComponent);
            EditorEntity child = CreateUserEntity("Blueprint Child", new float3(5f, 6f, 7f), float3.One, float4.Identity);
            root.AddChild(child);

            EntitySaveComponent saveComponent = GetSaveComponent(root);
            saveComponent.SetAssetReference(meshComponent, "Model", modelReference);
            saveComponent.SetAssetReference(meshComponent, "Materials[0]", materialReference);
            saveComponent.SetTransformPlatformOverride("windows", new SceneEntityPlatformTransformOverrideAsset {
                PlatformId = "windows",
                HasLocalPositionOverride = true,
                LocalPosition = new float3(10f, 20f, 30f)
            });

            AttachPlatformOverridePayload(root, meshComponent, "windows", new byte[] { 9, 8, 7, 6 });

            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            BlueprintSaveService saveService = new BlueprintSaveService(TempProjectRootPath, registry);
            string blueprintPath = Path.Combine(TempProjectRootPath, "assets", "Blueprints", "RoundTrip.hblueprint");

            saveService.Save(blueprintPath);

            BlueprintAsset asset;
            using (FileStream stream = File.OpenRead(blueprintPath)) {
                asset = Assert.IsType<BlueprintAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Equal("Blueprints/RoundTrip.hblueprint", asset.Id);
            Assert.NotNull(asset.RootEntity);
            Assert.NotEqual(0u, asset.RootEntity.Id);
            Assert.Equal("Blueprint Root", asset.RootEntity.Name);
            Assert.False(string.IsNullOrWhiteSpace(Assert.Single(asset.RootEntity.Components).ComponentKey));
            Assert.Single(asset.RootEntity.Children);
            Assert.Equal(2, asset.AssetReferences.Length);
            Assert.Single(asset.RootEntity.PlatformTransformOverrides);
            Assert.Equal("windows", asset.RootEntity.PlatformTransformOverrides[0].PlatformId);
            Assert.True(asset.RootEntity.PlatformTransformOverrides[0].HasLocalPositionOverride);

            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            TestRuntimeModel loadedModel = new TestRuntimeModel();
            TestRuntimeMaterial loadedMaterial = new TestRuntimeMaterial();
            resolver.RegisterModel(modelReference, loadedModel);
            resolver.RegisterMaterial(materialReference, loadedMaterial);
            BlueprintLoadService loadService = new BlueprintLoadService(registry, resolver);

            LoadedEditorBlueprintDocument loaded = loadService.Load(asset);

            Assert.NotNull(loaded.RootEntity);
            EditorEntity loadedRoot = loaded.RootEntity;
            Assert.Equal("Blueprint Root", loadedRoot.Name);
            Assert.Equal(new float3(1f, 2f, 3f), loadedRoot.LocalPosition);
            Assert.Equal(new float3(2f, 2f, 2f), loadedRoot.LocalScale);
            Assert.Equal(new float4(0f, 0.70710677f, 0f, 0.70710677f), loadedRoot.LocalOrientation);
            Assert.Equal(asset.RootEntity.Id, GetSaveComponent(loadedRoot).EntityId);

            MeshComponent loadedMesh = Assert.IsType<MeshComponent>(Assert.Single(loadedRoot.Components, component => component is MeshComponent));
            Assert.Same(loadedModel, loadedMesh.Model);
            Assert.Same(loadedMaterial, Assert.Single(loadedMesh.Materials));
            Assert.True(GetSaveComponent(loadedRoot).TryGetComponentState(loadedMesh, out EntityComponentSaveState loadedSaveState));
            Assert.True(loadedSaveState.TryGetAssetReference("Model", out SceneAssetReference loadedModelReference));
            Assert.True(loadedSaveState.TryGetAssetReference("Materials[0]", out SceneAssetReference loadedMaterialReference));
            Assert.Equal(modelReference.AssetId, loadedModelReference.AssetId);
            Assert.Equal(materialReference.AssetId, loadedMaterialReference.AssetId);
            Assert.True(TryGetPlatformOverride(loadedSaveState, "windows", out object loadedOverrideState));

            Type overrideStateType = ResolveRequiredType("helengine.EntityComponentPlatformOverrideState");
            byte[] loadedPayload = Assert.IsType<byte[]>(overrideStateType.GetProperty("Payload").GetValue(loadedOverrideState));
            Assert.Equal(new byte[] { 9, 8, 7, 6 }, loadedPayload);
        }

        /// <summary>
        /// Ensures blueprint save rejects an authoring state with no editable root entities.
        /// </summary>
        [Fact]
        public void Save_WhenBlueprintContainsNoEditableRoot_ThrowsInvalidOperationException() {
            BlueprintSaveService saveService = new BlueprintSaveService(TempProjectRootPath, new ComponentPersistenceRegistry());
            string blueprintPath = Path.Combine(TempProjectRootPath, "assets", "Blueprints", "Empty.hblueprint");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => saveService.Save(blueprintPath));

            Assert.Contains("exactly one editable root", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures blueprint save rejects an authoring state with multiple editable root entities.
        /// </summary>
        [Fact]
        public void Save_WhenBlueprintContainsMultipleEditableRoots_ThrowsInvalidOperationException() {
            CreateUserEntity("Root A", float3.Zero, float3.One, float4.Identity);
            CreateUserEntity("Root B", float3.Zero, float3.One, float4.Identity);
            BlueprintSaveService saveService = new BlueprintSaveService(TempProjectRootPath, new ComponentPersistenceRegistry());
            string blueprintPath = Path.Combine(TempProjectRootPath, "assets", "Blueprints", "Multi.hblueprint");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => saveService.Save(blueprintPath));

            Assert.Contains("exactly one editable root", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures blueprint save rejects nested blueprint-instance components in v1.
        /// </summary>
        [Fact]
        public void Save_WhenBlueprintContainsNestedBlueprintInstanceComponent_ThrowsInvalidOperationException() {
            EditorEntity root = CreateUserEntity("Root", float3.Zero, float3.One, float4.Identity);
            EditorEntity child = CreateUserEntity("Child", float3.Zero, float3.One, float4.Identity);
            child.AddComponent(new BlueprintInstanceComponent());
            root.AddChild(child);

            BlueprintSaveService saveService = new BlueprintSaveService(TempProjectRootPath, new ComponentPersistenceRegistry());
            string blueprintPath = Path.Combine(TempProjectRootPath, "assets", "Blueprints", "NestedInstance.hblueprint");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => saveService.Save(blueprintPath));

            Assert.Contains("nested blueprint instances", exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Creates one user-authored editor entity configured for blueprint serialization.
        /// </summary>
        /// <param name="name">Display name assigned to the entity.</param>
        /// <param name="position">Local position assigned to the entity.</param>
        /// <param name="scale">Local scale assigned to the entity.</param>
        /// <param name="orientation">Local orientation assigned to the entity.</param>
        /// <returns>Configured editor entity.</returns>
        EditorEntity CreateUserEntity(string name, float3 position, float3 scale, float4 orientation) {
            EditorEntity entity = Assert.IsType<EditorEntity>(Core.Instance.EntityFactory.Create(name));
            entity.LocalPosition = position;
            entity.LocalScale = scale;
            entity.LocalOrientation = orientation;
            return entity;
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
        /// Attaches one editor-only platform override payload to one persisted component.
        /// </summary>
        /// <param name="entity">Entity that owns the component.</param>
        /// <param name="component">Component whose save-state should receive the override.</param>
        /// <param name="platformId">Platform identifier that owns the override.</param>
        /// <param name="payload">Serialized override payload.</param>
        void AttachPlatformOverridePayload(EditorEntity entity, Component component, string platformId, byte[] payload) {
            EntitySaveComponent saveComponent = GetSaveComponent(entity);
            EntityComponentSaveState saveState = saveComponent.GetOrCreateComponentState(component);
            Type overrideStateType = ResolveRequiredType("helengine.EntityComponentPlatformOverrideState");
            object overrideState = Activator.CreateInstance(overrideStateType);

            overrideStateType.GetProperty("PlatformId").SetValue(overrideState, platformId);
            overrideStateType.GetProperty("Payload").SetValue(overrideState, payload);

            MethodInfo setMethod = typeof(EntityComponentSaveState).GetMethod("SetPlatformOverride", BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(setMethod);
            setMethod.Invoke(saveState, new[] { platformId, overrideState });
        }

        /// <summary>
        /// Attempts to read one platform override entry from one component save-state.
        /// </summary>
        /// <param name="saveState">Component save-state that owns the override entries.</param>
        /// <param name="platformId">Platform identifier to resolve.</param>
        /// <param name="overrideState">Resolved override entry when one exists.</param>
        /// <returns>True when the requested override exists.</returns>
        bool TryGetPlatformOverride(EntityComponentSaveState saveState, string platformId, out object overrideState) {
            MethodInfo tryGetMethod = typeof(EntityComponentSaveState).GetMethod("TryGetPlatformOverride", BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(tryGetMethod);

            object[] arguments = new object[] { platformId, null };
            bool found = Assert.IsType<bool>(tryGetMethod.Invoke(saveState, arguments));
            overrideState = arguments[1];
            return found;
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

        /// <summary>
        /// Dummy component used to exercise the v1 nested-blueprint validation before the real component ships.
        /// </summary>
        sealed class BlueprintInstanceComponent : Component {
        }
    }
}
