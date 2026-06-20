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

        /// <summary>
        /// Ensures shared runtime registration ignores platform identity and keeps the default solve schedule.
        /// </summary>
        [Fact]
        public void Register_WhenPlatformIsNintendoDs_AttachesDefaultSolveScheduleWorld() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new PlatformInfo("DS", "test-version"));

            BepuRuntimeComponentRegistration.Register(core);

            BepuPhysicsWorld3D world = Assert.IsType<BepuPhysicsWorld3D>(core.PhysicsRuntime);
            Assert.Equal(4, world.SolveVelocityIterationCount);
            Assert.Equal(1, world.SolveSubstepCount);
        }

        /// <summary>
        /// Ensures non-DS runtimes keep the existing default BEPU solve schedule.
        /// </summary>
        [Fact]
        public void Register_WhenPlatformIsNotNintendoDs_KeepsDefaultSolveSchedule() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = AppContext.BaseDirectory
            });
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"));

            BepuRuntimeComponentRegistration.Register(core);

            BepuPhysicsWorld3D world = Assert.IsType<BepuPhysicsWorld3D>(core.PhysicsRuntime);
            Assert.Equal(4, world.SolveVelocityIterationCount);
            Assert.Equal(1, world.SolveSubstepCount);
        }
    }
}
