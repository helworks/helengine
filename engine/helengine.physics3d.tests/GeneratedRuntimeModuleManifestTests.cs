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
                typeof(Physics3DRuntimeComponentRegistration)
                    .Assembly
                    .GetCustomAttributes<GeneratedRuntimeModuleManifestAttribute>());

            Assert.Equal("physics3d-runtime-module", manifest.ModuleId);
            Assert.Equal(typeof(Physics3DRuntimeComponentRegistration), manifest.RegistrationType);
            Assert.Equal(nameof(Physics3DRuntimeComponentRegistration.Register), manifest.RegistrationMethodName);
            Assert.Contains(typeof(RigidBody3DComponent), manifest.ActivationTypes);
            Assert.Contains(typeof(BoxCollider3DComponent), manifest.ActivationTypes);
            Assert.Contains(typeof(SphereCollider3DComponent), manifest.ActivationTypes);
            Assert.Contains(typeof(CapsuleCollider3DComponent), manifest.ActivationTypes);
            Assert.Contains(typeof(StaticMeshCollider3DComponent), manifest.ActivationTypes);
            Assert.Contains(typeof(KinematicMotion3DComponent), manifest.ActivationTypes);
            Assert.Contains(typeof(CharacterController3DComponent), manifest.ActivationTypes);
        }
    }
}
