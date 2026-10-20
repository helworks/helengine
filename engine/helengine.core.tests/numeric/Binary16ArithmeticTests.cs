using System.Runtime.InteropServices;

namespace helengine.core.tests.numeric {
    /// <summary>Verifies per-operation binary16 evaluation against independent System.Half arithmetic.</summary>
    public sealed class Binary16ArithmeticTests {
        /// <summary>The cook evaluator rounds operands and every primitive result instead of deferring until storage.</summary>
        [Fact]
        public void Binary16RoundsEachPrimitiveOperation() {
            NumericArithmetic math = new NumericArithmetic(new NumericPrecisionPolicy(16, 16));
            Assert.Equal(0d, math.Subtract(math.Add(2048d, 1d), 2048d));
            double[] values = { 0d, -0d, 1d, -1d, 2048d, 3d, 65504d, 0.00006103515625d, 0.000000059604644775390625d };
            foreach (double left in values) {
                foreach (double right in values) {
                    Half a = (Half)left; Half b = (Half)right;
                    AssertBits((Half)(a + b), math.Add(left, right));
                    AssertBits((Half)(a - b), math.Subtract(left, right));
                    AssertBits((Half)(a * b), math.Multiply(left, right));
                    AssertBits((Half)(a / b), math.Divide(left, right));
                    AssertBits((Half)(a % b), math.Remainder(left, right));
                }
                AssertBits(Half.Sqrt((Half)left), math.Sqrt(left));
            }
        }

        /// <summary>The compiled runtime uses two-byte evaluation and preserves its operation boundaries.</summary>
        [Fact]
        public void CompiledBinary16EvaluationMatchesCook() {
            if (RealRepresentation.ArithmeticBits != 16) { return; }
            Assert.Equal(2, Marshal.SizeOf<RealEvaluation>());
            RealEvaluation aboveTie = 1d + Math.ScaleB(1d, -11) + Math.ScaleB(1d, -40);
            Assert.Equal((ushort)0x3c01, Binary16Codec.Encode(aboveTie.ToDouble()));
            RealEvaluation a = 2048d;
            RealEvaluation b = 1d;
            Assert.Equal(0d, ((a + b) - a).ToDouble());
            NumericArithmetic math = new NumericArithmetic(RealRepresentation.Policy);
            AssertBits((Half)math.Divide(1d, 3d), ((RealEvaluation)1d / (RealEvaluation)3d).ToDouble());
            AssertBits((Half)math.Sqrt(2d), RealMath.Sqrt(2d).ToDouble());
        }

        /// <summary>The actual runtime quaternion kernel agrees with independent half operations and the native golden bits.</summary>
        [Fact]
        public void QuaternionNormalizationMatchesManaged() {
            if (RealRepresentation.StorageBits != 16 || RealRepresentation.ArithmeticBits != 16) { return; }
            float4 value = new float4(1d, 2d, 3d, 4d);
            value.Normalize();
            Half inverse = (Half)((Half)1 / Half.Sqrt((Half)30));
            AssertBits((Half)((Half)1 * inverse), value.X.ToDouble());
            AssertBits((Half)((Half)2 * inverse), value.Y.ToDouble());
            AssertBits((Half)((Half)3 * inverse), value.Z.ToDouble());
            AssertBits((Half)((Half)4 * inverse), value.W.ToDouble());
            Assert.Equal((ushort)0x31d8, Binary16Codec.Encode(value.X.ToDouble()));
            Assert.Equal((ushort)0x3862, Binary16Codec.Encode(value.Z.ToDouble()));
        }

        /// <summary>Scene-loading deadlines retain a fixed double clock even when configurable geometry is binary16.</summary>
        [Fact]
        public void FixedClockDoesNotBecomeBinary16() {
            Assert.Equal(typeof(double), typeof(IRuntimeSceneLoadingClock).GetProperty(nameof(IRuntimeSceneLoadingClock.ElapsedMilliseconds)).PropertyType);
            double milliseconds = 86400000d;
            milliseconds += 0.25d;
            Assert.Equal(86400000.25d, milliseconds);
        }

        /// <summary>Compares exact non-NaN encodings while accepting different valid NaN payloads.</summary>
        static void AssertBits(Half expected, double actual) {
            if (Half.IsNaN(expected)) { Assert.True(double.IsNaN(actual)); }
            else { Assert.Equal(BitConverter.HalfToUInt16Bits(expected), Binary16Codec.Encode(actual)); }
        }
    }
}
