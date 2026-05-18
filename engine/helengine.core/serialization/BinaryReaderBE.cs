using System.Buffers.Binary;

namespace helengine {
    /// <summary>
    /// Reads big-endian binary payloads from a stream.
    /// </summary>
    public class BinaryReaderBE : EngineBinaryReader {
        /// <summary>
        /// Initializes a new big-endian reader over the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing the payload bytes.</param>
        /// <param name="leaveOpen">True to leave the stream open when the reader is disposed.</param>
        public BinaryReaderBE(Stream stream, bool leaveOpen = true)
            : base(stream, leaveOpen) {
        }

        /// <summary>
        /// Gets the endianness handled by this reader.
        /// </summary>
        public override EngineBinaryEndianness Endianness => EngineBinaryEndianness.BigEndian;

        /// <summary>
        /// Reads a 16-bit unsigned integer in big-endian order.
        /// </summary>
        /// <returns>Decoded unsigned integer.</returns>
        public override ushort ReadUInt16() {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            ReadRequiredBytes(buffer);
            return BinaryPrimitives.ReadUInt16BigEndian(buffer);
        }

        /// <summary>
        /// Reads a 32-bit signed integer in big-endian order.
        /// </summary>
        /// <returns>Decoded signed integer.</returns>
        public override int ReadInt32() {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            ReadRequiredBytes(buffer);
            return BinaryPrimitives.ReadInt32BigEndian(buffer);
        }

        /// <summary>
        /// Reads a 32-bit unsigned integer in big-endian order.
        /// </summary>
        /// <returns>Decoded unsigned integer.</returns>
        public override uint ReadUInt32() {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            ReadRequiredBytes(buffer);
            return BinaryPrimitives.ReadUInt32BigEndian(buffer);
        }

        /// <summary>
        /// Reads a 64-bit signed integer in big-endian order.
        /// </summary>
        /// <returns>Decoded signed integer.</returns>
        public override long ReadInt64() {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            ReadRequiredBytes(buffer);
            return BinaryPrimitives.ReadInt64BigEndian(buffer);
        }

        /// <summary>
        /// Reads a double-precision floating point value in big-endian order.
        /// </summary>
        /// <returns>Decoded floating point value.</returns>
        public override double ReadDouble() {
            Span<byte> buffer = stackalloc byte[sizeof(double)];
            ReadRequiredBytes(buffer);
            return BinaryPrimitives.ReadDoubleBigEndian(buffer);
        }

    }
}
