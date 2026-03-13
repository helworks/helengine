using System.Buffers.Binary;

namespace helengine {
    /// <summary>
    /// Reads and writes the fixed little-endian HELE file header that precedes engine binary payloads.
    /// </summary>
    public static class EngineBinaryHeaderSerializer {
        /// <summary>
        /// Number of bytes stored in the fixed HELE header.
        /// </summary>
        const int HeaderLength = 12;

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
        /// Attempts to read and validate the standardized HELE header without throwing for non-HELE payloads.
        /// </summary>
        /// <param name="stream">Source stream containing the header.</param>
        /// <param name="header">Decoded header metadata when the payload uses the HELE header.</param>
        /// <returns>True when a valid HELE header was read.</returns>
        public static bool TryRead(Stream stream, out EngineBinaryHeader header) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            long originalPosition = 0;
            bool canSeek = stream.CanSeek;
            if (canSeek) {
                originalPosition = stream.Position;
            }

            header = null;
            Span<byte> buffer = stackalloc byte[HeaderLength];
            if (!TryReadRequiredBytes(stream, buffer)) {
                if (canSeek) {
                    stream.Position = originalPosition;
                }

                return false;
            }

            if (buffer[0] != (byte)'H' ||
                buffer[1] != (byte)'E' ||
                buffer[2] != (byte)'L' ||
                buffer[3] != (byte)'E') {
                if (canSeek) {
                    stream.Position = originalPosition;
                }

                return false;
            }

            EngineBinaryEndianness endianness = (EngineBinaryEndianness)buffer[4];
            if (endianness != EngineBinaryEndianness.LittleEndian &&
                endianness != EngineBinaryEndianness.BigEndian) {
                if (canSeek) {
                    stream.Position = originalPosition;
                }

                return false;
            }

            byte version = buffer[5];
            ushort formatId = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(6, 2));
            ushort recordKind = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(8, 2));
            ushort valueKind = BinaryPrimitives.ReadUInt16LittleEndian(buffer.Slice(10, 2));
            header = new EngineBinaryHeader(endianness, version, formatId, recordKind, valueKind);
            return true;
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

        /// <summary>
        /// Attempts to fill the supplied byte buffer from the stream.
        /// </summary>
        /// <param name="stream">Source stream to read.</param>
        /// <param name="buffer">Destination buffer to fill.</param>
        /// <returns>True when the buffer was filled completely.</returns>
        static bool TryReadRequiredBytes(Stream stream, Span<byte> buffer) {
            int totalBytesRead = 0;
            while (totalBytesRead < buffer.Length) {
                int bytesRead = stream.Read(buffer.Slice(totalBytesRead));
                if (bytesRead <= 0) {
                    return false;
                }

                totalBytesRead += bytesRead;
            }

            return true;
        }
    }
}
