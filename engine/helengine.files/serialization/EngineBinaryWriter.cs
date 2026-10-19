using System.Text;
using helengine;

namespace helengine.files {
    /// <summary>
    /// Provides endianness-specific binary writes over a stream after the writer has been selected once.
    /// </summary>
    public abstract class EngineBinaryWriter : IDisposable {
        /// <summary>
        /// Underlying stream receiving the payload bytes.
        /// </summary>
        protected readonly Stream BaseStream;

        /// <summary>Numeric policy used only by configurable engine values in this stream.</summary>
        NumericPrecisionPolicy NumericPolicyValue = RealRepresentation.Policy;


        /// <summary>
        /// Indicates whether disposing the writer should leave the underlying stream open.
        /// </summary>
        readonly bool LeaveOpen;

        /// <summary>
        /// Initializes a new binary writer over the supplied stream.
        /// </summary>
        /// <param name="stream">Destination stream for the payload.</param>
        /// <param name="leaveOpen">True to leave the stream open when the writer is disposed.</param>
        protected EngineBinaryWriter(Stream stream, bool leaveOpen = true) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            BaseStream = stream;
            LeaveOpen = leaveOpen;
        }

        /// <summary>
        /// Gets the payload endianness handled by this writer.
        /// </summary>
        public abstract EngineBinaryEndianness Endianness { get; }

        /// <summary>Gets or sets the implemented numeric contract without changing fixed-width writes.</summary>
        public NumericPrecisionPolicy NumericPolicy {
            get { return NumericPolicyValue; }
            set { RealBinaryCodec.ValidatePolicy(value); NumericPolicyValue = value; }
        }

        /// <summary>Writes fixed IEEE64 transport using the stream's byte order.</summary>
        public void WriteDouble(double value) { WriteInt64(BitConverter.DoubleToInt64Bits(value)); }

        /// <summary>Writes one configurable stored scalar under the selected stream policy.</summary>
        public void WriteReal(Real value) {
            if (NumericPolicy.StorageBits == 32) { WriteSingle(value.ToSingle()); }
            else { WriteDouble(value.ToDouble()); }
        }

        /// <summary>Writes a configurable stored two-component vector.</summary>
        public void WriteReal2(float2 value) { WriteReal(value.X); WriteReal(value.Y); }

        /// <summary>Writes a configurable stored three-component vector.</summary>
        public void WriteReal3(float3 value) { WriteReal(value.X); WriteReal(value.Y); WriteReal(value.Z); }

        /// <summary>Writes a configurable stored four-component vector.</summary>
        public void WriteReal4(float4 value) { WriteReal(value.X); WriteReal(value.Y); WriteReal(value.Z); WriteReal(value.W); }

        /// <summary>Records the stream's explicit numeric representation before any configurable values.</summary>
        public void WriteNumericPolicy() {
            RealBinaryCodec.ValidatePolicy(NumericPolicy);
            WriteByte((byte)NumericPolicy.RepresentationVersion);
            WriteByte((byte)NumericPolicy.StorageBits);
            WriteByte((byte)NumericPolicy.ArithmeticBits);
        }


        /// <summary>
        /// Creates a writer for the requested payload endianness.
        /// </summary>
        /// <param name="stream">Destination stream for the payload bytes.</param>
        /// <param name="endianness">Endianness to encode.</param>
        /// <param name="leaveOpen">True to leave the stream open when the writer is disposed.</param>
        /// <returns>Concrete writer for the selected endianness.</returns>
        public static EngineBinaryWriter Create(Stream stream, EngineBinaryEndianness endianness, bool leaveOpen = true) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            } else if (endianness == EngineBinaryEndianness.LittleEndian) {
                return new BinaryWriterLE(stream, leaveOpen);
            } else if (endianness == EngineBinaryEndianness.BigEndian) {
                return new BinaryWriterBE(stream, leaveOpen);
            }

            throw new InvalidOperationException($"Unsupported binary payload endianness '{(byte)endianness}'.");
        }

        /// <summary>
        /// Writes one byte to the stream.
        /// </summary>
        /// <param name="value">Byte value to write.</param>
        public void WriteByte(byte value) {
            BaseStream.WriteByte(value);
        }

        /// <summary>
        /// Writes a 16-bit unsigned integer using the writer's endianness.
        /// </summary>
        /// <param name="value">Value to write.</param>
        public abstract void WriteUInt16(ushort value);

        /// <summary>
        /// Writes a 32-bit signed integer using the writer's endianness.
        /// </summary>
        /// <param name="value">Value to write.</param>
        public abstract void WriteInt32(int value);

        /// <summary>
        /// Writes a 32-bit unsigned integer using the writer's endianness.
        /// </summary>
        /// <param name="value">Value to write.</param>
        public abstract void WriteUInt32(uint value);

        /// <summary>
        /// Writes a 64-bit signed integer using the writer's endianness.
        /// </summary>
        /// <param name="value">Value to write.</param>
        public abstract void WriteInt64(long value);

        /// <summary>
        /// Writes a two-component integer vector using the writer's endianness.
        /// </summary>
        /// <param name="value">Value to write.</param>
        public void WriteInt2(int2 value) {
            WriteInt32(value.X);
            WriteInt32(value.Y);
        }

        /// <summary>
        /// Writes a four-component integer vector using the writer's endianness.
        /// </summary>
        /// <param name="value">Value to write.</param>
        public void WriteInt4(int4 value) {
            WriteInt32(value.X);
            WriteInt32(value.Y);
            WriteInt32(value.Z);
            WriteInt32(value.W);
        }

        /// <summary>
        /// Writes a two-component floating point vector using the writer's endianness.
        /// </summary>
        /// <param name="value">Value to write.</param>
        public void WriteFloat2(float2 value) {
            WriteSingle((float)(value.X));
            WriteSingle((float)(value.Y));
        }

        /// <summary>
        /// Writes a three-component floating point vector using the writer's endianness.
        /// </summary>
        /// <param name="value">Value to write.</param>
        public void WriteFloat3(float3 value) {
            WriteSingle((float)(value.X));
            WriteSingle((float)(value.Y));
            WriteSingle((float)(value.Z));
        }

        /// <summary>
        /// Writes a four-component floating point vector using the writer's endianness.
        /// </summary>
        /// <param name="value">Value to write.</param>
        public void WriteFloat4(float4 value) {
            WriteSingle((float)(value.X));
            WriteSingle((float)(value.Y));
            WriteSingle((float)(value.Z));
            WriteSingle((float)(value.W));
        }

        /// <summary>
        /// Writes a single-precision floating point value using the writer's endianness.
        /// </summary>
        /// <param name="value">Value to write.</param>
        public void WriteSingle(float value) {
            WriteInt32(BitConverter.SingleToInt32Bits(value));
        }

        /// <summary>
        /// Writes a UTF-8 string prefixed by a 32-bit length.
        /// </summary>
        /// <param name="value">String value to write.</param>
        public void WriteString(string value) {
            if (value == null) {
                WriteInt32(-1);
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            WriteInt32(bytes.Length);
            BaseStream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Writes a byte array prefixed by a 32-bit length.
        /// </summary>
        /// <param name="value">Byte array to write.</param>
        public void WriteByteArray(byte[] value) {
            if (value == null) {
                WriteInt32(-1);
                return;
            }

            WriteInt32(value.Length);
            BaseStream.Write(value, 0, value.Length);
        }

        /// <summary>
        /// Writes one optional scene entity reference value using the writer's endianness.
        /// </summary>
        /// <param name="reference">Scene entity reference to write.</param>
        public void WriteSceneEntityReference(SceneEntityReference reference) {
            WriteByte(reference == null ? (byte)0 : (byte)1);
            if (reference == null) {
                return;
            }

            if (reference.EntityId == 0u) {
                throw new InvalidOperationException("Scene entity references must define a non-zero entity id.");
            }

            WriteUInt32(reference.EntityId);
        }

        /// <summary>
        /// Writes an array prefixed by a 32-bit length.
        /// </summary>
        /// <typeparam name="T">Element type stored in the array.</typeparam>
        /// <param name="values">Array to write.</param>
        /// <param name="writeElement">Delegate that writes one element.</param>
        public void WriteArray<T>(T[] values, Action<EngineBinaryWriter, T> writeElement) {
            if (writeElement == null) {
                throw new ArgumentNullException(nameof(writeElement));
            }

            if (values == null) {
                WriteInt32(-1);
                return;
            }

            WriteInt32(values.Length);
            for (int i = 0; i < values.Length; i++) {
                writeElement(this, values[i]);
            }
        }

        /// <summary>
        /// Releases the writer and optionally the underlying stream.
        /// </summary>
        public void Dispose() {
            if (!LeaveOpen) {
                BaseStream.Dispose();
            }
        }
    }
}
