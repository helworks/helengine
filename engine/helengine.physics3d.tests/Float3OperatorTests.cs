namespace helengine {
    /// <summary>
    /// Verifies the operator surface exposed by <see cref="float3"/> for physics and generated native math usage.
    /// </summary>
    public sealed class Float3OperatorTests {
        /// <summary>
        /// Ensures unary negation returns a vector with each component sign-inverted.
        /// </summary>
        [Fact]
        public void UnaryNegation_ReturnsComponentWiseNegatedVector() {
            float3 value = new float3(1.5f, -2.0f, 3.25f);

            float3 result = -value;

            Assert.Equal(-1.5f, result.X);
            Assert.Equal(2.0f, result.Y);
            Assert.Equal(-3.25f, result.Z);
        }

        /// <summary>
        /// Ensures vector length helpers return the expected Euclidean magnitude values.
        /// </summary>
        [Fact]
        public void LengthHelpers_ReturnExpectedMagnitudeValues() {
            float3 value = new float3(2.0f, 3.0f, 6.0f);

            float lengthSquared = value.LengthSquared();
            float length = value.Length();

            Assert.Equal(49.0f, lengthSquared);
            Assert.Equal(7.0f, length);
        }

        /// <summary>
        /// Ensures absolute-value projection returns a vector whose components are all non-negative.
        /// </summary>
        [Fact]
        public void Abs_ReturnsComponentWiseAbsoluteValues() {
            float3 value = new float3(-1.5f, 2.0f, -3.25f);

            float3 result = float3.Abs(value);

            Assert.Equal(1.5f, result.X);
            Assert.Equal(2.0f, result.Y);
            Assert.Equal(3.25f, result.Z);
        }

        /// <summary>
        /// Ensures the axis unit-vector properties expose the expected basis directions for generated physics helpers.
        /// </summary>
        [Fact]
        public void UnitAxisProperties_ReturnExpectedBasisVectors() {
            Assert.Equal(new float3(1.0f, 0.0f, 0.0f), float3.UnitX);
            Assert.Equal(new float3(0.0f, 1.0f, 0.0f), float3.UnitY);
            Assert.Equal(new float3(0.0f, 0.0f, 1.0f), float3.UnitZ);
        }
    }
}
