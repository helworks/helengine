using System.Buffers.Binary;
using helengine;

namespace helengine.files {
    /// <summary>
    /// Writes big-endian binary payloads to a stream.
    /// </summary>
    public class BinaryWriterBE : EngineBinaryWriter {
        /// <summary>
        /// Initializes a new big-endian writer over the supplied stream.
        /// </summary>
        /// <param name="stream">Destination stream for the payload bytes.</param>
        /// <param name="leaveOpen">True to leave the stream open when the writer is disposed.</param>
        public BinaryWriterBE(Stream stream, bool leaveOpen = true)
            : base(stream, leaveOpen) {
        }

        /// <summary>
        /// Gets the endianness handled by this writer.
        /// </summary>
        public override EngineBinaryEndianness Endianness => EngineBinaryEndianness.BigEndian;

        /// <summary>
        /// Writes a 16-bit unsigned integer in big-endian order.
        /// </summary>
        /// <param name="value">Value to write.</param>
        public override void WriteUInt16(ushort value) {
            Span<byte> buffer = stackalloc byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
            BaseStream.Write(buffer);
        }

        /// <summary>
        /// Writes a 32-bit signed integer in big-endian order.
        /// </summary>
        /// <param name="value">Value to write.</param>
        public override void WriteInt32(int value) {
            Span<byte> buffer = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(buffer, value);
            BaseStream.Write(buffer);
        }

        /// <summary>
        /// Writes a 32-bit unsigned integer in big-endian order.
        /// </summary>
        /// <param name="value">Value to write.</param>
        public override void WriteUInt32(uint value) {
            Span<byte> buffer = stackalloc byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
            BaseStream.Write(buffer);
        }

        /// <summary>
        /// Writes a 64-bit signed integer in big-endian order.
        /// </summary>
        /// <param name="value">Value to write.</param>
        public override void WriteInt64(long value) {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64BigEndian(buffer, value);
            BaseStream.Write(buffer);
        }
    }
}
