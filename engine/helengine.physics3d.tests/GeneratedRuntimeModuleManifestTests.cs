using System.Reflection;

namespace helengine.physics3d.tests {
    /// <summary>
    /// Verifies the 3D physics runtime assembly exposes one generated runtime module manifest that activates when authored physics scene types are present.
    /// </summary>
    public sealed class GeneratedRuntimeModuleManifestTests {
        /// <summary>
        /// Ensures the 3D physics runtime assembly declares one generated runtime module manifest with the expected registration entrypoint and activation types.
        /// </summary>
        [Fact]
        public void Physics3DAssembly_DeclaresGeneratedRuntimeModuleManifest() {
            GeneratedRuntimeModuleManifestAttribute manifest = Assert.Single(
                typeof(PhysicsSceneFeatureAnalyzer3D)
                    .Assembly
                    .GetCustomAttributes<GeneratedRuntimeModuleManifestAttribute>());

            Assert.Equal("physics3d-legacy-runtime-module", manifest.ModuleId);
            Assert.Equal(typeof(BepuRuntimeComponentRegistration), manifest.RegistrationType);
            Assert.Equal(nameof(BepuRuntimeComponentRegistration.Register), manifest.RegistrationMethodName);
            Assert.Contains(typeof(BepuPhysicsWorld3D), manifest.ActivationTypes);
            Assert.DoesNotContain(typeof(RigidBody3DComponent), manifest.ActivationTypes);
        }
    }
}
