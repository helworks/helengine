using System.Runtime.InteropServices;

namespace helengine.core.tests.numeric {
    /// <summary>Checks IEEE binary16 storage independently of the authoring host and preserves wider transient evaluation.</summary>
    public sealed class Binary16StorageTests {
        /// <summary>Explicit two-byte storage preserves boundary values, ties-to-even, signs and byte order.</summary>
        [Theory]
        [InlineData(EngineBinaryEndianness.LittleEndian)]
        [InlineData(EngineBinaryEndianness.BigEndian)]
        public void Binary16StorageMatchesIeeeBoundaries(EngineBinaryEndianness endianness) {
            NumericPrecisionPolicy policy = new NumericPrecisionPolicy(16, 32);
            double[] values = { 0d, -0d, Math.ScaleB(1d, -24), 1023d * Math.ScaleB(1d, -24), Math.ScaleB(1d, -14),
                65504d, 65520d, 1d + Math.ScaleB(1d, -11), 1d + 3d * Math.ScaleB(1d, -11),
                Math.ScaleB(1d, -25), 3d * Math.ScaleB(1d, -25), double.PositiveInfinity, double.NegativeInfinity };
            ushort[] expected = { 0x0000, 0x8000, 0x0001, 0x03ff, 0x0400, 0x7bff, 0x7c00, 0x3c00, 0x3c02, 0x0000, 0x0002, 0x7c00, 0xfc00 };
            using MemoryStream stream = new MemoryStream();
            using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, endianness);
            foreach (double value in values) { RealBinaryCodec.Write(writer, value, policy); }
            RealBinaryCodec.Write(writer, double.NaN, policy);
            Assert.Equal((values.Length + 1) * 2, stream.Length);
            byte[] bytes = stream.ToArray();
            for (int index = 0; index < expected.Length; index++) {
                int first = endianness == EngineBinaryEndianness.LittleEndian ? expected[index] & 255 : expected[index] >> 8;
                int second = endianness == EngineBinaryEndianness.LittleEndian ? expected[index] >> 8 : expected[index] & 255;
                Assert.Equal((byte)first, bytes[index * 2]);
                Assert.Equal((byte)second, bytes[index * 2 + 1]);
            }
            stream.Position = 0;
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, endianness);
            foreach (ushort bits in expected) {
                double expectedValue = (double)BitConverter.UInt16BitsToHalf(bits);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expectedValue), BitConverter.DoubleToInt64Bits(RealBinaryCodec.Read(reader, policy)));
            }
            Assert.True(double.IsNaN(RealBinaryCodec.Read(reader, policy)));
        }

        /// <summary>All finite half encodings and infinities roundtrip exactly; NaN payloads keep their classification.</summary>
        [Fact]
        public void EveryBinary16PatternRoundtrips() {
            for (int pattern = 0; pattern <= ushort.MaxValue; pattern++) {
                ushort bits = (ushort)pattern;
                float decoded = Binary16Codec.Decode(bits);
                float oracle = (float)BitConverter.UInt16BitsToHalf(bits);
                if (float.IsNaN(oracle)) { Assert.True(float.IsNaN(decoded)); }
                else {
                    Assert.Equal(BitConverter.SingleToInt32Bits(oracle), BitConverter.SingleToInt32Bits(decoded));
                    Assert.Equal(bits, Binary16Codec.Encode(decoded));
                }
            }
        }

        /// <summary>Fixed double input is rounded directly rather than losing the side of a half midpoint in a float conversion.</summary>
        [Fact]
        public void DoubleIngressDoesNotRoundThroughFloat() {
            double above = 1d + Math.ScaleB(1d, -11) + Math.ScaleB(1d, -40);
            double below = 1d + Math.ScaleB(1d, -11) - Math.ScaleB(1d, -40);
            Assert.Equal((ushort)0x3c01, Binary16Codec.Encode(above));
            Assert.Equal((ushort)0x3c00, Binary16Codec.Encode(below));
            double expected = RealRepresentation.StorageBits == 16 ? 1d + Math.ScaleB(1d, -10)
                : RealRepresentation.StorageBits == 32 ? (double)(float)above : above;
            Assert.Equal(expected, Real.FromDouble(above).ToDouble());
        }

        /// <summary>A transient result stays at the selected arithmetic width instead of narrowing to storage after each operator.</summary>
        [Fact]
        public void MixedArithmeticDoesNotNarrowEarly() {
            Real large = Real.FromDouble(2048d);
            Real one = Real.FromDouble(1d);
            RealEvaluation result = (large + one) - large;
            Assert.Equal(RealRepresentation.ArithmeticBits == 16 ? 0d : 1d, result.ToDouble());
            Assert.Equal(RealRepresentation.StorageBits / 8, Marshal.SizeOf<Real>());
            Assert.Equal(RealRepresentation.ArithmeticBits / 8, Marshal.SizeOf<RealEvaluation>());
        }

        /// <summary>An explicit stored intermediate rounds while the matching transient expression remains wider.</summary>
        [Fact]
        public void StoredIntermediateRoundsToBinary16() {
            Real large = Real.FromDouble(2048d);
            Real intermediate = large + Real.FromDouble(1d);
            Assert.Equal(RealRepresentation.StorageBits == 16 || RealRepresentation.ArithmeticBits == 16 ? 0d : 1d,
                ((RealEvaluation)intermediate - large).ToDouble());
            NumericArithmetic math = new NumericArithmetic(new NumericPrecisionPolicy(16, 32));
            Assert.Equal(1d, math.Subtract(math.Add(2048d, 1d), 2048d));
            Assert.Equal(0d, math.Subtract(math.Store(math.Add(2048d, 1d)), 2048d));
        }
    }
}
