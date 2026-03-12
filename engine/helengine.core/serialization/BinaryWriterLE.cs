using System.Buffers.Binary;

namespace helengine {
    /// <summary>
    /// Writes little-endian binary payloads to a stream.
    /// </summary>
    public class BinaryWriterLE : EngineBinaryWriter {
        /// <summary>
        /// Initializes a new little-endian writer over the supplied stream.
        /// </summary>
        /// <param name="stream">Destination stream for the payload bytes.</param>
        /// <param name="leaveOpen">True to leave the stream open when the writer is disposed.</param>
        public BinaryWriterLE(Stream stream, bool leaveOpen = true)
            : base(stream, leaveOpen) {
        }

        /// <summary>
        /// Gets the endianness handled by this writer.
        /// </summary>
        public override EngineBinaryEndianness Endianness => EngineBinaryEndianness.LittleEndian;

        /// <summary>
        /// Writes a 16-bit unsigned integer in little-endian order.
        /// </summary>
        /// <param name="value">Value to write.</param>
        public override void WriteUInt16(ushort value) {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
            Stream.Write(buffer);
        }

        /// <summary>
        /// Writes a 32-bit signed integer in little-endian order.
        /// </summary>
        /// <param name="value">Value to write.</param>
        public override void WriteInt32(int value) {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            Stream.Write(buffer);
        }

        /// <summary>
        /// Writes a 64-bit signed integer in little-endian order.
        /// </summary>
        /// <param name="value">Value to write.</param>
        public override void WriteInt64(long value) {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
            Stream.Write(buffer);
        }
    }
}
