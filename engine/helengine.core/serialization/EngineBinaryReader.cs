using System.Text;

namespace helengine {
    /// <summary>
    /// Provides endianness-specific binary reads over a stream after the reader has been selected once.
    /// </summary>
    public abstract class EngineBinaryReader : IDisposable {
        /// <summary>
        /// Underlying stream supplying the payload bytes.
        /// </summary>
        readonly Stream BaseStream;

        /// <summary>
        /// Indicates whether disposing the reader should leave the underlying stream open.
        /// </summary>
        readonly bool LeaveOpen;

        /// <summary>
        /// Initializes a new binary reader over the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing the binary payload.</param>
        /// <param name="leaveOpen">True to leave the stream open when the reader is disposed.</param>
        protected EngineBinaryReader(Stream stream, bool leaveOpen = true) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            BaseStream = stream;
            LeaveOpen = leaveOpen;
        }

        /// <summary>
        /// Gets the payload endianness handled by this reader.
        /// </summary>
        public abstract EngineBinaryEndianness Endianness { get; }

        /// <summary>
        /// Creates a reader for the requested payload endianness.
        /// </summary>
        /// <param name="stream">Stream containing the payload bytes.</param>
        /// <param name="endianness">Endianness to decode.</param>
        /// <param name="leaveOpen">True to leave the stream open when the reader is disposed.</param>
        /// <returns>Concrete reader for the selected endianness.</returns>
        public static EngineBinaryReader Create(Stream stream, EngineBinaryEndianness endianness, bool leaveOpen = true) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (endianness == EngineBinaryEndianness.LittleEndian) {
                return new BinaryReaderLE(stream, leaveOpen);
            } else if (endianness == EngineBinaryEndianness.BigEndian) {
                return new BinaryReaderBE(stream, leaveOpen);
            }

            throw new InvalidOperationException($"Unsupported binary payload endianness '{(byte)endianness}'.");
        }

        /// <summary>
        /// Reads one byte from the stream.
        /// </summary>
        /// <returns>Decoded byte value.</returns>
        public byte ReadByte() {
            int value = BaseStream.ReadByte();
            if (value < 0) {
                throw new EndOfStreamException("Unexpected end of stream while reading engine binary data.");
            }

            return (byte)value;
        }

        /// <summary>
        /// Reads a 16-bit unsigned integer using the reader's endianness.
        /// </summary>
        /// <returns>Decoded unsigned integer.</returns>
        public abstract ushort ReadUInt16();

        /// <summary>
        /// Reads a 32-bit signed integer using the reader's endianness.
        /// </summary>
        /// <returns>Decoded signed integer.</returns>
        public abstract int ReadInt32();

        /// <summary>
        /// Reads a 32-bit unsigned integer using the reader's endianness.
        /// </summary>
        /// <returns>Decoded unsigned integer.</returns>
        public abstract uint ReadUInt32();

        /// <summary>
        /// Reads a 64-bit signed integer using the reader's endianness.
        /// </summary>
        /// <returns>Decoded signed integer.</returns>
        public abstract long ReadInt64();

        /// <summary>
        /// Reads a two-component integer vector using the reader's endianness.
        /// </summary>
        /// <returns>Decoded integer vector.</returns>
        public int2 ReadInt2() {
            return new int2(ReadInt32(), ReadInt32());
        }

        /// <summary>
        /// Reads a four-component integer vector using the reader's endianness.
        /// </summary>
        /// <returns>Decoded integer vector.</returns>
        public int4 ReadInt4() {
            return new int4(ReadInt32(), ReadInt32(), ReadInt32(), ReadInt32());
        }

        /// <summary>
        /// Reads a two-component floating point vector using the reader's endianness.
        /// </summary>
        /// <returns>Decoded floating point vector.</returns>
        public float2 ReadFloat2() {
            return new float2(ReadSingle(), ReadSingle());
        }

        /// <summary>
        /// Reads a three-component floating point vector using the reader's endianness.
        /// </summary>
        /// <returns>Decoded floating point vector.</returns>
        public float3 ReadFloat3() {
            return new float3(ReadSingle(), ReadSingle(), ReadSingle());
        }

        /// <summary>
        /// Reads a four-component floating point vector using the reader's endianness.
        /// </summary>
        /// <returns>Decoded floating point vector.</returns>
        public float4 ReadFloat4() {
            return new float4(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        /// <summary>
        /// Reads a single-precision floating point value using the reader's endianness.
        /// </summary>
        /// <returns>Decoded floating point value.</returns>
        public float ReadSingle() {
            return BitConverter.Int32BitsToSingle(ReadInt32());
        }

        /// <summary>
        /// Reads a double-precision floating point value using the reader's endianness.
        /// </summary>
        /// <returns>Decoded floating point value.</returns>
        public abstract double ReadDouble();

        /// <summary>
        /// Reads a UTF-8 string prefixed by a 32-bit length.
        /// </summary>
        /// <returns>Decoded string value.</returns>
        public string ReadString() {
            int length = ReadInt32();
            if (length == -1) {
                return string.Empty;
            } else if (length < -1) {
                throw new InvalidOperationException("String length cannot be negative.");
            } else if (length == 0) {
                return string.Empty;
            }

            byte[] bytes = ReadBytes(length);
            try {
                return Encoding.UTF8.GetString(bytes);
            } finally {
                NativeOwnership.Delete(bytes);
            }
        }

        /// <summary>
        /// Reads a byte array prefixed by a 32-bit length.
        /// </summary>
        /// <returns>Decoded byte array.</returns>
        public byte[] ReadByteArray() {
            int length = ReadInt32();
            if (length == -1) {
                return null;
            } else if (length < -1) {
                throw new InvalidOperationException("Byte array length cannot be negative.");
            } else if (length == 0) {
                return Array.Empty<byte>();
            }

            return ReadBytes(length);
        }

        /// <summary>
        /// Reads one optional scene entity reference value using the reader's endianness.
        /// </summary>
        /// <returns>Decoded scene entity reference or null when the value was not present.</returns>
        public SceneEntityReference ReadSceneEntityReference() {
            if (ReadByte() == 0) {
                return null;
            }

            return new SceneEntityReference {
                EntityId = ReadUInt32()
            };
        }

        /// <summary>
        /// Reads an array prefixed by a 32-bit length.
        /// </summary>
        /// <typeparam name="T">Element type stored in the array.</typeparam>
        /// <param name="readElement">Delegate that reads one element.</param>
        /// <returns>Decoded array.</returns>
        public T[] ReadArray<T>(Func<EngineBinaryReader, T> readElement) {
            if (readElement == null) {
                throw new ArgumentNullException(nameof(readElement));
            }

            int length = ReadInt32();
            if (length == -1) {
                return null;
            } else if (length < -1) {
                throw new InvalidOperationException("Array length cannot be negative.");
            } else if (length == 0) {
                return Array.Empty<T>();
            }

            T[] values = new T[length];
            for (int i = 0; i < values.Length; i++) {
                values[i] = readElement(this);
            }

            return values;
        }

        /// <summary>
        /// Releases the reader and optionally the underlying stream.
        /// </summary>
        public void Dispose() {
            if (!LeaveOpen) {
                BaseStream.Dispose();
            }
        }

        /// <summary>
        /// Reads an exact number of bytes from the stream.
        /// </summary>
        /// <param name="length">Number of bytes to read.</param>
        /// <returns>Filled byte array.</returns>
        protected byte[] ReadBytes(int length) {
            if (length < 0) {
                throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative.");
            }

            byte[] buffer = new byte[length];
            ReadRequiredBytes(buffer);
            return buffer;
        }

        /// <summary>
        /// Fills the supplied byte span from the stream or throws when the stream ends early.
        /// </summary>
        /// <param name="buffer">Destination span to fill.</param>
        protected void ReadRequiredBytes(Span<byte> buffer) {
            int totalBytesRead = 0;
            while (totalBytesRead < buffer.Length) {
                int bytesRead = BaseStream.Read(buffer.Slice(totalBytesRead));
                if (bytesRead <= 0) {
                    throw new EndOfStreamException("Unexpected end of stream while reading engine binary data.");
                }

                totalBytesRead += bytesRead;
            }
        }
    }
}
