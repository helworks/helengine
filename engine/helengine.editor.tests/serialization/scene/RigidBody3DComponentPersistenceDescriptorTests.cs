using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies scene persistence for the 3D rigid-body component descriptor.
    /// </summary>
    public sealed class RigidBody3DComponentPersistenceDescriptorTests {
        /// <summary>
        /// Ensures rigid-body persistence round-trips body kind, gravity flags, mass, gravity scale, and linear velocity.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenRigidBodyUsesCustomValues_RoundTripsTheComponent() {
            RigidBody3DComponentPersistenceDescriptor descriptor = new RigidBody3DComponentPersistenceDescriptor();
            RigidBody3DComponent rigidBodyComponent = new RigidBody3DComponent {
                BodyKind = BodyKind3D.Kinematic,
                UseGravity = false,
                Mass = 4.5d,
                GravityScale = 0.25d,
                LinearVelocity = new float3(1.25f, -3.5f, 9f)
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(rigidBodyComponent, 2, null);
            RigidBody3DComponent loadedComponent = Assert.IsType<RigidBody3DComponent>(
                descriptor.DeserializeComponent(record, null, new TestSceneAssetReferenceResolver()));

            Assert.Equal(BodyKind3D.Kinematic, loadedComponent.BodyKind);
            Assert.False(loadedComponent.UseGravity);
            Assert.Equal(4.5d, loadedComponent.Mass, 3);
            Assert.Equal(0.25d, loadedComponent.GravityScale, 3);
            Assert.Equal(new float3(1.25f, -3.5f, 9f), loadedComponent.LinearVelocity);
        }
    }
}
