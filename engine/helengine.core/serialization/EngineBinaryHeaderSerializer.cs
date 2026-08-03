namespace helengine {
    /// <summary>
    /// Reads and writes the fixed little-endian HELE file header that precedes engine binary payloads.
    /// </summary>
    public static class EngineBinaryHeaderSerializer {
        /// <summary>
        /// Writes the standardized HELE header to the supplied stream.
        /// </summary>
        /// <param name="stream">Destination stream for the header.</param>
        /// <param name="header">Header metadata to write.</param>
        public static void Write([NativeNoEscape] Stream stream, [NativeNoEscape] EngineBinaryHeader header) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (header == null) {
                throw new ArgumentNullException(nameof(header));
            }

            ValidateEndianness(header.Endianness);
            stream.WriteByte((byte)'H');
            stream.WriteByte((byte)'E');
            stream.WriteByte((byte)'L');
            stream.WriteByte((byte)'E');
            stream.WriteByte((byte)header.Endianness);
            stream.WriteByte(header.Version);
            WriteUInt16LittleEndian(stream, header.FormatId);
            WriteUInt16LittleEndian(stream, header.RecordKind);
            WriteUInt16LittleEndian(stream, header.ValueKind);
        }

        /// <summary>
        /// Reads and validates the standardized HELE header from the supplied stream.
        /// </summary>
        /// <param name="stream">Source stream containing the header.</param>
        /// <returns>Decoded header metadata.</returns>
        public static EngineBinaryHeader Read([NativeNoEscape] Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            if (ReadRequiredByte(stream) != (byte)'H' ||
                ReadRequiredByte(stream) != (byte)'E' ||
                ReadRequiredByte(stream) != (byte)'L' ||
                ReadRequiredByte(stream) != (byte)'E') {
                throw new InvalidOperationException("The binary payload does not start with the HELE header.");
            }

            EngineBinaryEndianness endianness = (EngineBinaryEndianness)ReadRequiredByte(stream);
            ValidateEndianness(endianness);

            byte version = ReadRequiredByte(stream);
            ushort formatId = ReadUInt16LittleEndian(stream);
            ushort recordKind = ReadUInt16LittleEndian(stream);
            ushort valueKind = ReadUInt16LittleEndian(stream);
            return new EngineBinaryHeader(endianness, version, formatId, recordKind, valueKind);
        }

        /// <summary>
        /// Reads one required byte and reports truncated header data as a deterministic end-of-stream failure.
        /// </summary>
        /// <param name="stream">Source stream positioned at the byte to read.</param>
        /// <returns>The next byte from the stream.</returns>
        static byte ReadRequiredByte([NativeNoEscape] Stream stream) {
            int value = stream.ReadByte();
            if (value < 0) {
                throw new EndOfStreamException("The binary payload ended before the HELE header was complete.");
            }

            return (byte)value;
        }

        /// <summary>
        /// Reads one unsigned 16-bit HELE header value in the format's fixed little-endian byte order.
        /// </summary>
        /// <param name="stream">Source stream positioned at the two-byte value.</param>
        /// <returns>The decoded unsigned value.</returns>
        static ushort ReadUInt16LittleEndian([NativeNoEscape] Stream stream) {
            byte low = ReadRequiredByte(stream);
            byte high = ReadRequiredByte(stream);
            return (ushort)(low | (high << 8));
        }

        /// <summary>
        /// Writes one unsigned 16-bit HELE header value in the format's fixed little-endian byte order.
        /// </summary>
        /// <param name="stream">Destination stream for the two-byte value.</param>
        /// <param name="value">Unsigned value to encode.</param>
        static void WriteUInt16LittleEndian([NativeNoEscape] Stream stream, ushort value) {
            stream.WriteByte((byte)(value & 0xFF));
            stream.WriteByte((byte)(value >> 8));
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
