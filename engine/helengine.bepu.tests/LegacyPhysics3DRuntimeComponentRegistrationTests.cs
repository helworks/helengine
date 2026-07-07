namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies the legacy 3D physics registration entrypoint forwards to the BEPU-backed runtime during migration.
    /// </summary>
    public sealed class LegacyPhysics3DRuntimeComponentRegistrationTests {
        /// <summary>
        /// Ensures the legacy registration hook defers BEPU-backed runtime attachment until one supported physics scene loads.
        /// </summary>
        [Fact]
        public void Register_WhenCalledThroughLegacyEntryPoint_DoesNotAttachBepuPhysicsRuntimeUntilPhysicsSceneLoads() {
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));

            Physics3DRuntimeComponentRegistration.Register(core);

            Assert.Null(core.PhysicsRuntime);
        }
    }
}

