using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies scene persistence for the 3D kinematic-motion component descriptor.
    /// </summary>
    public sealed class KinematicMotion3DComponentPersistenceDescriptorTests {
        /// <summary>
        /// Ensures kinematic-motion persistence round-trips the authored path and timing values.
        /// </summary>
        [Fact]
        public void SerializeAndDeserialize_WhenMotionUsesCustomValues_RoundTripsTheComponent() {
            KinematicMotion3DComponentPersistenceDescriptor descriptor = new KinematicMotion3DComponentPersistenceDescriptor();
            KinematicMotion3DComponent motionComponent = new KinematicMotion3DComponent {
                StartLocalPosition = new float3(-2f, 0.5f, 0f),
                EndLocalPosition = new float3(0.5f, 0.5f, 0f),
                TravelDurationSeconds = 1.25d,
                PingPong = false
            };

            SceneComponentAssetRecord record = descriptor.SerializeComponent(motionComponent, 2, null);
            KinematicMotion3DComponent loadedComponent = Assert.IsType<KinematicMotion3DComponent>(
                descriptor.DeserializeComponent(record, null, new TestSceneAssetReferenceResolver()));

            Assert.Equal(new float3(-2f, 0.5f, 0f), loadedComponent.StartLocalPosition);
            Assert.Equal(new float3(0.5f, 0.5f, 0f), loadedComponent.EndLocalPosition);
            Assert.Equal(1.25d, loadedComponent.TravelDurationSeconds, 3);
            Assert.False(loadedComponent.PingPong);
        }
    }
}
