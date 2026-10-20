namespace helengine.core.tests.numeric {
    /// <summary>Verifies expression evaluation and per-build cook arithmetic obey selected widths.</summary>
    public sealed class RealArithmeticTests {
        /// <summary>A cancellation expression reveals accidental promotion of single arithmetic to double.</summary>
        [Fact]
        public void EvaluationDoesNotPromoteToDouble() {
            Real large = Real.FromDouble(RealRepresentation.StorageBits == 16 ? 2048d : 16777216d);
            Real one = Real.FromDouble(1d);
            RealEvaluation result = (large + one) - large;
            Assert.Equal(RealRepresentation.ArithmeticBits == 64 || RealRepresentation.StorageBits == 16 && RealRepresentation.ArithmeticBits == 32 ? 1d : 0d, result.ToDouble());
        }

        /// <summary>Division rounds to the evaluation format before conversion to assertion transport.</summary>
        [Fact]
        public void DivisionUsesEvaluationWidth() {
            RealEvaluation result = Real.FromDouble(1d) / Real.FromDouble(10d);
            Assert.Equal(RealRepresentation.ArithmeticBits == 16 ? 0.0999755859375d : RealRepresentation.ArithmeticBits == 32 ? (double)(1f / 10f) : 0.1d, result.ToDouble());
        }

        /// <summary>An expression result becomes a stored scalar only at its destination conversion.</summary>
        [Fact]
        public void ExplicitStoreIsTheNarrowingBoundary() {
            RealEvaluation result = Real.FromDouble(1d) / Real.FromDouble(10d);
            Real stored = result;
            double expected = RealRepresentation.StorageBits == 16 ? (double)(Half)result.ToDouble() : RealRepresentation.StorageBits == 32 ? (double)(float)result.ToDouble() : result.ToDouble();
            Assert.Equal(expected, stored.ToDouble());
        }

        /// <summary>Interpolation and square root follow selected arithmetic rather than host double defaults.</summary>
        [Fact]
        public void MathHelpersUseEvaluationWidth() {
            RealEvaluation lerped = RealMath.Lerp(Real.FromDouble(RealRepresentation.StorageBits == 16 ? 2048d : 16777216d), Real.FromDouble(RealRepresentation.StorageBits == 16 ? 2050d : 16777218d), Real.FromDouble(0.5d));
            Assert.Equal(RealRepresentation.StorageBits == 16 ? (RealRepresentation.ArithmeticBits == 16 ? 2048d : 2049d) : RealRepresentation.ArithmeticBits == 32 ? 16777216d : 16777217d, lerped.ToDouble());
            RealEvaluation root = RealMath.Sqrt(Real.FromDouble(2d));
            Assert.Equal(RealRepresentation.ArithmeticBits == 16 ? (double)Half.Sqrt((Half)2) : RealRepresentation.ArithmeticBits == 32 ? (double)MathF.Sqrt(2f) : Math.Sqrt(2d), root.ToDouble());
        }

        /// <summary>Both operand orders keep mixed stored/evaluation expressions unambiguous and unnarrowed.</summary>
        [Fact]
        public void MixedOperandsRetainEvaluationRepresentation() {
            Real two = Real.FromDouble(2d);
            RealEvaluation four = two + two;
            Assert.Equal(6d, (four + two).ToDouble());
            Assert.Equal(6d, (two + four).ToDouble());
            Assert.Equal(2d, (four - two).ToDouble());
            Assert.Equal(-2d, (two - four).ToDouble());
            Assert.Equal(8d, (four * two).ToDouble());
            Assert.Equal(8d, (two * four).ToDouble());
            Assert.Equal(2d, (four / two).ToDouble());
            Assert.Equal(0.5d, (two / four).ToDouble());
        }

        /// <summary>A stored intermediate narrows separately from the surrounding arithmetic expression.</summary>
        [Fact]
        public void IntermediateStoreCanChangeMixedPrecisionResult() {
            Real large = Real.FromDouble(RealRepresentation.StorageBits == 16 ? 2048d : 16777216d);
            Real one = Real.FromDouble(1d);
            RealEvaluation transient = large + one;
            Real stored = transient;
            double expectedTransient = RealRepresentation.StorageBits == 16 ? (RealRepresentation.ArithmeticBits == 16 ? 2048d : 2049d) : RealRepresentation.ArithmeticBits == 64 ? 16777217d : 16777216d;
            double expectedStored = RealRepresentation.StorageBits == 16 ? (double)(Half)expectedTransient : RealRepresentation.StorageBits == 32 ? (double)(float)expectedTransient : expectedTransient;
            Assert.Equal(expectedTransient, transient.ToDouble());
            Assert.Equal(expectedStored, stored.ToDouble());
        }

        /// <summary>Each cook request owns independent precision without mutating another request's evaluator.</summary>
        [Fact]
        public void CookEvaluatorsRemainIndependent() {
            NumericArithmetic single = new(new NumericPrecisionPolicy(32, 32));
            NumericArithmetic wide = new(new NumericPrecisionPolicy(64, 64));
            Assert.Equal(0d, single.Subtract(single.Add(16777216d, 1d), 16777216d));
            Assert.Equal(1d, wide.Subtract(wide.Add(16777216d, 1d), 16777216d));
            Assert.Equal(0d, single.Subtract(single.Add(16777216d, 1d), 16777216d));
        }

        /// <summary>Cook arithmetic and compiled managed operations agree for the active build policy.</summary>
        [Fact]
        public void CookEvaluationMatchesCompiledArithmetic() {
            NumericArithmetic arithmetic = new(RealRepresentation.Policy);
            Assert.Equal((Real.FromDouble(1d) / Real.FromDouble(10d)).ToDouble(), arithmetic.Divide(1d, 10d));
            Assert.Equal(RealMath.Sqrt(Real.FromDouble(2d)).ToDouble(), arithmetic.Sqrt(2d));
            Assert.Equal(RealMath.Sin(Real.FromDouble(0.5d)).ToDouble(), arithmetic.Sin(0.5d));
            Assert.Equal(RealMath.Cos(Real.FromDouble(0.5d)).ToDouble(), arithmetic.Cos(0.5d));
            Assert.Equal(RealMath.Atan2(Real.FromDouble(1d), Real.FromDouble(2d)).ToDouble(), arithmetic.Atan2(1d, 2d));
            Assert.Equal(RealMath.Remainder(Real.FromDouble(5.5d), Real.FromDouble(2d)).ToDouble(), arithmetic.Remainder(5.5d, 2d));
        }

        /// <summary>Unimplemented half policies cannot silently evaluate with a wider backend.</summary>
        [Theory]
        [InlineData(16, 64)] [InlineData(64, 16)] [InlineData(32, 16)]
        public void UnsupportedHalfPolicyFailsExplicitly(int storageBits, int arithmeticBits) {
            Assert.Throws<NotSupportedException>(() => new NumericArithmetic(new NumericPrecisionPolicy(storageBits, arithmeticBits)));
        }
        /// <summary>Clamp evaluates selected-width operands and rejects an inverted interval.</summary>
        [Fact]
        public void ClampUsesSelectedArithmetic() {
            Assert.Equal(2d, RealMath.Clamp(3d, 1d, 2d).ToDouble());
            Assert.Equal(1d, RealMath.Clamp(-3d, 1d, 2d).ToDouble());
            Assert.Equal(1.5d, RealMath.Clamp(1.5d, 1d, 2d).ToDouble());
            Assert.True(double.IsNaN(RealMath.Clamp(double.NaN, 1d, 2d).ToDouble()));
            Assert.Throws<ArgumentException>(() => RealMath.Clamp(1d, 3d, 2d));
        }
    }
}
