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

        /// <summary>
        /// Ensures older box-collider payload versions are rejected instead of being interpreted as the current scene format.
        /// </summary>
        [Fact]
        public void DeserializeComponent_WhenPayloadUsesOlderVersion_ThrowsUnsupportedPayloadVersion() {
            BoxCollider3DComponentPersistenceDescriptor descriptor = new BoxCollider3DComponentPersistenceDescriptor();
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = descriptor.ComponentTypeId,
                ComponentIndex = 0,
                Payload = WriteOlderVersionPayload()
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => descriptor.DeserializeComponent(record, null, new TestSceneAssetReferenceResolver()));

            Assert.Contains("Unsupported box collider component payload version", exception.Message);
        }

        /// <summary>
        /// Writes one older box-collider payload that predates the current collision metadata layout.
        /// </summary>
        /// <returns>Serialized older-version box-collider payload.</returns>
        static byte[] WriteOlderVersionPayload() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteFloat3(new float3(2.5f, 1.75f, 6f));
            return stream.ToArray();
        }
    }
}
