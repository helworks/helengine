using helengine.baseplatform.Definitions;

namespace helengine.baseplatform.tests.Player {
    /// <summary>
    /// Verifies Play capability validation and the platform-definition default.
    /// </summary>
    public sealed class PlatformPlayerCapabilityTests {
        /// <summary>A supported capability must name its adapter type.</summary>
        [Fact]
        public void Ctor_WhenSupportedWithoutAdapterType_Throws() {
            Assert.Throws<ArgumentException>(() => new PlatformPlayerCapability(true, " ", "ps2-staging", 60d));
        }

        /// <summary>A supported capability must name its content root.</summary>
        [Fact]
        public void Ctor_WhenSupportedWithoutContentRoot_Throws() {
            Assert.Throws<ArgumentException>(() => new PlatformPlayerCapability(true, "a.B", "", 60d));
        }

        /// <summary>A supported capability must declare a positive, finite target frame rate.</summary>
        /// <param name="targetFrameRate">Invalid frame rate passed to the constructor.</param>
        [Theory]
        [InlineData(0d)]
        [InlineData(-60d)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void Ctor_WhenSupportedWithoutPositiveTargetFrameRate_Throws(double targetFrameRate) {
            ArgumentException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => new PlatformPlayerCapability(true, "a.B", "staging", targetFrameRate));
            Assert.Equal("targetFrameRate", exception.ParamName);
        }

        /// <summary>A supported capability exposes the target frame rate it was given.</summary>
        [Fact]
        public void Ctor_WhenSupported_ExposesTargetFrameRate() {
            Assert.Equal(59.94d, new PlatformPlayerCapability(true, "a.B", "staging", 59.94d).TargetFrameRate);
        }

        /// <summary>The unsupported capability does not support Play.</summary>
        [Fact]
        public void CreateUnsupported_DoesNotSupportPlay() {
            Assert.False(PlatformPlayerCapability.CreateUnsupported().SupportsPlay);
        }

        /// <summary>The unsupported capability declares no frame rate, because no player ever runs for it.</summary>
        [Fact]
        public void CreateUnsupported_DeclaresZeroTargetFrameRate() {
            Assert.Equal(0d, PlatformPlayerCapability.CreateUnsupported().TargetFrameRate);
        }
    }
}
