using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies scene save and load services for user-authored editor entities.
    /// </summary>
    public class SceneSaveServiceTests : IDisposable {
        /// <summary>
        /// Temporary project root used for scene save outputs.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes a temporary project root and the core services required for scene serialization.
        /// </summary>
        public SceneSaveServiceTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-save-service-tests", Guid.NewGuid().ToString("N"));
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
            EditorCameraVisualResources.ResetForTests();
            EditorPointLightVisualResources.ResetForTests();
            EditorDirectionalLightVisualResources.ResetForTests();
            EditorSpotLightVisualResources.ResetForTests();
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures scene save writes one `.helen` file, excludes internal editor entities, and round-trips mesh persistence through the load service.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenSceneContainsUserEntities_RoundTripsUserHierarchyAndExcludesInternalEntities() {
            SceneAssetReference modelReference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "Engine/Models/Cube",
                ProviderId = "engine",
                AssetId = EngineGeneratedModelCache.CubeAssetId
            };
            SceneAssetReference materialReference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = "Materials/Default.helmat"
            };

            EditorEntity root = CreateUserEntity("Root", new float3(1f, 2f, 3f), new float3(2f, 2f, 2f), new float4(0f, 0.70710677f, 0f, 0.70710677f));
            MeshComponent rootMesh = new MeshComponent {
                Model = new TestRuntimeModel(),
                Material = new TestRuntimeMaterial(),
                RenderOrder3D = 5
            };
            root.AddComponent(rootMesh);
            GetSaveComponent(root).SetAssetReference(rootMesh, "Model", modelReference);
            GetSaveComponent(root).SetAssetReference(rootMesh, "Material", materialReference);

            EditorEntity child = CreateUserEntity("Child", new float3(5f, 6f, 7f), float3.One, float4.Identity);
            root.AddChild(child);

            EditorEntity internalEntity = CreateUserEntity("Internal", new float3(9f, 9f, 9f), float3.One, float4.Identity);
            internalEntity.InternalEntity = true;

            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            registry.Register(new MeshComponentPersistenceDescriptor());
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "RoundTrip.helen");

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Equal("Scenes/RoundTrip.helen", asset.Id);
            Assert.Equal(2, asset.AssetReferences.Length);
            Assert.Contains(asset.AssetReferences, reference =>
                reference.SourceKind == modelReference.SourceKind &&
                reference.RelativePath == modelReference.RelativePath &&
                reference.ProviderId == modelReference.ProviderId &&
                reference.AssetId == modelReference.AssetId);
            Assert.Contains(asset.AssetReferences, reference =>
                reference.SourceKind == materialReference.SourceKind &&
                reference.RelativePath == materialReference.RelativePath);
            Assert.Single(asset.RootEntities);
            Assert.False(string.IsNullOrWhiteSpace(asset.RootEntities[0].Id));
            Assert.Equal("Root", asset.RootEntities[0].Name);
            Assert.Single(asset.RootEntities[0].Children);
            Assert.Equal("Child", asset.RootEntities[0].Children[0].Name);

            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            TestRuntimeModel loadedModel = new TestRuntimeModel();
            TestRuntimeMaterial loadedMaterial = new TestRuntimeMaterial();
            resolver.RegisterModel(modelReference, loadedModel);
            resolver.RegisterMaterial(materialReference, loadedMaterial);

            SceneLoadService loadService = new SceneLoadService(registry, resolver);
            IReadOnlyList<EditorEntity> loadedRoots = loadService.Load(asset);

            EditorEntity loadedRoot = Assert.Single(loadedRoots);
            Assert.Equal("Root", loadedRoot.Name);
            Assert.Equal(new float3(1f, 2f, 3f), loadedRoot.LocalPosition);
            Assert.Equal(new float3(2f, 2f, 2f), loadedRoot.LocalScale);
            Assert.Equal(new float4(0f, 0.70710677f, 0f, 0.70710677f), loadedRoot.LocalOrientation);
            Assert.Equal(asset.RootEntities[0].Id, GetSaveComponent(loadedRoot).EntityId);
            Assert.Single(loadedRoot.Children);
            Assert.Equal("Child", ((EditorEntity)loadedRoot.Children[0]).Name);

            MeshComponent loadedMesh = FindMeshComponent(loadedRoot);
            Assert.Same(loadedModel, loadedMesh.Model);
            Assert.Same(loadedMaterial, loadedMesh.Material);
            Assert.Equal((byte)5, loadedMesh.RenderOrder3D);
            Assert.True(GetSaveComponent(loadedRoot).TryGetComponentState(loadedMesh, out EntityComponentSaveState loadedSaveState));
            Assert.True(loadedSaveState.TryGetAssetReference("Model", out SceneAssetReference loadedModelReference));
            Assert.Equal(modelReference.AssetId, loadedModelReference.AssetId);
        }

        /// <summary>
        /// Ensures scene save collects multiple text font references into the generic scene dependency manifest.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenSceneContainsMultipleTextComponents_CollectsAllFontReferences() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            registry.Register(new TextComponentPersistenceDescriptor());
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "TextRoundTrip.helen");

            SceneAssetReference titleFontReference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "Fonts/Title",
                ProviderId = "fonts",
                AssetId = "title"
            };
            SceneAssetReference bodyFontReference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "Fonts/Body",
                ProviderId = "fonts",
                AssetId = "body"
            };

            EditorEntity root = CreateUserEntity("Root", float3.Zero, float3.One, float4.Identity);
            TextComponent titleText = new TextComponent {
                Font = CreateFont("Title"),
                Text = "Title",
                WrapText = false,
                Size = new int2(240, 48),
                Color = new byte4(255, 255, 255, 255),
                SourceRect = new float4(0f, 0f, 1f, 1f),
                Rotation = 0f,
                RenderOrder2D = 11,
                LayerMask = 3
            };
            root.AddComponent(titleText);
            GetSaveComponent(root).SetAssetReference(titleText, "Font", titleFontReference);

            EditorEntity child = CreateUserEntity("Child", new float3(0f, 32f, 0f), float3.One, float4.Identity);
            TextComponent bodyText = new TextComponent {
                Font = CreateFont("Body"),
                Text = "Body",
                WrapText = true,
                Size = new int2(320, 96),
                Color = new byte4(12, 34, 56, 255),
                SourceRect = new float4(0f, 0f, 1f, 1f),
                Rotation = 0f,
                RenderOrder2D = 12,
                LayerMask = 5
            };
            child.AddComponent(bodyText);
            root.AddChild(child);
            GetSaveComponent(child).SetAssetReference(bodyText, "Font", bodyFontReference);

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Equal(2, asset.AssetReferences.Length);
            Assert.Contains(asset.AssetReferences, reference =>
                reference.RelativePath == titleFontReference.RelativePath &&
                reference.ProviderId == titleFontReference.ProviderId &&
                reference.AssetId == titleFontReference.AssetId);
            Assert.Contains(asset.AssetReferences, reference =>
                reference.RelativePath == bodyFontReference.RelativePath &&
                reference.ProviderId == bodyFontReference.ProviderId &&
                reference.AssetId == bodyFontReference.AssetId);

            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            FontAsset loadedTitleFont = CreateFont("LoadedTitle");
            FontAsset loadedBodyFont = CreateFont("LoadedBody");
            resolver.RegisterFont(titleFontReference, loadedTitleFont);
            resolver.RegisterFont(bodyFontReference, loadedBodyFont);

            SceneLoadService loadService = new SceneLoadService(registry, resolver);
            IReadOnlyList<EditorEntity> loadedRoots = loadService.Load(asset);

            EditorEntity loadedRoot = Assert.Single(loadedRoots);
            TextComponent loadedTitleText = Assert.IsType<TextComponent>(Assert.Single(loadedRoot.Components, component => component is TextComponent));
            Assert.Same(loadedTitleFont, loadedTitleText.Font);
            Assert.Equal("Title", loadedTitleText.Text);

            EditorEntity loadedChild = Assert.IsType<EditorEntity>(Assert.Single(loadedRoot.Children));
            TextComponent loadedBodyText = Assert.IsType<TextComponent>(Assert.Single(loadedChild.Components, component => component is TextComponent));
            Assert.Same(loadedBodyFont, loadedBodyText.Font);
            Assert.Equal("Body", loadedBodyText.Text);
        }

        /// <summary>
        /// Ensures save fails clearly when one user component does not expose a persistence descriptor.
        /// </summary>
        [Fact]
        public void Save_WhenEntityContainsUnsupportedComponent_ThrowsClearError() {
            EditorEntity entity = CreateUserEntity("Unsupported", float3.Zero, float3.One, float4.Identity);
            entity.AddComponent(new AnchorComponent());
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "Unsupported.helen");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => saveService.Save(scenePath));

            Assert.Contains(nameof(AnchorComponent), exception.Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures eligible scripted components round-trip through the automatic reflected editor persistence fallback.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenSceneContainsEligibleScriptComponent_RoundTripsThroughAutomaticFallback() {
            EditorEntity entity = CreateUserEntity("Scripted", float3.Zero, float3.One, float4.Identity);
            TestScriptSerializableComponent component = new TestScriptSerializableComponent {
                DisplayName = "Runtime Widget",
                Visible = true,
                SortOrder = 9
            };
            entity.AddComponent(component);
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "ScriptedRoundTrip.helen");

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            SceneComponentAssetRecord record = Assert.Single(Assert.Single(asset.RootEntities).Components);
            Assert.Equal(
                AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(TestScriptSerializableComponent)),
                record.ComponentTypeId);

            SceneLoadService loadService = new SceneLoadService(registry, new TestSceneAssetReferenceResolver());
            IReadOnlyList<EditorEntity> loadedRoots = loadService.Load(asset);

            EditorEntity loadedEntity = Assert.Single(loadedRoots);
            TestScriptSerializableComponent loadedComponent = Assert.IsType<TestScriptSerializableComponent>(
                Assert.Single(loadedEntity.Components, loadedComponent => loadedComponent is TestScriptSerializableComponent));
            Assert.Equal("Runtime Widget", loadedComponent.DisplayName);
            Assert.True(loadedComponent.Visible);
            Assert.Equal(9, loadedComponent.SortOrder);
        }

        /// <summary>
        /// Ensures camera entities persist only the camera component and rebuild the hidden editor visual when loaded.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenSceneContainsCameraEntity_RoundTripsCameraAndReattachesHiddenEditorVisual() {
            EditorSceneCreationService creationService = new EditorSceneCreationService();
            EditorEntity cameraEntity = creationService.CreateCamera();
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            registry.Register(new CameraComponentPersistenceDescriptor());
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "CameraRoundTrip.helen");

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Single(asset.RootEntities);
            Assert.Single(asset.RootEntities[0].Components);
            Assert.Equal("helengine.CameraComponent", asset.RootEntities[0].Components[0].ComponentTypeId);

            SceneLoadService loadService = new SceneLoadService(registry, new TestSceneAssetReferenceResolver());
            IReadOnlyList<EditorEntity> loadedRoots = loadService.Load(asset);
            EditorEntity loadedCameraEntity = Assert.Single(loadedRoots);
            CameraComponent loadedCamera = Assert.IsType<CameraComponent>(Assert.Single(loadedCameraEntity.Components, component => component is CameraComponent));
            EditorSceneCameraSuppressionComponent suppressionComponent = Assert.IsType<EditorSceneCameraSuppressionComponent>(Assert.Single(loadedCameraEntity.Components, component => component is EditorSceneCameraSuppressionComponent));
            EditorEntity loadedVisualEntity = Assert.IsType<EditorEntity>(Assert.Single(loadedCameraEntity.Children));
            EditorCameraVisualComponent loadedVisual = Assert.IsType<EditorCameraVisualComponent>(Assert.Single(loadedVisualEntity.Components, component => component is EditorCameraVisualComponent));

            Assert.Equal((ushort)0, loadedCamera.LayerMask);
            Assert.False(loadedCamera.ClearSettings.ClearColorEnabled);
            Assert.Equal(EditorLayerMasks.SceneObjects, suppressionComponent.LayerMask);
            Assert.True(suppressionComponent.ClearSettings.ClearColorEnabled);
            Assert.True(loadedVisualEntity.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneCameraVisuals, loadedVisualEntity.LayerMask);
            Assert.NotNull(loadedVisual.Model);
            Assert.NotNull(loadedVisual.Material);
        }

        /// <summary>
        /// Ensures point light entities persist only the point-light component and rebuild the hidden editor visual when loaded.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenSceneContainsPointLightEntity_RoundTripsPointLightAndReattachesHiddenEditorVisual() {
            EditorSceneCreationService creationService = new EditorSceneCreationService();
            EditorEntity pointLightEntity = creationService.CreatePointLight();
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            registry.Register(new PointLightComponentPersistenceDescriptor());
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "PointLightRoundTrip.helen");

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Single(asset.RootEntities);
            Assert.Equal("Point Light", asset.RootEntities[0].Name);
            Assert.Single(asset.RootEntities[0].Components);
            Assert.Equal("helengine.PointLightComponent", asset.RootEntities[0].Components[0].ComponentTypeId);
            Assert.Empty(asset.RootEntities[0].Children);

            SceneLoadService loadService = new SceneLoadService(registry, new TestSceneAssetReferenceResolver());
            IReadOnlyList<EditorEntity> loadedRoots = loadService.Load(asset);

            EditorEntity loadedPointLightEntity = Assert.Single(loadedRoots);
            PointLightComponent loadedPointLight = Assert.IsType<PointLightComponent>(Assert.Single(loadedPointLightEntity.Components, component => component is PointLightComponent));
            EditorEntity loadedVisualEntity = Assert.IsType<EditorEntity>(Assert.Single(loadedPointLightEntity.Children));
            EditorPointLightVisualComponent loadedVisual = Assert.IsType<EditorPointLightVisualComponent>(Assert.Single(loadedVisualEntity.Components, component => component is EditorPointLightVisualComponent));

            Assert.Equal(10f, loadedPointLight.Range);
            Assert.True(loadedVisualEntity.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneCameraVisuals, loadedVisualEntity.LayerMask);
            Assert.NotNull(loadedVisual.Model);
            Assert.NotNull(loadedVisual.Material);
        }

        /// <summary>
        /// Ensures directional light entities persist only the directional-light component and rebuild the hidden editor arrow when loaded.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenSceneContainsDirectionalLightEntity_RoundTripsDirectionalLightAndReattachesHiddenEditorVisual() {
            EditorSceneCreationService creationService = new EditorSceneCreationService();
            EditorEntity directionalLightEntity = creationService.CreateDirectionalLight();
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            registry.Register(new DirectionalLightComponentPersistenceDescriptor());
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "DirectionalLightRoundTrip.helen");

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Single(asset.RootEntities);
            Assert.Equal("Directional Light", asset.RootEntities[0].Name);
            Assert.Single(asset.RootEntities[0].Components);
            Assert.Equal("helengine.DirectionalLightComponent", asset.RootEntities[0].Components[0].ComponentTypeId);
            Assert.Empty(asset.RootEntities[0].Children);

            SceneLoadService loadService = new SceneLoadService(registry, new TestSceneAssetReferenceResolver());
            IReadOnlyList<EditorEntity> loadedRoots = loadService.Load(asset);

            EditorEntity loadedDirectionalLightEntity = Assert.Single(loadedRoots);
            DirectionalLightComponent loadedDirectionalLight = Assert.IsType<DirectionalLightComponent>(Assert.Single(loadedDirectionalLightEntity.Components, component => component is DirectionalLightComponent));
            EditorEntity loadedVisualEntity = Assert.IsType<EditorEntity>(Assert.Single(loadedDirectionalLightEntity.Children));
            EditorDirectionalLightVisualComponent loadedVisual = Assert.IsType<EditorDirectionalLightVisualComponent>(Assert.Single(loadedVisualEntity.Components, component => component is EditorDirectionalLightVisualComponent));

            Assert.True(loadedDirectionalLight.ShadowsEnabled);
            Assert.True(loadedVisualEntity.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneCameraVisuals, loadedVisualEntity.LayerMask);
            Assert.NotNull(loadedVisual.Model);
            Assert.NotNull(loadedVisual.Material);
        }

        /// <summary>
        /// Ensures spot light entities persist only the spot-light component and rebuild the hidden editor cone when loaded.
        /// </summary>
        [Fact]
        public void SaveAndLoad_WhenSceneContainsSpotLightEntity_RoundTripsSpotLightAndReattachesHiddenEditorVisual() {
            EditorSceneCreationService creationService = new EditorSceneCreationService();
            EditorEntity spotLightEntity = creationService.CreateSpotLight();
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            registry.Register(new SpotLightComponentPersistenceDescriptor());
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "SpotLightRoundTrip.helen");

            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.Single(asset.RootEntities);
            Assert.Equal("Spot Light", asset.RootEntities[0].Name);
            Assert.Single(asset.RootEntities[0].Components);
            Assert.Equal("helengine.SpotLightComponent", asset.RootEntities[0].Components[0].ComponentTypeId);
            Assert.Empty(asset.RootEntities[0].Children);

            SceneLoadService loadService = new SceneLoadService(registry, new TestSceneAssetReferenceResolver());
            IReadOnlyList<EditorEntity> loadedRoots = loadService.Load(asset);

            EditorEntity loadedSpotLightEntity = Assert.Single(loadedRoots);
            SpotLightComponent loadedSpotLight = Assert.IsType<SpotLightComponent>(Assert.Single(loadedSpotLightEntity.Components, component => component is SpotLightComponent));
            EditorEntity loadedVisualEntity = Assert.IsType<EditorEntity>(Assert.Single(loadedSpotLightEntity.Children));
            EditorSpotLightVisualComponent loadedVisual = Assert.IsType<EditorSpotLightVisualComponent>(Assert.Single(loadedVisualEntity.Components, component => component is EditorSpotLightVisualComponent));

            Assert.Equal(10f, loadedSpotLight.Range);
            Assert.Equal(25f, loadedSpotLight.InnerConeAngleDegrees);
            Assert.Equal(45f, loadedSpotLight.OuterConeAngleDegrees);
            Assert.True(loadedVisualEntity.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneCameraVisuals, loadedVisualEntity.LayerMask);
            Assert.NotNull(loadedVisual.Model);
            Assert.NotNull(loadedVisual.Material);
        }

        /// <summary>
        /// Creates one user-authored editor entity configured for scene serialization.
        /// </summary>
        /// <param name="name">Display name assigned to the entity.</param>
        /// <param name="position">Local position assigned to the entity.</param>
        /// <param name="scale">Local scale assigned to the entity.</param>
        /// <param name="orientation">Local orientation assigned to the entity.</param>
        /// <returns>Configured editor entity.</returns>
        EditorEntity CreateUserEntity(string name, float3 position, float3 scale, float4 orientation) {
            EditorEntity entity = new EditorEntity {
                Name = name,
                LayerMask = EditorLayerMasks.SceneObjects,
                LocalPosition = position,
                LocalScale = scale,
                LocalOrientation = orientation
            };
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
        /// Finds the mesh component attached to one editor entity.
        /// </summary>
        /// <param name="entity">Entity whose mesh component should be returned.</param>
        /// <returns>Attached mesh component.</returns>
        MeshComponent FindMeshComponent(EditorEntity entity) {
            return Assert.IsType<MeshComponent>(Assert.Single(entity.Components, component => component is MeshComponent));
        }

        /// <summary>
        /// Creates a stable runtime font asset used by scene persistence tests.
        /// </summary>
        /// <param name="name">Friendly font name.</param>
        /// <returns>Runtime font asset with deterministic metrics.</returns>
        FontAsset CreateFont(string name) {
            return new FontAsset(
                new FontInfo(name, 16, 4f),
                new TestRuntimeTexture {
                    Width = 1,
                    Height = 1
                },
                new Dictionary<char, FontChar>(),
                16f,
                1,
                1);
        }
    }
}
