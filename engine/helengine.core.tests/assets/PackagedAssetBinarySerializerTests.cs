using helengine;

namespace helengine.core.tests.assets {
    /// <summary>
    /// Verifies that packaged runtime asset readers enforce the current binary format.
    /// </summary>
    public sealed class PackagedAssetBinarySerializerTests {
        /// <summary>
        /// Ensures a packaged asset with an older or newer header version is rejected before payload reads begin.
        /// </summary>
        /// <param name="version">Unsupported packaged asset header version.</param>
        [Theory]
        [InlineData(24)]
        [InlineData(26)]
        public void Deserialize_WhenHeaderVersionIsNotCurrent_ThrowsRegenerationGuidance(byte version) {
            using MemoryStream stream = CreateHeaderOnlyStream(version);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => PackagedAssetBinarySerializer.Deserialize(stream));

            Assert.Contains(version.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains(PackagedAssetBinarySerializer.CurrentVersion.ToString(), exception.Message, StringComparison.Ordinal);
            Assert.Contains("Regenerate", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies rejected and retained override orders consume their full payload before the next field.
        /// </summary>
        /// <param name="hasOrder">Whether the serialized entity owns the supplied order.</param>
        /// <param name="payloadLength">Length of the serialized order, or -1 for a null array.</param>
        /// <param name="endianness">Payload byte order.</param>
        [Theory]
        [InlineData(false, -1, EngineBinaryEndianness.LittleEndian)]
        [InlineData(true, -1, EngineBinaryEndianness.LittleEndian)]
        [InlineData(false, 0, EngineBinaryEndianness.LittleEndian)]
        [InlineData(false, 2, EngineBinaryEndianness.LittleEndian)]
        [InlineData(true, 2, EngineBinaryEndianness.LittleEndian)]
        [InlineData(false, -1, EngineBinaryEndianness.BigEndian)]
        [InlineData(true, -1, EngineBinaryEndianness.BigEndian)]
        [InlineData(false, 0, EngineBinaryEndianness.BigEndian)]
        [InlineData(false, 2, EngineBinaryEndianness.BigEndian)]
        [InlineData(true, 2, EngineBinaryEndianness.BigEndian)]
        public void ReadSceneEntity_OverrideOrderRespectsFlagAndReaderPosition(bool hasOrder, int payloadLength, EngineBinaryEndianness endianness) {
            using MemoryStream stream = new MemoryStream();
            SceneOverrideScopeStepKind[] order = payloadLength > 0
                ? new[] { SceneOverrideScopeStepKind.Platform, SceneOverrideScopeStepKind.BuildConfig }
                : Array.Empty<SceneOverrideScopeStepKind>();
            using (EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, endianness)) {
                writer.WriteByte(10);
                writer.WriteUInt32(42);
                writer.WriteString("ownership-test");
                writer.WriteByte(0);
                writer.WriteByte(1);
                writer.WriteByte(0);
                writer.WriteUInt16(7);
                writer.WriteFloat3(new float3(1, 2, 3));
                writer.WriteFloat3(new float3(1, 1, 1));
                writer.WriteFloat4(new float4(0, 0, 0, 1));
                writer.WriteByte(hasOrder ? (byte)1 : (byte)0);
                if (payloadLength < 0) {
                    writer.WriteInt32(-1);
                } else {
                    writer.WriteArray(order, static (target, value) => target.WriteByte((byte)value));
                }
                for (int index = 0; index < 5; index++) {
                    writer.WriteInt32(0);
                }
                writer.WriteByte(0x5A);
            }
            long expectedPosition = stream.Length - 1;
            stream.Position = 0;
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, endianness);
            System.Reflection.MethodInfo method = typeof(PackagedAssetBinarySerializer).GetMethod(
                "ReadSceneEntityAsset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            SceneEntityAsset entity = Assert.IsType<SceneEntityAsset>(method.Invoke(null, new object[] { reader }));

            Assert.Equal(hasOrder, entity.HasOverrideLevelOrder);
            Assert.Equal(hasOrder ? order : Array.Empty<SceneOverrideScopeStepKind>(), entity.OverrideLevelOrder);
            Assert.Equal((uint)42, entity.Id);
            Assert.Equal("ownership-test", entity.Name);
            Assert.Empty(entity.Components);
            Assert.Empty(entity.Children);
            Assert.Equal(expectedPosition, reader.GetStreamPosition());
            Assert.Equal((byte)0x5A, reader.ReadByte());
        }

        /// <summary>
        /// Creates a stream containing a valid packaged scene header and no payload bytes.
        /// </summary>
        /// <param name="version">Version encoded in the header.</param>
        /// <returns>Stream positioned at the beginning of the header.</returns>
        static MemoryStream CreateHeaderOnlyStream(byte version) {
            MemoryStream stream = new MemoryStream();
            EngineBinaryHeader header = new EngineBinaryHeader(
                EngineBinaryEndianness.LittleEndian,
                version,
                PackagedAssetBinarySerializer.FormatId,
                (ushort)PackagedAssetBinarySerializer.RecordKind,
                (ushort)EditorAssetBinaryValueKind.SceneAsset);
            EngineBinaryHeaderSerializer.Write(stream, header);
            stream.Position = 0;
            return stream;
        }
    }
}
