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
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
            EngineGeneratedModelCache.ResetForTests();
            EngineGeneratedMaterialCache.ResetForTests();
        }

        /// <summary>
        /// Deletes temporary project state after each test.
        /// </summary>
        public void Dispose() {
            EngineGeneratedModelCache.ResetForTests();
            EngineGeneratedMaterialCache.ResetForTests();
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
        /// Ensures primitive creation stores both generated model and generated standard material references required by scene saving.
        /// </summary>
        [Theory]
        [InlineData("Cube", EngineGeneratedModelCache.CubeAssetId, EngineGeneratedAssetProvider.CubeRelativePath)]
        [InlineData("Plane", EngineGeneratedModelCache.PlaneAssetId, EngineGeneratedAssetProvider.PlaneRelativePath)]
        public void CreatePrimitive_StoresGeneratedModelAndMaterialReferences(string expectedName, string modelAssetId, string modelRelativePath) {
            EditorSceneCreationService service = new EditorSceneCreationService();

            EditorEntity entity = expectedName == "Cube" ? service.CreateCube() : service.CreatePlane();

            MeshComponent meshComponent = Assert.IsType<MeshComponent>(Assert.Single(entity.Components, component => component is MeshComponent));
            EntitySaveComponent saveComponent = Assert.IsType<EntitySaveComponent>(Assert.Single(entity.Components, component => component is EntitySaveComponent));

            Assert.Equal(expectedName, entity.Name);
            Assert.NotNull(meshComponent.Model);
            Assert.NotNull(meshComponent.Material);
            Assert.True(saveComponent.TryGetComponentState(meshComponent, out EntityComponentSaveState saveState));
            Assert.True(saveState.TryGetAssetReference("Model", out SceneAssetReference modelReference));
            Assert.True(saveState.TryGetAssetReference("Material", out SceneAssetReference materialReference));
            Assert.Equal(SceneAssetReferenceSourceKind.Generated, modelReference.SourceKind);
            Assert.Equal(EngineGeneratedAssetProvider.ProviderIdValue, modelReference.ProviderId);
            Assert.Equal(modelRelativePath, modelReference.RelativePath);
            Assert.Equal(modelAssetId, modelReference.AssetId);
            Assert.Equal(SceneAssetReferenceSourceKind.Generated, materialReference.SourceKind);
            Assert.Equal(EngineGeneratedAssetProvider.ProviderIdValue, materialReference.ProviderId);
            Assert.Equal(EngineGeneratedAssetProvider.StandardMaterialRelativePath, materialReference.RelativePath);
            Assert.Equal(EngineGeneratedMaterialCache.StandardAssetId, materialReference.AssetId);
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
    }
}
