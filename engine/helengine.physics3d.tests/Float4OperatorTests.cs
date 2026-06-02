namespace helengine {
    /// <summary>
    /// Verifies the operator surface exposed by <see cref="float4"/> for quaternion and generated native math usage.
    /// </summary>
    public sealed class Float4OperatorTests {
        /// <summary>
        /// Ensures unary negation returns a four-component value with each component sign-inverted.
        /// </summary>
        [Fact]
        public void UnaryNegation_ReturnsComponentWiseNegatedValue() {
            float4 value = new float4(1.5f, -2.0f, 3.25f, -4.5f);

            float4 result = -value;

            Assert.Equal(-1.5f, result.X);
            Assert.Equal(2.0f, result.Y);
            Assert.Equal(-3.25f, result.Z);
            Assert.Equal(4.5f, result.W);
        }

        /// <summary>
        /// Ensures the arithmetic operator surface used by generated BEPU code behaves component-wise.
        /// </summary>
        [Fact]
        public void ArithmeticOperators_ReturnComponentWiseValues() {
            float4 left = new float4(8.0f, -6.0f, 12.0f, -9.0f);
            float4 right = new float4(2.0f, 3.0f, -4.0f, -3.0f);

            float4 sum = left + right;
            float4 difference = left - right;
            float4 product = left * right;
            float4 quotient = left / right;

            Assert.Equal(10.0f, sum.X);
            Assert.Equal(-3.0f, sum.Y);
            Assert.Equal(8.0f, sum.Z);
            Assert.Equal(-12.0f, sum.W);

            Assert.Equal(6.0f, difference.X);
            Assert.Equal(-9.0f, difference.Y);
            Assert.Equal(16.0f, difference.Z);
            Assert.Equal(-6.0f, difference.W);

            Assert.Equal(16.0f, product.X);
            Assert.Equal(-18.0f, product.Y);
            Assert.Equal(-48.0f, product.Z);
            Assert.Equal(27.0f, product.W);

            Assert.Equal(4.0f, quotient.X);
            Assert.Equal(-2.0f, quotient.Y);
            Assert.Equal(-3.0f, quotient.Z);
            Assert.Equal(3.0f, quotient.W);
        }

        /// <summary>
        /// Ensures the zero helper required by generated wide-lane code produces an all-zero value.
        /// </summary>
        [Fact]
        public void Zero_ReturnsAllZeroComponents() {
            float4 zero = float4.Zero;

            Assert.Equal(0.0f, zero.X);
            Assert.Equal(0.0f, zero.Y);
            Assert.Equal(0.0f, zero.Z);
            Assert.Equal(0.0f, zero.W);
        }

        /// <summary>
        /// Ensures component-wise maximum selection matches the generated BEPU math expectations.
        /// </summary>
        [Fact]
        public void Max_ReturnsComponentWiseMaximum() {
            float4 left = new float4(1.0f, 6.0f, -2.0f, 5.0f);
            float4 right = new float4(4.0f, 3.0f, 8.0f, -1.0f);

            float4 result = float4.Max(left, right);

            Assert.Equal(4.0f, result.X);
            Assert.Equal(6.0f, result.Y);
            Assert.Equal(8.0f, result.Z);
            Assert.Equal(5.0f, result.W);
        }

        /// <summary>
        /// Ensures generated matrix helpers can promote one three-component vector plus a scalar into one four-component value.
        /// </summary>
        [Fact]
        public void Constructor_WithFloat3AndScalar_PopulatesAllComponents() {
            float4 result = new float4(new float3(1.0f, -2.5f, 3.75f), 4.5f);

            Assert.Equal(1.0f, result.X);
            Assert.Equal(-2.5f, result.Y);
            Assert.Equal(3.75f, result.Z);
            Assert.Equal(4.5f, result.W);
        }

        /// <summary>
        /// Ensures the generated matrix code can compute four-component dot products through the remapped engine numerics surface.
        /// </summary>
        [Fact]
        public void Dot_ReturnsFourComponentDotProduct() {
            float4 left = new float4(1.0f, 2.0f, 3.0f, 4.0f);
            float4 right = new float4(-5.0f, 6.0f, -7.0f, 8.0f);

            float result = float4.Dot(left, right);

            Assert.Equal(18.0f, result);
        }

        /// <summary>
        /// Ensures generated gameplay code can build quaternions from one axis-angle pair without requiring a ref argument on the axis input.
        /// </summary>
        [Fact]
        public void CreateFromAxisAngle_WithoutRefAxis_ProducesExpectedQuaternion() {
            float3 axis = float3.UnitZ;

            float4.CreateFromAxisAngle(axis, (float)(Math.PI * 0.5d), out float4 result);

            Assert.Equal(0.0f, result.X, 5);
            Assert.Equal(0.0f, result.Y, 5);
            Assert.Equal(0.70710677f, result.Z, 5);
            Assert.Equal(0.70710677f, result.W, 5);
        }
    }
}
