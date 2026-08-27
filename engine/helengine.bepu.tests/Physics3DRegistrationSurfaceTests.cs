using System.Reflection;

namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies the physics3d assembly does not expose an obsolete forwarding registration type.
    /// </summary>
    public sealed class Physics3DRegistrationSurfaceTests {
        /// <summary>
        /// Ensures runtime registration is owned by the current BEPU registration API rather than a physics3d forwarding facade.
        /// </summary>
        [Fact]
        public void Physics3DAssembly_DoesNotExposeObsoleteForwardingRegistrationType() {
            Type registrationType = Assembly.Load("helengine.physics3d")
                .GetType("helengine.Physics3DRuntimeComponentRegistration", false);

            Assert.Null(registrationType);
        }
    }
}
