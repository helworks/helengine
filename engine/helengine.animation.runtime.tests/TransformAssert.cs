namespace helengine.animation.runtime.tests {
    /// <summary>
    /// Component-wise assertions for vectors and quaternions with a tolerance of five decimals.
    /// </summary>
    static class TransformAssert {
        /// <summary>
        /// Asserts two vectors match component by component.
        /// </summary>
        /// <param name="expected">Expected vector.</param>
        /// <param name="actual">Actual vector.</param>
        public static void Vector(float3 expected, float3 actual) {
            Assert.Equal((double)expected.X, (double)actual.X, 5);
            Assert.Equal((double)expected.Y, (double)actual.Y, 5);
            Assert.Equal((double)expected.Z, (double)actual.Z, 5);
        }

        /// <summary>
        /// Asserts two quaternions match component by component.
        /// </summary>
        /// <param name="expected">Expected quaternion.</param>
        /// <param name="actual">Actual quaternion.</param>
        public static void Quaternion(float4 expected, float4 actual) {
            Assert.Equal((double)expected.X, (double)actual.X, 5);
            Assert.Equal((double)expected.Y, (double)actual.Y, 5);
            Assert.Equal((double)expected.Z, (double)actual.Z, 5);
            Assert.Equal((double)expected.W, (double)actual.W, 5);
        }
    }
}
