using helengine.baseplatform.Definitions;

namespace helengine.baseplatform.tests.Player {
    /// <summary>
    /// Verifies Play capability validation and the platform-definition default.
    /// </summary>
    public sealed class PlatformPlayerCapabilityTests {
        /// <summary>A supported capability must name its adapter type.</summary>
        [Fact]
        public void Ctor_WhenSupportedWithoutAdapterType_Throws() {
            Assert.Throws<ArgumentException>(() => new PlatformPlayerCapability(true, " ", "ps2-staging"));
        }

        /// <summary>A supported capability must name its content root.</summary>
        [Fact]
        public void Ctor_WhenSupportedWithoutContentRoot_Throws() {
            Assert.Throws<ArgumentException>(() => new PlatformPlayerCapability(true, "a.B", ""));
        }

        /// <summary>The unsupported capability does not support Play.</summary>
        [Fact]
        public void CreateUnsupported_DoesNotSupportPlay() {
            Assert.False(PlatformPlayerCapability.CreateUnsupported().SupportsPlay);
        }
    }
}
