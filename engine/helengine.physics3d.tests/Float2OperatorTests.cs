namespace helengine {
    /// <summary>
    /// Verifies the <see cref="float2"/> math surface required by BEPU-authored hull helpers after native type remapping.
    /// </summary>
    public sealed class Float2OperatorTests {
        /// <summary>
        /// Ensures unary negation and vector addition/subtraction produce component-wise results.
        /// </summary>
        [Fact]
        public void Operators_ReturnExpectedComponentWiseResults() {
            float2 value = new float2(3.5f, -2.0f);
            float2 other = new float2(-1.5f, 4.0f);

            float2 negated = -value;
            float2 sum = value + other;
            float2 difference = value - other;

            Assert.Equal(-3.5f, negated.X);
            Assert.Equal(2.0f, negated.Y);
            Assert.Equal(2.0f, sum.X);
            Assert.Equal(2.0f, sum.Y);
            Assert.Equal(5.0f, difference.X);
            Assert.Equal(-6.0f, difference.Y);
        }

        /// <summary>
        /// Ensures vector-length, dot-product, and normalization helpers match Euclidean expectations.
        /// </summary>
        [Fact]
        public void LengthDotAndNormalize_ReturnExpectedValues() {
            float2 value = new float2(3.0f, 4.0f);
            float2 other = new float2(-2.0f, 5.0f);

            float lengthSquared = value.LengthSquared();
            float length = value.Length();
            float dot = float2.Dot(value, other);
            float2 normalized = float2.Normalize(value);

            Assert.Equal(25.0f, lengthSquared);
            Assert.Equal(5.0f, length);
            Assert.Equal(14.0f, dot);
            Assert.Equal(0.6f, normalized.X, 5);
            Assert.Equal(0.8f, normalized.Y, 5);
        }

        /// <summary>
        /// Ensures the BEPU-required float3 and float4 component-wise square root helpers and float4 one vector are available through the remapped engine numerics surface.
        /// </summary>
        [Fact]
        public void Float3AndFloat4Helpers_ReturnExpectedComponentWiseResults() {
            float3 squareRoot3 = float3.SquareRoot(new float3(4.0f, 9.0f, 16.0f));
            float4 squareRoot4 = float4.SquareRoot(new float4(1.0f, 4.0f, 9.0f, 16.0f));
            float4 one = float4.One;

            Assert.Equal(2.0f, squareRoot3.X);
            Assert.Equal(3.0f, squareRoot3.Y);
            Assert.Equal(4.0f, squareRoot3.Z);
            Assert.Equal(1.0f, squareRoot4.X);
            Assert.Equal(2.0f, squareRoot4.Y);
            Assert.Equal(3.0f, squareRoot4.Z);
            Assert.Equal(4.0f, squareRoot4.W);
            Assert.Equal(1.0f, one.X);
            Assert.Equal(1.0f, one.Y);
            Assert.Equal(1.0f, one.Z);
            Assert.Equal(1.0f, one.W);
        }
    }
}
