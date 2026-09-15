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
        /// <summary>
        /// Reads a HELE header without throwing, for callers that probe arbitrary files.
        /// </summary>
        /// <param name="stream">Stream positioned at the start of the payload.</param>
        /// <param name="header">Parsed header when the payload starts with a valid HELE header.</param>
        /// <returns>True when a complete, valid header was read; false when the payload is too short, lacks the magic, or has an unsupported endianness.</returns>
        public static bool TryRead([NativeNoEscape] Stream stream, out EngineBinaryHeader header) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            header = null;
            if (stream.ReadByte() != (byte)'H' ||
                stream.ReadByte() != (byte)'E' ||
                stream.ReadByte() != (byte)'L' ||
                stream.ReadByte() != (byte)'E') {
                return false;
            }

            int endiannessValue = stream.ReadByte();
            if (endiannessValue != (int)EngineBinaryEndianness.LittleEndian &&
                endiannessValue != (int)EngineBinaryEndianness.BigEndian) {
                return false;
            }

            int version = stream.ReadByte();
            if (version < 0) {
                return false;
            }

            if (!TryReadUInt16LittleEndian(stream, out ushort formatId) ||
                !TryReadUInt16LittleEndian(stream, out ushort recordKind) ||
                !TryReadUInt16LittleEndian(stream, out ushort valueKind)) {
                return false;
            }

            header = new EngineBinaryHeader((EngineBinaryEndianness)endiannessValue, (byte)version, formatId, recordKind, valueKind);
            return true;
        }

        static bool TryReadUInt16LittleEndian([NativeNoEscape] Stream stream, out ushort value) {
            int low = stream.ReadByte();
            int high = stream.ReadByte();
            if (low < 0 || high < 0) {
                value = 0;
                return false;
            }

            value = (ushort)(low | (high << 8));
            return true;
        }

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
