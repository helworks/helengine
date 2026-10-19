namespace helengine.editor.tests.testing {
    /// <summary>Checks that assertion transport never weakens tolerances or rounds unrelated double values through Real.</summary>
    public sealed class NumericAssertTests {
        /// <summary>Configured numeric comparisons retain the caller's requested precision.</summary>
        [Fact]
        public void EngineComparisonDoesNotRelaxTolerance() {
            Assert.Throws<Xunit.Sdk.EqualException>(() => NumericAssert.Equal(1d, Real.FromDouble(1.01d), 8));
        }

        /// <summary>Existing fixed double comparisons keep distinctions much smaller than a binary32 ULP.</summary>
        [Fact]
        public void FixedDoubleComparisonRemainsFixed() {
            Assert.Throws<Xunit.Sdk.EqualException>(() => NumericAssert.Equal(1.0000000001d, 1.0000000002d, 12));
        }

        /// <summary>Integral range checks remain exact above the double mantissa's integer precision.</summary>
        [Fact]
        public void IntegerRangeRemainsExact() {
            Assert.Throws<Xunit.Sdk.InRangeException>(() => NumericAssert.InRange(9007199254740993L, 9007199254740992L, 9007199254740992L));
        }
    }
}
