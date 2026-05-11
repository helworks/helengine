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

        /// <summary>
        /// Ensures mesh payloads that omit the current material-reference array are rejected.
        /// </summary>
        [Fact]
        public void DeserializeComponent_WhenPayloadOnlyHasLegacyMaterialReferenceField_ThrowsUnsupportedPayload() {
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("MaterialReference", fieldWriter => SceneComponentBinaryFieldEncoding.WriteOptionalReference(
                fieldWriter,
                new SceneAssetReference {
                    SourceKind = SceneAssetReferenceSourceKind.Generated,
                    RelativePath = "Engine/Materials/Standard",
                    ProviderId = "engine",
                    AssetId = "engine:material:standard"
                }));

            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.MeshComponent",
                ComponentIndex = 0,
                Payload = writer.BuildPayload()
            };

            MeshComponentPersistenceDescriptor descriptor = new MeshComponentPersistenceDescriptor();
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => descriptor.DeserializeComponent(record, null, new AnySceneAssetReferenceResolver()));
            Assert.Contains("MaterialReferences", exception.Message);
        }

        /// <summary>
        /// Ensures mesh persistence round-trips every material slot reference using the current material-reference array field.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenMeshUsesMultipleMaterialSlots_RoundTripsEverySlot() {
            MeshComponentPersistenceDescriptor descriptor = new MeshComponentPersistenceDescriptor();
            MeshComponent meshComponent = new MeshComponent {
                Model = new TestRuntimeModel(),
                RenderOrder3D = 19
            };
            TestRuntimeMaterial firstMaterial = new TestRuntimeMaterial();
            TestRuntimeMaterial secondMaterial = new TestRuntimeMaterial();
            meshComponent.SetMaterials(new RuntimeMaterial[] {
                firstMaterial,
                secondMaterial
            });

            EntitySaveComponent saveComponent = new EntitySaveComponent();
            SceneAssetReference modelReference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = "Engine/Models/Sponza",
                ProviderId = "engine",
                AssetId = "engine:model:sponza"
            };
            SceneAssetReference firstMaterialReference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = "Materials/SponzaWalls.helmat",
                ProviderId = string.Empty,
                AssetId = string.Empty
            };
            SceneAssetReference secondMaterialReference = new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.FileSystem,
                RelativePath = "Materials/SponzaTrim.helmat",
                ProviderId = string.Empty,
                AssetId = string.Empty
            };
            saveComponent.SetAssetReference(meshComponent, "Model", modelReference);
            saveComponent.SetAssetReference(meshComponent, "Material", firstMaterialReference);
            saveComponent.SetAssetReference(meshComponent, "Material[1]", secondMaterialReference);

            SceneComponentAssetRecord record = descriptor.SerializeComponent(
                meshComponent,
                0,
                saveComponent.GetOrCreateComponentState(meshComponent));

            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            TestRuntimeModel loadedModel = new TestRuntimeModel();
            TestRuntimeMaterial loadedFirstMaterial = new TestRuntimeMaterial();
            TestRuntimeMaterial loadedSecondMaterial = new TestRuntimeMaterial();
            resolver.RegisterModel(modelReference, loadedModel);
            resolver.RegisterMaterial(firstMaterialReference, loadedFirstMaterial);
            resolver.RegisterMaterial(secondMaterialReference, loadedSecondMaterial);

            EntitySaveComponent loadedSaveComponent = new EntitySaveComponent();
            MeshComponent loadedMesh = Assert.IsType<MeshComponent>(descriptor.DeserializeComponent(record, loadedSaveComponent, resolver));

            Assert.Same(loadedModel, loadedMesh.Model);
            Assert.Same(loadedFirstMaterial, loadedMesh.Material);
            Assert.Equal(2, loadedMesh.Materials.Length);
            Assert.Same(loadedFirstMaterial, loadedMesh.Materials[0]);
            Assert.Same(loadedSecondMaterial, loadedMesh.Materials[1]);
            Assert.Equal((byte)19, loadedMesh.RenderOrder3D);
            Assert.True(loadedSaveComponent.TryGetComponentState(loadedMesh, out EntityComponentSaveState loadedState));
            Assert.True(loadedState.TryGetAssetReference("Material", out SceneAssetReference loadedFirstMaterialReference));
            Assert.True(loadedState.TryGetAssetReference("Material[1]", out SceneAssetReference loadedSecondMaterialReference));
            Assert.Equal(firstMaterialReference.RelativePath, loadedFirstMaterialReference.RelativePath);
            Assert.Equal(secondMaterialReference.RelativePath, loadedSecondMaterialReference.RelativePath);
        }
    }
}
