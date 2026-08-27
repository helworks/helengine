using System.Reflection;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies mesh components persist through the generic reflected descriptor.
    /// </summary>
    public sealed class MeshComponentGenericPersistenceTests {
        /// <summary>
        /// Ensures mesh material assignments clone the incoming array so reflected persistence does not depend on caller-owned buffers.
        /// </summary>
        [Fact]
        public void Materials_WhenAssigned_ClonesTheIncomingArray() {
            MeshComponent meshComponent = new MeshComponent();
            RuntimeMaterial[] first = new RuntimeMaterial[] {
                new TestRuntimeMaterial()
            };
            RuntimeMaterial[] second = new RuntimeMaterial[] {
                new TestRuntimeMaterial(),
                new TestRuntimeMaterial()
            };

            SetMeshMaterials(meshComponent, first);
            SetMeshMaterials(meshComponent, second);

            RuntimeMaterial[] assignedMaterials = GetMeshMaterials(meshComponent);
            Assert.Equal(2, assignedMaterials.Length);
            Assert.NotSame(second, assignedMaterials);
            Assert.Same(second[0], assignedMaterials[0]);
            Assert.Same(second[1], assignedMaterials[1]);
        }

        /// <summary>
        /// Ensures mesh components round-trip model and material-slot references through the automatic reflected persistence path.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenMeshUsesModelAndMaterialSlots_RoundTripsGenericPayload() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            MeshComponent meshComponent = new MeshComponent {
                Model = new TestRuntimeModel(),
                RenderOrder3D = 7
            };
            TestRuntimeMaterial firstMaterial = new TestRuntimeMaterial();
            TestRuntimeMaterial secondMaterial = new TestRuntimeMaterial();
            SetMeshMaterials(meshComponent, new RuntimeMaterial[] {
                firstMaterial,
                secondMaterial
            });

            SceneAssetReference modelReference = global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateSerialized(
                SceneAssetReferenceSourceKind.Generated,
                "Engine/Models/Cube",
                "engine",
                "engine:model:cube");
            SceneAssetReference firstMaterialReference = global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateSerialized(
                SceneAssetReferenceSourceKind.Generated,
                "Engine/Materials/Standard",
                "engine",
                "engine:material:standard");
            SceneAssetReference secondMaterialReference = global::helengine.editor.tests.SceneAssetReferenceTestFactory.CreateFileSystemMaterial(
                "Materials/Accent.helmat");
            TestRuntimeModel restoredModel = new TestRuntimeModel();
            TestRuntimeMaterial restoredFirstMaterial = new TestRuntimeMaterial();
            TestRuntimeMaterial restoredSecondMaterial = new TestRuntimeMaterial();
            resolver.RegisterModel(modelReference, restoredModel);
            resolver.RegisterMaterial(firstMaterialReference, restoredFirstMaterial);
            resolver.RegisterMaterial(secondMaterialReference, restoredSecondMaterial);

            saveComponent.SetAssetReference(meshComponent, "Model", modelReference);
            saveComponent.SetAssetReference(meshComponent, "Materials[0]", firstMaterialReference);
            saveComponent.SetAssetReference(meshComponent, "Materials[1]", secondMaterialReference);

            SceneComponentAssetRecord record = descriptor.SerializeComponent(meshComponent, 0, saveComponent.GetOrCreateComponentState(meshComponent));
            MeshComponent restored = Assert.IsType<MeshComponent>(descriptor.DeserializeComponent(record, saveComponent, resolver));
            RuntimeMaterial[] restoredMaterials = GetMeshMaterials(restored);

            Assert.Same(restoredModel, restored.Model);
            Assert.Equal(2, restoredMaterials.Length);
            Assert.Same(restoredFirstMaterial, restoredMaterials[0]);
            Assert.Same(restoredSecondMaterial, restoredMaterials[1]);
            Assert.Equal((byte)7, restored.RenderOrder3D);
            Assert.True(saveComponent.TryGetComponentState(restored, out EntityComponentSaveState restoredSaveState));
            Assert.True(restoredSaveState.TryGetAssetReference("Materials[0]", out SceneAssetReference restoredReference));
            Assert.Equal("Engine/Materials/Standard", restoredReference.RelativePath);
        }

        /// <summary>
        /// Ensures mesh payloads that use removed legacy field names fail instead of restoring an alias.
        /// </summary>
        [Fact]
        public void Deserialize_WhenMeshUsesLegacyMaterialReferencesField_ThrowsUnsupportedField() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            EditorTaggedSceneComponentFieldWriter writer = new EditorTaggedSceneComponentFieldWriter();
            writer.WriteField("ModelReference", fieldWriter => fieldWriter.WriteByte(1));
            writer.WriteField("MaterialReferences", fieldWriter => fieldWriter.WriteByte(1));
            writer.WriteField("RenderOrder3D", fieldWriter => fieldWriter.WriteByte(4));

            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = "helengine.MeshComponent",
                ComponentIndex = 0,
                Payload = writer.BuildPayload()
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => descriptor.DeserializeComponent(record, null, null));

            Assert.Contains("unsupported field", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Assigns runtime materials through the public writable mesh property expected by generic reflected persistence.
        /// </summary>
        /// <param name="meshComponent">Mesh component receiving the runtime material array.</param>
        /// <param name="materials">Runtime materials to assign.</param>
        static void SetMeshMaterials(MeshComponent meshComponent, RuntimeMaterial[] materials) {
            if (meshComponent == null) {
                throw new ArgumentNullException(nameof(meshComponent));
            }

            PropertyInfo materialsProperty = typeof(MeshComponent).GetProperty(nameof(MeshComponent.Materials)) ?? throw new InvalidOperationException("MeshComponent must expose a public Materials property.");
            Assert.True(materialsProperty.CanWrite, "MeshComponent.Materials must be writable for generic reflected persistence.");
            materialsProperty.SetValue(meshComponent, materials);
        }

        /// <summary>
        /// Reads runtime materials through the public mesh property expected by generic reflected persistence.
        /// </summary>
        /// <param name="meshComponent">Mesh component whose runtime materials should be read.</param>
        /// <returns>Runtime materials currently assigned to the mesh component.</returns>
        static RuntimeMaterial[] GetMeshMaterials(MeshComponent meshComponent) {
            if (meshComponent == null) {
                throw new ArgumentNullException(nameof(meshComponent));
            }

            PropertyInfo materialsProperty = typeof(MeshComponent).GetProperty(nameof(MeshComponent.Materials)) ?? throw new InvalidOperationException("MeshComponent must expose a public Materials property.");
            return Assert.IsType<RuntimeMaterial[]>(materialsProperty.GetValue(meshComponent));
        }
    }
}
