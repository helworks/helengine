using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies scene persistence for the 3D box-collider component descriptor.
    /// </summary>
    public sealed class BoxCollider3DComponentPersistenceDescriptorTests {
        /// <summary>
        /// Ensures box-collider persistence round-trips the authored size and trigger metadata.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenBoxColliderUsesCustomSize_RoundTripsTheComponent() {
            BoxCollider3DComponentPersistenceDescriptor descriptor = new BoxCollider3DComponentPersistenceDescriptor();
            BoxCollider3DComponent boxColliderComponent = new BoxCollider3DComponent {
                Size = new float3(2.5f, 1.75f, 6f),
                CollisionLayer = 0b0000000000000100,
                CollisionMask = 0b0000000000001110,
                IsTrigger = true
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(boxColliderComponent, 1, null);
            BoxCollider3DComponent loadedComponent = Assert.IsType<BoxCollider3DComponent>(
                descriptor.DeserializeComponent(record, null, new TestSceneAssetReferenceResolver()));

            Assert.Equal(new float3(2.5f, 1.75f, 6f), loadedComponent.Size);
            Assert.Equal((ushort)0b0000000000000100, loadedComponent.CollisionLayer);
            Assert.Equal((ushort)0b0000000000001110, loadedComponent.CollisionMask);
            Assert.True(loadedComponent.IsTrigger);
        }
    }
}
