using System.IO;
using helengine.files;
using Xunit;

namespace helengine.files.tests.serialization {
    public class EngineBinaryWriterTests {
        [Fact]
        public void Create_LittleEndian_WritesExpectedBytes() {
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);

            writer.WriteUInt16(0x1234);
            writer.WriteInt32(0x55667788);

            Assert.Equal(new byte[] { 0x34, 0x12, 0x88, 0x77, 0x66, 0x55 }, stream.ToArray());
        }
    }
}
