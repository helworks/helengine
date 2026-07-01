using helengine.editor;
using helengine.editor.tests.testing;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies static-mesh colliders persist through the generic reflected descriptor.
    /// </summary>
    public sealed class StaticMeshColliderGenericPersistenceTests {
        /// <summary>
        /// Ensures generic collision data and one cooked runtime payload both round-trip through the automatic reflected persistence path.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenStaticMeshColliderUsesCookedRuntimePayload_RoundTripsGenericPayload() {
            AutomaticScriptComponentPersistenceDescriptor descriptor = new AutomaticScriptComponentPersistenceDescriptor(new ScriptComponentReflectionSchemaBuilder());
            TestSceneAssetReferenceResolver resolver = new TestSceneAssetReferenceResolver();
            EntitySaveComponent saveComponent = new EntitySaveComponent();
            StaticMeshCollider3DComponent collider = new StaticMeshCollider3DComponent {
                CollisionData = new StaticMeshCollisionData3D(
                    [
                        new float3(-1f, 0f, -1f),
                        new float3(1f, 0f, -1f),
                        new float3(-1f, 0f, 1f)
                    ],
                    [0, 1, 2]),
                CookedRuntimeData = StaticMeshCollisionRuntimeData3D.Create(
                    "helengine.bepu.static-mesh",
                    0x7301,
                    2,
                    EngineBinaryEndianness.BigEndian,
                    writer => {
                        writer.WriteInt32(4);
                        writer.WriteFloat3(new float3(0.25f, 0.5f, 0.75f));
                    })
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(collider, 0, saveComponent.GetOrCreateComponentState(collider));
            StaticMeshCollider3DComponent restored = Assert.IsType<StaticMeshCollider3DComponent>(descriptor.DeserializeComponent(record, saveComponent, resolver));

            Assert.Equal(3, restored.CollisionData.Vertices.Length);
            Assert.Equal(new float3(-1f, 0f, -1f), restored.CollisionData.Vertices[0]);
            Assert.Equal(new[] { 0, 1, 2 }, restored.CollisionData.Indices);
            Assert.Equal("helengine.bepu.static-mesh", restored.CookedRuntimeData.FormatId);
            using EngineBinaryReader reader = restored.CookedRuntimeData.CreatePayloadReader("helengine.bepu.static-mesh", 0x7301, 2);
            Assert.Equal(EngineBinaryEndianness.BigEndian, reader.Endianness);
            Assert.Equal(4, reader.ReadInt32());
            Assert.Equal(new float3(0.25f, 0.5f, 0.75f), reader.ReadFloat3());
        }
    }
}
