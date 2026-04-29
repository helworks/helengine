using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies scene persistence for the built-in mesh component descriptor.
    /// </summary>
    public class MeshComponentPersistenceDescriptorTests {
        /// <summary>
        /// Ensures mesh persistence round-trips generated model references, filesystem material references, and render order.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenMeshUsesGeneratedAndFileSystemReferences_RoundTripsTheComponent() {
            MeshComponentPersistenceDescriptor descriptor = new MeshComponentPersistenceDescriptor();
            MeshComponent meshComponent = new MeshComponent {
                Model = new TestRuntimeModel(),
                Material = new TestRuntimeMaterial(),
                RenderOrder3D = 7
            };
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            SceneAssetReference modelReference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "Engine/Models/Cube",
                ProviderId = "engine",
                AssetId = EngineGeneratedModelCache.CubeAssetId
            };
            SceneAssetReference materialReference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = "Materials/Default.helmat",
                ProviderId = string.Empty,
                AssetId = string.Empty
            };
            saveComponent.SetAssetReference(meshComponent, "Model", modelReference);
            saveComponent.SetAssetReference(meshComponent, "Material", materialReference);

            SceneComponentAssetRecord record = descriptor.SerializeComponent(
                meshComponent,
                0,
                saveComponent.GetOrCreateComponentState(meshComponent));

            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            TestRuntimeModel loadedModel = new TestRuntimeModel();
            TestRuntimeMaterial loadedMaterial = new TestRuntimeMaterial();
            resolver.RegisterModel(modelReference, loadedModel);
            resolver.RegisterMaterial(materialReference, loadedMaterial);

            EntitySaveComponent loadedSaveComponent = new EntitySaveComponent();
            MeshComponent loadedMesh = Assert.IsType<MeshComponent>(descriptor.DeserializeComponent(record, loadedSaveComponent, resolver));

            Assert.Same(loadedModel, loadedMesh.Model);
            Assert.Same(loadedMaterial, loadedMesh.Material);
            Assert.Equal((byte)7, loadedMesh.RenderOrder3D);
            Assert.True(loadedSaveComponent.TryGetComponentState(loadedMesh, out EntityComponentSaveState loadedState));
            Assert.True(loadedState.TryGetAssetReference("Model", out SceneAssetReference loadedModelReference));
            Assert.True(loadedState.TryGetAssetReference("Material", out SceneAssetReference loadedMaterialReference));
            Assert.Equal(modelReference.ProviderId, loadedModelReference.ProviderId);
            Assert.Equal(materialReference.RelativePath, loadedMaterialReference.RelativePath);
        }
    }
}
