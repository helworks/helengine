namespace helengine.physics3d.tests {
    /// <summary>
    /// Verifies authored sleep settings on rigid-body components.
    /// </summary>
    public sealed class RigidBody3DComponentTests {
        /// <summary>
        /// Confirms a newly created rigid body uses the approved aggressive sleeping defaults.
        /// </summary>
        [Fact]
        public void Constructor_UsesAggressiveSleepingDefaults() {
            RigidBody3DComponent component = new RigidBody3DComponent();

            Assert.Equal(0.5d, component.SleepThreshold);
            Assert.Equal(10, component.SleepTicks);
        }

        /// <summary>
        /// Confirms zero, negative, and non-finite sleep thresholds are rejected.
        /// </summary>
        [Theory]
        [InlineData(0d)]
        [InlineData(-1d)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void SleepThreshold_WhenNotFiniteAndPositive_ThrowsArgumentOutOfRangeException(double value) {
            RigidBody3DComponent component = new RigidBody3DComponent();

            Assert.Throws<ArgumentOutOfRangeException>(() => component.SleepThreshold = value);
        }

        /// <summary>
        /// Confirms zero and negative sleep tick counts are rejected.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void SleepTicks_WhenNotPositive_ThrowsArgumentOutOfRangeException(int value) {
            RigidBody3DComponent component = new RigidBody3DComponent();

            Assert.Throws<ArgumentOutOfRangeException>(() => component.SleepTicks = value);
        }
    }
}
