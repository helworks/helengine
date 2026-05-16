using helengine.editor;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies scene persistence for the runtime rounded-rectangle component descriptor.
    /// </summary>
    public class RoundedRectComponentPersistenceDescriptorTests {
        /// <summary>
        /// Ensures rounded-rectangle deserialization accepts the strict runtime payload shape written by cooked scene packaging.
        /// </summary>
        [Fact]
        public void DeserializeComponent_WhenPayloadUsesCookedRuntimeLayout_LoadsTheComponent() {
            RoundedRectComponentPersistenceDescriptor descriptor = new RoundedRectComponentPersistenceDescriptor();
            SceneComponentAssetRecord record = new SceneComponentAssetRecord {
                ComponentTypeId = descriptor.ComponentTypeId,
                ComponentIndex = 0,
                Payload = WriteCookedRuntimePayload()
            };

            RoundedRectComponent loadedComponent = Assert.IsType<RoundedRectComponent>(descriptor.DeserializeComponent(record, null, null));

            Assert.Equal((byte)9, loadedComponent.RenderOrder2D);
            Assert.Equal((byte)5, loadedComponent.LayerMask);
            Assert.Equal(RoundedRectCorners.TopLeft | RoundedRectCorners.BottomRight, loadedComponent.Corners);
            Assert.Equal(0.6f, loadedComponent.Rotation);
            Assert.Equal(new byte4(5, 10, 15, 255), loadedComponent.Color);
            Assert.Equal(new float4(0.25f, 0.35f, 0.45f, 0.55f), loadedComponent.SourceRect);
            Assert.Equal(new int2(320, 140), loadedComponent.Size);
            Assert.Equal(18f, loadedComponent.Radius);
            Assert.Equal(4f, loadedComponent.BorderThickness);
            Assert.Equal(new byte4(30, 60, 90, 255), loadedComponent.FillColor);
            Assert.Equal(new byte4(120, 150, 180, 255), loadedComponent.BorderColor);
        }

        /// <summary>
        /// Writes one strict runtime rounded-rectangle payload matching the cooked scene layout used by packaged builds.
        /// </summary>
        /// <returns>Serialized cooked-runtime payload.</returns>
        byte[] WriteCookedRuntimePayload() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteByte(1);
            writer.WriteByte(9);
            writer.WriteByte(5);
            writer.WriteInt32((int)(RoundedRectCorners.TopLeft | RoundedRectCorners.BottomRight));
            writer.WriteSingle(0.6f);
            SceneComponentBinaryFieldEncoding.WriteByte4(writer, new byte4(5, 10, 15, 255));
            writer.WriteFloat4(new float4(0.25f, 0.35f, 0.45f, 0.55f));
            writer.WriteInt2(new int2(320, 140));
            writer.WriteSingle(18f);
            writer.WriteSingle(4f);
            SceneComponentBinaryFieldEncoding.WriteByte4(writer, new byte4(30, 60, 90, 255));
            SceneComponentBinaryFieldEncoding.WriteByte4(writer, new byte4(120, 150, 180, 255));
            return stream.ToArray();
        }
    }
}
