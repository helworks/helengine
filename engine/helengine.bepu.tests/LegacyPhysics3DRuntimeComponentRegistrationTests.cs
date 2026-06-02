namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies the legacy 3D physics registration entrypoint forwards to the BEPU-backed runtime during migration.
    /// </summary>
    public sealed class LegacyPhysics3DRuntimeComponentRegistrationTests {
        /// <summary>
        /// Ensures the legacy registration hook attaches the BEPU-backed runtime instead of the retired custom world.
        /// </summary>
        [Fact]
        public void Register_WhenCalledThroughLegacyEntryPoint_AttachesBepuPhysicsRuntime() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));

            Physics3DRuntimeComponentRegistration.Register(core);

            Assert.IsType<BepuPhysicsWorld3D>(core.PhysicsRuntime);
        }
    }
}
