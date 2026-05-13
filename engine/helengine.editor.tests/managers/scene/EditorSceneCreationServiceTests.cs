using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.scene {
    /// <summary>
    /// Verifies scene entities created through the editor add flow.
    /// </summary>
    public class EditorSceneCreationServiceTests : IDisposable {
        /// <summary>
        /// Temporary project root used by scene-creation tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes the core services required for generated primitive creation.
        /// </summary>
        public EditorSceneCreationServiceTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-creation-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scenes"));

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
            EngineGeneratedModelCache.ResetForTests();
            EngineGeneratedMaterialCache.ResetForTests();
            EditorCameraVisualResources.ResetForTests();
            EditorPointLightVisualResources.ResetForTests();
            EditorDirectionalLightVisualResources.ResetForTests();
            EditorSpotLightVisualResources.ResetForTests();
        }

        /// <summary>
        /// Deletes temporary project state after each test.
        /// </summary>
        public void Dispose() {
            EngineGeneratedModelCache.ResetForTests();
            EngineGeneratedMaterialCache.ResetForTests();
            EditorCameraVisualResources.ResetForTests();
            EditorPointLightVisualResources.ResetForTests();
            EditorDirectionalLightVisualResources.ResetForTests();
            EditorSpotLightVisualResources.ResetForTests();
            if (Directory.Exists(TempProjectRootPath)) {
                Directory.Delete(TempProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures Add > Empty creates a root scene entity at the origin.
        /// </summary>
        [Fact]
        public void CreateEmpty_CreatesRootSceneEntityAtOrigin() {
            EditorSceneCreationService service = new EditorSceneCreationService();

            EditorEntity entity = service.CreateEmpty();

            Assert.Equal("Empty", entity.Name);
            Assert.Equal(EditorLayerMasks.SceneObjects, entity.LayerMask);
            Assert.Null(entity.Parent);
            Assert.Equal(float3.Zero, entity.LocalPosition);
            Assert.Equal(float3.One, entity.LocalScale);
            Assert.Equal(float4.Identity, entity.LocalOrientation);
        }

        /// <summary>
        /// Ensures primitive creation assigns generated runtime assets without pre-seeding hidden mesh reference metadata.
        /// </summary>
        [Theory]
        [InlineData("Cube", EngineGeneratedModelCache.CubeAssetId)]
        [InlineData("Plane", EngineGeneratedModelCache.PlaneAssetId)]
        public void CreatePrimitive_AssignsGeneratedRuntimeAssetsWithoutStoredReferences(string expectedName, string modelAssetId) {
            EditorSceneCreationService service = new EditorSceneCreationService();

            EditorEntity entity = expectedName == "Cube" ? service.CreateCube() : service.CreatePlane();

            MeshComponent meshComponent = Assert.IsType<MeshComponent>(Assert.Single(entity.Components, component => component is MeshComponent));
            EntitySaveComponent saveComponent = Assert.IsType<EntitySaveComponent>(Assert.Single(entity.Components, component => component is EntitySaveComponent));

            Assert.Equal(expectedName, entity.Name);
            Assert.NotNull(meshComponent.Model);
            Assert.NotNull(meshComponent.Material);
            Assert.Same(EngineGeneratedModelCache.GetRuntimeModel(modelAssetId), meshComponent.Model);
            Assert.Same(EngineGeneratedMaterialCache.GetRuntimeMaterial(EngineGeneratedMaterialCache.StandardAssetId), meshComponent.Material);
            Assert.False(saveComponent.TryGetComponentState(meshComponent, out _));
        }

        /// <summary>
        /// Ensures model creation stores the supplied model and ordered material references on the save component.
        /// </summary>
        [Fact]
        public void CreateModel_StoresModelAndMaterialReferences() {
            EditorSceneCreationService service = new EditorSceneCreationService();
            RuntimeModel runtimeModel = EngineGeneratedModelCache.GetRuntimeModel(EngineGeneratedModelCache.CubeAssetId);
            RuntimeMaterial runtimeMaterial = EngineGeneratedMaterialCache.GetRuntimeMaterial(EngineGeneratedMaterialCache.StandardAssetId);
            SceneAssetReference modelReference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = "Models/Cube.obj"
            };
            SceneAssetReference[] materialReferences = {
                new SceneAssetReference {
                    SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                    RelativePath = "Models/Cube.hasset"
                }
            };

            EditorEntity entity = service.CreateModel("Cube", runtimeModel, new[] { runtimeMaterial }, modelReference, materialReferences);

            MeshComponent meshComponent = Assert.IsType<MeshComponent>(Assert.Single(entity.Components, component => component is MeshComponent));
            EntitySaveComponent saveComponent = Assert.IsType<EntitySaveComponent>(Assert.Single(entity.Components, component => component is EntitySaveComponent));
            Assert.True(saveComponent.TryGetComponentState(meshComponent, out EntityComponentSaveState saveState));
            Assert.True(saveState.TryGetAssetReference("Model", out SceneAssetReference savedModelReference));
            Assert.True(saveState.TryGetAssetReference("Material", out SceneAssetReference savedMaterialReference));

            Assert.Equal("Cube", entity.Name);
            Assert.Same(runtimeModel, meshComponent.Model);
            Assert.Single(meshComponent.Materials);
            Assert.Same(runtimeMaterial, meshComponent.Material);
            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, savedModelReference.SourceKind);
            Assert.Equal("Models/Cube.obj", savedModelReference.RelativePath);
            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, savedMaterialReference.SourceKind);
            Assert.Equal("Models/Cube.hasset", savedMaterialReference.RelativePath);
        }

        /// <summary>
        /// Ensures Add > Camera creates a camera-backed scene entity with the hidden editor visual attached.
        /// </summary>
        [Fact]
        public void CreateCamera_CreatesSceneCameraWithHiddenEditorVisual() {
            EditorSceneCreationService service = new EditorSceneCreationService();

            EditorEntity entity = service.CreateCamera();

            CameraComponent cameraComponent = Assert.IsType<CameraComponent>(Assert.Single(entity.Components, component => component is CameraComponent));
            EditorSceneCameraSuppressionComponent suppressionComponent = Assert.IsType<EditorSceneCameraSuppressionComponent>(Assert.Single(entity.Components, component => component is EditorSceneCameraSuppressionComponent));
            EditorEntity visualEntity = Assert.IsType<EditorEntity>(Assert.Single(entity.Children));
            EditorCameraVisualComponent visualComponent = Assert.IsType<EditorCameraVisualComponent>(Assert.Single(visualEntity.Components, component => component is EditorCameraVisualComponent));

            Assert.Equal("Camera", entity.Name);
            Assert.Equal(EditorLayerMasks.SceneObjects, entity.LayerMask);
            Assert.Equal((ushort)0, cameraComponent.LayerMask);
            Assert.Equal(float4.Identity, entity.LocalOrientation);
            Assert.False(cameraComponent.ClearSettings.ClearColorEnabled);
            Assert.False(cameraComponent.ClearSettings.ClearDepthEnabled);
            Assert.Equal(EditorLayerMasks.SceneObjects, suppressionComponent.LayerMask);
            Assert.True(suppressionComponent.ClearSettings.ClearColorEnabled);
            Assert.True(suppressionComponent.ClearSettings.ClearDepthEnabled);
            Assert.True(visualEntity.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneCameraVisuals, visualEntity.LayerMask);
            Assert.NotNull(visualComponent.Model);
            Assert.NotNull(visualComponent.Material);
        }

        /// <summary>
        /// Ensures Add > Light > Spot Light creates a root spot-light entity with the authored defaults.
        /// </summary>
        [Fact]
        public void CreateSpotLight_CreatesRootSpotLightEntityWithDefaultSettings() {
            EditorSceneCreationService service = new EditorSceneCreationService();

            EditorEntity entity = service.CreateSpotLight();

            SpotLightComponent lightComponent = Assert.IsType<SpotLightComponent>(Assert.Single(entity.Components, component => component is SpotLightComponent));

            Assert.Equal("Spot Light", entity.Name);
            Assert.Equal(EditorLayerMasks.SceneObjects, entity.LayerMask);
            Assert.Null(entity.Parent);
            Assert.Equal(float3.Zero, entity.LocalPosition);
            Assert.Equal(float3.One, entity.LocalScale);
            Assert.Equal(float4.Identity, entity.LocalOrientation);
            Assert.Equal(10f, lightComponent.Range);
            Assert.Equal(25f, lightComponent.InnerConeAngleDegrees);
            Assert.Equal(45f, lightComponent.OuterConeAngleDegrees);
        }

        /// <summary>
        /// Ensures Add > Light > Point Light creates a root point-light entity with the authored defaults.
        /// </summary>
        [Fact]
        public void CreatePointLight_CreatesRootPointLightEntityWithDefaultSettings() {
            EditorSceneCreationService service = new EditorSceneCreationService();

            EditorEntity entity = service.CreatePointLight();

            PointLightComponent lightComponent = Assert.IsType<PointLightComponent>(Assert.Single(entity.Components, component => component is PointLightComponent));

            Assert.Equal("Point Light", entity.Name);
            Assert.Equal(EditorLayerMasks.SceneObjects, entity.LayerMask);
            Assert.Null(entity.Parent);
            Assert.Equal(float3.Zero, entity.LocalPosition);
            Assert.Equal(float3.One, entity.LocalScale);
            Assert.Equal(float4.Identity, entity.LocalOrientation);
            Assert.Equal(10f, lightComponent.Range);
        }

        /// <summary>
        /// Ensures Add > Light > Directional Light creates a root directional-light entity with the authored defaults.
        /// </summary>
        [Fact]
        public void CreateDirectionalLight_CreatesRootDirectionalLightEntityWithDefaultSettings() {
            EditorSceneCreationService service = new EditorSceneCreationService();

            EditorEntity entity = service.CreateDirectionalLight();

            DirectionalLightComponent lightComponent = Assert.IsType<DirectionalLightComponent>(Assert.Single(entity.Components, component => component is DirectionalLightComponent));

            Assert.Equal("Directional Light", entity.Name);
            Assert.Equal(EditorLayerMasks.SceneObjects, entity.LayerMask);
            Assert.Null(entity.Parent);
            Assert.Equal(float3.Zero, entity.LocalPosition);
            Assert.Equal(float3.One, entity.LocalScale);
            Assert.Equal(float4.Identity, entity.LocalOrientation);
            Assert.True(lightComponent.ShadowsEnabled);
        }

        /// <summary>
        /// Ensures Add > Light > Ambient Light creates a root ambient-light entity with the authored defaults.
        /// </summary>
        [Fact]
        public void CreateAmbientLight_CreatesRootAmbientLightEntityWithDefaultSettings() {
            EditorSceneCreationService service = new EditorSceneCreationService();

            EditorEntity entity = service.CreateAmbientLight();

            AmbientLightComponent lightComponent = Assert.IsType<AmbientLightComponent>(Assert.Single(entity.Components, component => component is AmbientLightComponent));

            Assert.Equal("Ambient Light", entity.Name);
            Assert.Equal(EditorLayerMasks.SceneObjects, entity.LayerMask);
            Assert.Null(entity.Parent);
            Assert.Equal(float3.Zero, entity.LocalPosition);
            Assert.Equal(float3.One, entity.LocalScale);
            Assert.Equal(float4.Identity, entity.LocalOrientation);
            Assert.Equal(LightType.Ambient, lightComponent.LightType);
            Assert.False(lightComponent.ShadowsEnabled);
        }

        /// <summary>
        /// Ensures Add > Point Light creates a point-light-backed scene entity with the hidden editor visual attached.
        /// </summary>
        [Fact]
        public void CreatePointLight_CreatesScenePointLightWithHiddenEditorVisual() {
            EditorSceneCreationService service = new EditorSceneCreationService();

            EditorEntity entity = service.CreatePointLight();

            PointLightComponent pointLightComponent = Assert.IsType<PointLightComponent>(Assert.Single(entity.Components, component => component is PointLightComponent));
            EditorEntity visualEntity = Assert.IsType<EditorEntity>(Assert.Single(entity.Children));
            EditorPointLightVisualComponent visualComponent = Assert.IsType<EditorPointLightVisualComponent>(Assert.Single(visualEntity.Components, component => component is EditorPointLightVisualComponent));

            Assert.Equal("Point Light", entity.Name);
            Assert.Equal(EditorLayerMasks.SceneObjects, entity.LayerMask);
            Assert.Equal(10f, pointLightComponent.Range);
            Assert.Equal(LightType.Point, pointLightComponent.LightType);
            Assert.True(visualEntity.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneCameraVisuals, visualEntity.LayerMask);
            Assert.NotNull(visualComponent.Model);
            Assert.NotNull(visualComponent.Material);
        }

        /// <summary>
        /// Ensures Add > Directional Light creates a directional-light-backed scene entity with the hidden editor arrow attached.
        /// </summary>
        [Fact]
        public void CreateDirectionalLight_CreatesSceneDirectionalLightWithHiddenEditorVisual() {
            EditorSceneCreationService service = new EditorSceneCreationService();

            EditorEntity entity = service.CreateDirectionalLight();

            DirectionalLightComponent directionalLightComponent = Assert.IsType<DirectionalLightComponent>(Assert.Single(entity.Components, component => component is DirectionalLightComponent));
            EditorEntity visualEntity = Assert.IsType<EditorEntity>(Assert.Single(entity.Children));
            EditorDirectionalLightVisualComponent visualComponent = Assert.IsType<EditorDirectionalLightVisualComponent>(Assert.Single(visualEntity.Components, component => component is EditorDirectionalLightVisualComponent));

            Assert.Equal("Directional Light", entity.Name);
            Assert.Equal(EditorLayerMasks.SceneObjects, entity.LayerMask);
            Assert.True(directionalLightComponent.ShadowsEnabled);
            Assert.Equal(LightType.Directional, directionalLightComponent.LightType);
            Assert.True(visualEntity.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneCameraVisuals, visualEntity.LayerMask);
            Assert.NotNull(visualComponent.Model);
            Assert.NotNull(visualComponent.Material);
        }

        /// <summary>
        /// Ensures the directional-light visual arrow extends along the engine forward direction on the local negative Z axis.
        /// </summary>
        [Fact]
        public void CreateDirectionalLightVisual_WhenBuilt_PointsTowardNegativeZ() {
            MethodInfo createModelAssetMethod = typeof(EditorDirectionalLightVisualResources).GetMethod("CreateModelAsset", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(createModelAssetMethod);

            ModelAsset modelAsset = Assert.IsType<ModelAsset>(createModelAssetMethod.Invoke(null, null));
            Assert.NotNull(modelAsset.Positions);

            float minZ = float.MaxValue;
            float maxZ = float.MinValue;
            for (int index = 0; index < modelAsset.Positions.Length; index++) {
                float z = modelAsset.Positions[index].Z;
                minZ = Math.Min(minZ, z);
                maxZ = Math.Max(maxZ, z);
            }

            Assert.True(minZ < -0.1f);
            Assert.InRange(Math.Abs(maxZ - 0f), 0f, 0.0001f);
        }

        /// <summary>
        /// Ensures Add > Spot Light creates a spot-light-backed scene entity with the hidden editor cone attached.
        /// </summary>
        [Fact]
        public void CreateSpotLight_CreatesSceneSpotLightWithHiddenEditorVisual() {
            EditorSceneCreationService service = new EditorSceneCreationService();

            EditorEntity entity = service.CreateSpotLight();

            SpotLightComponent spotLightComponent = Assert.IsType<SpotLightComponent>(Assert.Single(entity.Components, component => component is SpotLightComponent));
            EditorEntity visualEntity = Assert.IsType<EditorEntity>(Assert.Single(entity.Children));
            EditorSpotLightVisualComponent visualComponent = Assert.IsType<EditorSpotLightVisualComponent>(Assert.Single(visualEntity.Components, component => component is EditorSpotLightVisualComponent));

            Assert.Equal("Spot Light", entity.Name);
            Assert.Equal(EditorLayerMasks.SceneObjects, entity.LayerMask);
            Assert.Equal(10f, spotLightComponent.Range);
            Assert.Equal(25f, spotLightComponent.InnerConeAngleDegrees);
            Assert.Equal(45f, spotLightComponent.OuterConeAngleDegrees);
            Assert.Equal(LightType.Spot, spotLightComponent.LightType);
            Assert.True(visualEntity.InternalEntity);
            Assert.Equal(EditorLayerMasks.SceneCameraVisuals, visualEntity.LayerMask);
            Assert.NotNull(visualComponent.Model);
            Assert.NotNull(visualComponent.Material);
        }

        /// <summary>
        /// Ensures the spot-light visual cone points along the engine forward direction with its tip at the light origin.
        /// </summary>
        [Fact]
        public void CreateSpotLightVisual_WhenBuilt_HasTipAtOriginAndPointsTowardNegativeZ() {
            MethodInfo createModelAssetMethod = typeof(EditorSpotLightVisualResources).GetMethod("CreateModelAsset", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(createModelAssetMethod);

            ModelAsset modelAsset = Assert.IsType<ModelAsset>(createModelAssetMethod.Invoke(null, null));
            Assert.NotNull(modelAsset.Positions);

            float minZ = float.MaxValue;
            float maxZ = float.MinValue;
            float tipRadius = float.MaxValue;
            float baseRadius = float.MinValue;
            for (int index = 0; index < modelAsset.Positions.Length; index++) {
                float3 position = modelAsset.Positions[index];
                float z = position.Z;
                float horizontalRadius = (float)Math.Sqrt((position.X * position.X) + (position.Y * position.Y));
                minZ = Math.Min(minZ, z);
                maxZ = Math.Max(maxZ, z);

                if (Math.Abs(z - maxZ) < 0.0001f) {
                    tipRadius = Math.Min(tipRadius, horizontalRadius);
                }

                if (Math.Abs(z - minZ) < 0.0001f) {
                    baseRadius = Math.Max(baseRadius, horizontalRadius);
                }
            }

            Assert.True(minZ < -0.1f);
            Assert.InRange(Math.Abs(maxZ - 0f), 0f, 0.0001f);
            Assert.InRange(tipRadius, 0f, 0.0001f);
            Assert.True(baseRadius > 0.1f);
        }

        /// <summary>
        /// Ensures the top cylinders in the editor camera icon touch at the center seam on the local Z axis.
        /// </summary>
        [Fact]
        public void CreateCameraVisual_WhenBuilt_PlacesTheTopCylinderSeamAtZeroOnTheZAxis() {
            MethodInfo createModelAssetMethod = typeof(EditorCameraVisualResources).GetMethod("CreateModelAsset", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(createModelAssetMethod);
            FieldInfo bodyHeightField = typeof(EditorCameraVisualResources).GetField("BodyHeight", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(bodyHeightField);

            ModelAsset modelAsset = Assert.IsType<ModelAsset>(createModelAssetMethod.Invoke(null, null));
            Assert.NotNull(modelAsset.Positions);

            float bodyHeight = Convert.ToSingle(bodyHeightField.GetRawConstantValue());
            float topSeamThresholdY = (bodyHeight * 0.5f) + 0.001f;
            float leftCylinderMaxZ = float.MinValue;
            float rightCylinderMinZ = float.MaxValue;
            bool foundLeftCylinderVertex = false;
            bool foundRightCylinderVertex = false;

            for (int index = 0; index < modelAsset.Positions.Length; index++) {
                float3 position = modelAsset.Positions[index];
                if (position.Y <= topSeamThresholdY) {
                    continue;
                }

                if (position.Z <= 0f) {
                    foundLeftCylinderVertex = true;
                    if (position.Z > leftCylinderMaxZ) {
                        leftCylinderMaxZ = position.Z;
                    }
                }

                if (position.Z >= 0f) {
                    foundRightCylinderVertex = true;
                    if (position.Z < rightCylinderMinZ) {
                        rightCylinderMinZ = position.Z;
                    }
                }
            }

            Assert.True(foundLeftCylinderVertex);
            Assert.True(foundRightCylinderVertex);
            Assert.True(Math.Abs(leftCylinderMaxZ) < 0.0001f);
            Assert.True(Math.Abs(rightCylinderMinZ) < 0.0001f);
        }

        /// <summary>
        /// Ensures a created primitive can be saved immediately through the existing scene-save flow.
        /// </summary>
        [Fact]
        public void CreateCube_WhenSaved_WritesHelenFileWithoutAdditionalPickerMetadata() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            registry.Register(new MeshComponentPersistenceDescriptor());
            EditorSceneCreationService service = new EditorSceneCreationService();
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "CreatedFromAdd.helen");

            service.CreateCube();
            saveService.Save(scenePath);

            Assert.True(File.Exists(scenePath));
        }

        /// <summary>
        /// Ensures a created camera can be saved immediately without persisting the hidden editor visual component.
        /// </summary>
        [Fact]
        public void CreateCamera_WhenSaved_WritesHelenFileWithoutPersistingHiddenEditorVisual() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            registry.Register(new CameraComponentPersistenceDescriptor());
            EditorSceneCreationService service = new EditorSceneCreationService();
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "CreatedCamera.helen");

            service.CreateCamera();
            saveService.Save(scenePath);

            SceneAsset asset;
            using (FileStream stream = File.OpenRead(scenePath)) {
                asset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
            }

            Assert.True(File.Exists(scenePath));
            Assert.Single(asset.RootEntities);
            Assert.Single(asset.RootEntities[0].Components);
            Assert.Equal("helengine.CameraComponent", asset.RootEntities[0].Components[0].ComponentTypeId);

            CameraComponentPersistenceDescriptor descriptor = new CameraComponentPersistenceDescriptor();
            CameraComponent deserializedCamera = Assert.IsType<CameraComponent>(descriptor.DeserializeComponent(asset.RootEntities[0].Components[0], null, new TestSceneAssetReferenceResolver()));
            Assert.Equal(EditorLayerMasks.SceneObjects, deserializedCamera.LayerMask);
            Assert.True(deserializedCamera.ClearSettings.ClearColorEnabled);
        }
    }
}
