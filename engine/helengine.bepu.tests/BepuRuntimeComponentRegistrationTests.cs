namespace helengine.bepu.tests {
    /// <summary>
    /// Verifies the BEPU-backed runtime registration path attaches the expected physics runtime.
    /// </summary>
    public sealed class BepuRuntimeComponentRegistrationTests {
        /// <summary>
        /// Ensures registration attaches the BEPU-backed physics runtime to the core.
        /// </summary>
        [Fact]
        public void Register_WhenCalled_AttachesBepuPhysicsRuntime() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));

            BepuRuntimeComponentRegistration.Register(core);

            Assert.IsType<BepuPhysicsWorld3D>(core.PhysicsRuntime);
        }
    }
}
