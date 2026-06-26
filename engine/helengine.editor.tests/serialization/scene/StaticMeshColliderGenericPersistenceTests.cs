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
                CookedRuntimeData = new StaticMeshCollisionRuntimeData3D(
                    "helengine.bepu.static-mesh",
                    [0x10, 0x20, 0x30, 0x40])
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(collider, 0, saveComponent.GetOrCreateComponentState(collider));
            StaticMeshCollider3DComponent restored = Assert.IsType<StaticMeshCollider3DComponent>(descriptor.DeserializeComponent(record, saveComponent, resolver));

            Assert.Equal(3, restored.CollisionData.Vertices.Length);
            Assert.Equal(new float3(-1f, 0f, -1f), restored.CollisionData.Vertices[0]);
            Assert.Equal(new[] { 0, 1, 2 }, restored.CollisionData.Indices);
            Assert.Equal("helengine.bepu.static-mesh", restored.CookedRuntimeData.FormatId);
            Assert.Equal(new byte[] { 0x10, 0x20, 0x30, 0x40 }, restored.CookedRuntimeData.Data);
        }
    }
}
