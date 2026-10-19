namespace helengine.core.tests.numeric {
    /// <summary>Checks explicit numeric payload widths independently of the managed host representation.</summary>
    public sealed class RealBinaryCodecTests {
        /// <summary>Selected storage is encoded with the stream endianness and the exact declared width.</summary>
        [Theory]
        [InlineData(32, 32, EngineBinaryEndianness.LittleEndian)]
        [InlineData(32, 64, EngineBinaryEndianness.BigEndian)]
        [InlineData(64, 64, EngineBinaryEndianness.LittleEndian)]
        [InlineData(64, 32, EngineBinaryEndianness.BigEndian)]
        public void ScalarRoundTripsSelectedWidth(int storage, int arithmetic, EngineBinaryEndianness endianness) {
            NumericPrecisionPolicy policy = new(storage, arithmetic);
            using MemoryStream stream = new();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, endianness);
            RealBinaryCodec.Write(writer, 16777217d, policy);
            Assert.Equal(storage / 8, stream.Length);
            byte[] expected = storage == 32 ? BitConverter.GetBytes(16777216f) : BitConverter.GetBytes(16777217d);
            if ((endianness == EngineBinaryEndianness.LittleEndian) != BitConverter.IsLittleEndian) { Array.Reverse(expected); }
            Assert.Equal(expected, stream.ToArray());
            stream.Position = 0;
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, endianness);
            Assert.Equal(storage == 32 ? 16777216d : 16777217d, RealBinaryCodec.Read(reader, policy));
        }

        /// <summary>Mismatched numeric contracts fail before the first precision-dependent payload read.</summary>
        [Theory]
        [InlineData(32, 32, 64, 64)]
        [InlineData(32, 32, 32, 64)]
        public void RejectsPolicyBeforePayloadRead(int stored, int evaluated, int expectedStored, int expectedEvaluated) {
            using MemoryStream stream = new();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            RealBinaryCodec.WritePolicy(writer, new(stored, evaluated));
            long payloadStart = stream.Position;
            writer.WriteDouble(123d);
            stream.Position = 0;
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            Assert.Throws<InvalidOperationException>(() => RealBinaryCodec.ReadAndValidatePolicy(reader, new(expectedStored, expectedEvaluated)));
            Assert.Equal(payloadStart, stream.Position);
        }

        /// <summary>Numeric headers are immutable versioned storage/arithmetic contracts.</summary>
        [Fact]
        public void HeaderCarriesVersionAndBothWidths() {
            using MemoryStream stream = new();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.BigEndian);
            RealBinaryCodec.WritePolicy(writer, new(32, 64));
            Assert.Equal(new byte[] { 1, 32, 64 }, stream.ToArray());
            stream.Position = 0;
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.BigEndian);
            Assert.Equal("real-v1-s32-a64", RealBinaryCodec.ReadAndValidatePolicy(reader, new(32, 64)).Identity);
        }

        /// <summary>Unsupported policy versions never reach numeric payload decoding.</summary>
        [Fact]
        public void RejectsUnknownVersionBeforePayloadRead() {
            using MemoryStream stream = new(new byte[] { 2, 32, 32, 42 });
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            Assert.Throws<InvalidOperationException>(() => RealBinaryCodec.ReadAndValidatePolicy(reader, new(32, 32)));
            Assert.Equal(3, stream.Position);
        }

        /// <summary>Fixed legacy single/double methods keep their actual widths alongside configurable IO.</summary>
        [Fact]
        public void FixedWidthMethodsRemainFixed() {
            using MemoryStream stream = new();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
            writer.WriteSingle(1.25f);
            writer.WriteDouble(16777217d);
            writer.WriteReal(Real.FromDouble(2.5), new(64, 64));
            Assert.Equal(20, stream.Length);
            stream.Position = 0;
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            Assert.Equal(1.25f, reader.ReadSingle());
            Assert.Equal(16777217d, reader.ReadDouble());
            Assert.Equal(2.5, reader.ReadReal(new(64, 64)).ToDouble());
        }

        /// <summary>Special values retain their IEEE classification and zero sign through either byte order.</summary>
        [Theory]
        [InlineData(32, EngineBinaryEndianness.LittleEndian)]
        [InlineData(64, EngineBinaryEndianness.BigEndian)]
        public void SpecialValuesRoundTrip(int bits, EngineBinaryEndianness endianness) {
            NumericPrecisionPolicy policy = new(bits, bits);
            using MemoryStream stream = new();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, endianness);
            RealBinaryCodec.Write(writer, double.NaN, policy);
            RealBinaryCodec.Write(writer, double.PositiveInfinity, policy);
            RealBinaryCodec.Write(writer, -0d, policy);
            stream.Position = 0;
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, endianness);
            Assert.True(double.IsNaN(RealBinaryCodec.Read(reader, policy)));
            Assert.Equal(double.PositiveInfinity, RealBinaryCodec.Read(reader, policy));
            Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(RealBinaryCodec.Read(reader, policy)));
        }
    }
}
