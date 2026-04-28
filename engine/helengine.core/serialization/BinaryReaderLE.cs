using System.Buffers.Binary;

namespace helengine {
    /// <summary>
    /// Reads little-endian binary payloads from a stream.
    /// </summary>
    public class BinaryReaderLE : EngineBinaryReader {
        /// <summary>
        /// Initializes a new little-endian reader over the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing the payload bytes.</param>
        /// <param name="leaveOpen">True to leave the stream open when the reader is disposed.</param>
        public BinaryReaderLE(Stream stream, bool leaveOpen = true)
            : base(stream, leaveOpen) {
        }

        /// <summary>
        /// Gets the endianness handled by this reader.
        /// </summary>
        public override EngineBinaryEndianness Endianness => EngineBinaryEndianness.LittleEndian;

        /// <summary>
        /// Reads a 16-bit unsigned integer in little-endian order.
        /// </summary>
        /// <returns>Decoded unsigned integer.</returns>
        public override ushort ReadUInt16() {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            ReadRequiredBytes(buffer);
            return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
        }

        /// <summary>
        /// Reads a 32-bit signed integer in little-endian order.
        /// </summary>
        /// <returns>Decoded signed integer.</returns>
        public override int ReadInt32() {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            ReadRequiredBytes(buffer);
            return BinaryPrimitives.ReadInt32LittleEndian(buffer);
        }

        /// <summary>
        /// Reads a 32-bit unsigned integer in little-endian order.
        /// </summary>
        /// <returns>Decoded unsigned integer.</returns>
        public override uint ReadUInt32() {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            ReadRequiredBytes(buffer);
            return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        }

        /// <summary>
        /// Reads a 64-bit signed integer in little-endian order.
        /// </summary>
        /// <returns>Decoded signed integer.</returns>
        public override long ReadInt64() {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            ReadRequiredBytes(buffer);
            return BinaryPrimitives.ReadInt64LittleEndian(buffer);
        }
    }
}
