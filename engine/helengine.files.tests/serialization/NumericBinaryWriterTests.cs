using helengine.files;
using Xunit;

namespace helengine.files.tests.serialization {
    /// <summary>Checks actual authoring writers against the independently implemented runtime scalar readers.</summary>
    public sealed class NumericBinaryWriterTests {
        /// <summary>Half vectors occupy two bytes per component and leave the next fixed-width field aligned in either byte order.</summary>
        [Theory]
        [InlineData(EngineBinaryEndianness.LittleEndian)]
        [InlineData(EngineBinaryEndianness.BigEndian)]
        public void HalfWriterMatchesRuntimeReader(EngineBinaryEndianness endianness) {
            using MemoryStream stream = new MemoryStream();
            NumericPrecisionPolicy policy = new NumericPrecisionPolicy(16, 32);
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, endianness);
            writer.NumericPolicy = policy;
            writer.WriteReal(1d);
            writer.WriteReal3(new float3(2d, 3d, 4d));
            writer.WriteInt32(0x12345678);
            Assert.Equal(12, stream.Length);
            stream.Position = 0;
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, endianness);
            reader.NumericPolicy = policy;
            Assert.Equal(1d, reader.ReadReal().ToDouble());
            float3 vector = reader.ReadReal3();
            Assert.Equal(2d, vector.X.ToDouble());
            Assert.Equal(4d, vector.Z.ToDouble());
            Assert.Equal(0x12345678, reader.ReadInt32());
            Assert.Equal(stream.Length, stream.Position);
        }

        /// <summary>Scene identity and the fields following its transforms survive an explicit half-policy cook.</summary>
        [Fact]
        public void HalfSceneCanBeConsumedByRuntime() {
            NumericPrecisionPolicy policy = new NumericPrecisionPolicy(16, 32);
            SceneAsset scene = new SceneAsset { Id = "half-scene", RootEntities = new[] {
                new SceneEntityAsset { Id = 7, Name = "half-root", LocalPosition = new float3(1.25d, 2.5d, -3.75d),
                    LocalScale = new float3(1d, 1d, 1d), LocalOrientation = new float4(0d, 0d, 0d, 1d) }
            } };
            byte[] bytes = helengine.files.AssetSerializer.SerializeToBytes(scene, policy);
            using MemoryStream stream = new MemoryStream(bytes, false);
            SceneAsset actual = Assert.IsType<SceneAsset>(PackagedAssetBinarySerializer.Deserialize(stream, policy));
            SceneEntityAsset entity = Assert.Single(actual.RootEntities);
            Assert.Equal("half-root", entity.Name);
            Assert.Equal(7u, entity.Id);
            Assert.Equal(-3.75d, entity.LocalPosition.Z.ToDouble());
            Assert.Empty(entity.Children);
        }
    }
}
