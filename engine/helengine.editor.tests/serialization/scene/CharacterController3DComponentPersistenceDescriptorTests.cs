using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies scene persistence for the 3D character-controller component descriptor.
    /// </summary>
    public sealed class CharacterController3DComponentPersistenceDescriptorTests {
        /// <summary>
        /// Ensures character-controller persistence round-trips the authored locomotion values.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenControllerUsesCustomValues_RoundTripsTheComponent() {
            CharacterController3DComponentPersistenceDescriptor descriptor = new CharacterController3DComponentPersistenceDescriptor();
            CharacterController3DComponent controllerComponent = new CharacterController3DComponent {
                DesiredMoveDirection = new float3(1f, 0f, -0.25f),
                MoveSpeed = 3.5d,
                GravityScale = 0.75d,
                StepHeight = 0.6d,
                GroundSnapDistance = 0.2d
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(controllerComponent, 2, null);
            CharacterController3DComponent loadedComponent = Assert.IsType<CharacterController3DComponent>(
                descriptor.DeserializeComponent(record, null, new TestSceneAssetReferenceResolver()));

            Assert.Equal(new float3(1f, 0f, -0.25f), loadedComponent.DesiredMoveDirection);
            Assert.Equal(3.5d, loadedComponent.MoveSpeed, 3);
            Assert.Equal(0.75d, loadedComponent.GravityScale, 3);
            Assert.Equal(0.6d, loadedComponent.StepHeight, 3);
            Assert.Equal(0.2d, loadedComponent.GroundSnapDistance, 3);
        }
    }
}
