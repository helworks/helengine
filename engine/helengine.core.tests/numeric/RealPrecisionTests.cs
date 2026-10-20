using System.Runtime.InteropServices;

namespace helengine.core.tests.numeric {
    /// <summary>Verifies configured scalar storage layout and IEEE conversion boundaries.</summary>
    public sealed class RealPrecisionTests {
        /// <summary>The physical managed scalar and intermediate layouts match their build contract.</summary>
        [Fact]
        public void StoredLayoutMatchesPolicy() {
            Assert.Equal(RealRepresentation.StorageBits / 8, Marshal.SizeOf<Real>());
            Assert.Equal(RealRepresentation.ArithmeticBits / 8, Marshal.SizeOf<RealEvaluation>());
            Assert.Equal($"real-v1-s{RealRepresentation.StorageBits}-a{RealRepresentation.ArithmeticBits}", RealRepresentation.Policy.Identity);
        }

        /// <summary>Numeric wrappers retain the standard typed comparison contract needed by collections.</summary>
        [Fact]
        public void ConfigurableScalarsSupportDefaultComparers() {
            Real[] stored = { 2d, -1d, 0d };
            RealEvaluation[] evaluated = { 2d, -1d, 0d };
            Array.Sort(stored);
            Array.Sort(evaluated);
            Assert.Equal(-1d, stored[0].ToDouble());
            Assert.Equal(2d, stored[2].ToDouble());
            Assert.Equal(-1d, evaluated[0].ToDouble());
            Assert.Equal(2d, evaluated[2].ToDouble());
        }

        /// <summary>Precision beyond binary32 survives only when selected storage can represent it.</summary>
        [Fact]
        public void StorageRetainsSelectedPrecision() {
            double expected = RealRepresentation.StorageBits == 16 ? double.PositiveInfinity : RealRepresentation.StorageBits == 64 ? 16777217d : 16777216d;
            Assert.Equal(expected, Real.FromDouble(16777217d).ToDouble());
        }

        /// <summary>Halfway conversion rounds to an even binary32 significand rather than away from zero.</summary>
        [Fact]
        public void ConversionRoundsToNearestEven() {
            double lowerTie = 1d + Math.ScaleB(1d, RealRepresentation.StorageBits == 16 ? -11 : -24);
            double upperTie = 1d + 3d * Math.ScaleB(1d, RealRepresentation.StorageBits == 16 ? -11 : -24);
            Assert.Equal(RealRepresentation.StorageBits != 64 ? 1d : lowerTie, Real.FromDouble(lowerTie).ToDouble());
            Assert.Equal(RealRepresentation.StorageBits == 16 ? 1d + Math.ScaleB(1d, -9) : RealRepresentation.StorageBits == 32 ? 1d + Math.ScaleB(1d, -22) : upperTie, Real.FromDouble(upperTie).ToDouble());
        }

        /// <summary>Non-finite values and negative zero preserve their IEEE classification and sign.</summary>
        [Fact]
        public void SpecialValuesRetainClassification() {
            Assert.True(double.IsNaN(Real.FromDouble(double.NaN).ToDouble()));
            Assert.Equal(double.PositiveInfinity, Real.FromDouble(double.PositiveInfinity).ToDouble());
            Assert.Equal(double.NegativeInfinity, Real.FromDouble(double.NegativeInfinity).ToDouble());
            Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(Real.FromDouble(-0d).ToDouble()));
            Assert.Equal(0L, BitConverter.DoubleToInt64Bits(Real.FromDouble(0d).ToDouble()));
        }

        /// <summary>Representable subnormal values do not become zero during storage conversion.</summary>
        [Fact]
        public void SubnormalValueSurvivesConversion() {
            double minimum = RealRepresentation.StorageBits == 16 ? Math.ScaleB(1d, -24) : RealRepresentation.StorageBits == 32 ? Math.ScaleB(1d, -149) : double.Epsilon;
            Assert.Equal(minimum, Real.FromDouble(minimum).ToDouble());
        }

        /// <summary>Overflow and underflow follow the selected storage format, including halfway-to-zero rounding.</summary>
        [Fact]
        public void OverflowAndUnderflowFollowStorageWidth() {
            Assert.Equal(RealRepresentation.StorageBits != 64 ? double.PositiveInfinity : double.MaxValue,
                Real.FromDouble(double.MaxValue).ToDouble());
            double halfway = Math.ScaleB(1d, RealRepresentation.StorageBits == 16 ? -25 : -150);
            Assert.Equal(RealRepresentation.StorageBits != 64 ? 0d : halfway, Real.FromDouble(halfway).ToDouble());
        }

        /// <summary>Fixed single-precision output remains an explicit conversion even in a double build.</summary>
        [Fact]
        public void ExplicitSingleConversionKeepsExternalWidth() {
            Assert.Equal(RealRepresentation.StorageBits == 16 ? 0.0999755859375f : 0.1f, Real.FromDouble(0.1d).ToSingle());
        }
    }
}
