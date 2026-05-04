using helengine;

namespace helengine.files {
    /// <summary>
    /// Writes the fixed HELE file header that precedes engine binary payloads.
    /// </summary>
    public static class EngineBinaryHeaderSerializer {
        /// <summary>
        /// Writes the standardized HELE header to the supplied stream.
        /// </summary>
        /// <param name="stream">Destination stream for the header.</param>
        /// <param name="header">Header metadata to write.</param>
        public static void Write(Stream stream, EngineBinaryHeader header) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (header == null) {
                throw new ArgumentNullException(nameof(header));
            }

            ValidateEndianness(header.Endianness);
            using BinaryWriterLE writer = new BinaryWriterLE(stream);
            writer.WriteByte((byte)'H');
            writer.WriteByte((byte)'E');
            writer.WriteByte((byte)'L');
            writer.WriteByte((byte)'E');
            writer.WriteByte((byte)header.Endianness);
            writer.WriteByte(header.Version);
            writer.WriteUInt16(header.FormatId);
            writer.WriteUInt16(header.RecordKind);
            writer.WriteUInt16(header.ValueKind);
        }

        /// <summary>
        /// Reads and validates the standardized HELE header from the supplied stream.
        /// </summary>
        /// <param name="stream">Source stream containing the header.</param>
        /// <returns>Decoded header metadata.</returns>
        public static EngineBinaryHeader Read(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            using BinaryReaderLE reader = new BinaryReaderLE(stream);
            if (reader.ReadByte() != (byte)'H' ||
                reader.ReadByte() != (byte)'E' ||
                reader.ReadByte() != (byte)'L' ||
                reader.ReadByte() != (byte)'E') {
                throw new InvalidOperationException("The binary payload does not start with the HELE header.");
            }

            EngineBinaryEndianness endianness = (EngineBinaryEndianness)reader.ReadByte();
            ValidateEndianness(endianness);

            byte version = reader.ReadByte();
            ushort formatId = reader.ReadUInt16();
            ushort recordKind = reader.ReadUInt16();
            ushort valueKind = reader.ReadUInt16();
            return new EngineBinaryHeader(endianness, version, formatId, recordKind, valueKind);
        }

        /// <summary>
        /// Validates that the payload endianness code is supported.
        /// </summary>
        /// <param name="endianness">Endianness code to validate.</param>
        static void ValidateEndianness(EngineBinaryEndianness endianness) {
            if (endianness != EngineBinaryEndianness.LittleEndian &&
                endianness != EngineBinaryEndianness.BigEndian) {
                throw new InvalidOperationException($"Unsupported binary payload endianness '{(byte)endianness}'.");
            }
        }
    }
}
