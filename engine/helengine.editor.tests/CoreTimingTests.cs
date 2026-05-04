using helengine;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies core update timing state and default elapsed-frame handling.
    /// </summary>
    public class CoreTimingTests {
        /// <summary>
        /// Ensures explicit elapsed frame time is recorded and accumulated by the core.
        /// </summary>
        [Fact]
        public void Update_WithExplicitElapsedSeconds_RecordsFrameDeltaAndAccumulatesTotalTime() {
            Core core = CreateCore();

            core.Update(0.25d);
            core.Update(0.5d);

            Assert.Equal(0.5d, core.FrameDeltaSeconds, 10);
            Assert.Equal(0.75d, core.TotalElapsedSeconds, 10);
        }

        /// <summary>
        /// Ensures one attached physics runtime receives fixed simulation steps derived from the host frame delta.
        /// </summary>
        [Fact]
        public void Update_WithAttachedPhysicsRuntime_AdvancesUsingFixedStepScheduler() {
            Core core = CreateCore(new CoreInitializationOptions {
                PhysicsFixedStepSeconds = 1.0d / 60.0d
            });
            TestPhysicsRuntime runtime = new TestPhysicsRuntime();
            core.AttachPhysicsRuntime(runtime);

            core.Update(1.0d / 30.0d);

            Assert.Equal(2, runtime.StepCount);
            Assert.Equal(1.0d / 60.0d, runtime.LastStepSeconds, 10);
            Assert.Equal(0d, core.PhysicsScheduler.AccumulatedSeconds, 10);
        }

        /// <summary>
        /// Ensures the parameterless update path uses the configured default frame delta.
        /// </summary>
        [Fact]
        public void Update_WithoutExplicitElapsedSeconds_UsesConfiguredDefaultDelta() {
            Core core = CreateCore(new CoreInitializationOptions {
                DefaultUpdateDeltaSeconds = 1.0d / 30.0d
            });

            core.Update();

            Assert.Equal(1.0d / 30.0d, core.FrameDeltaSeconds, 10);
            Assert.Equal(1.0d / 30.0d, core.TotalElapsedSeconds, 10);
        }

        /// <summary>
        /// Ensures negative elapsed frame times are rejected instead of silently corrupting timing state.
        /// </summary>
        [Fact]
        public void Update_WithNegativeElapsedSeconds_ThrowsArgumentOutOfRangeException() {
            Core core = CreateCore();

            Assert.Throws<ArgumentOutOfRangeException>(() => core.Update(-0.01d));
        }

        /// <summary>
        /// Creates and initializes one core instance for timing tests.
        /// </summary>
        /// <param name="options">Initialization options to apply.</param>
        /// <returns>Initialized core instance ready for update calls.</returns>
        Core CreateCore(CoreInitializationOptions options = null) {
            CoreInitializationOptions resolvedOptions = options ?? new CoreInitializationOptions();
            Core core = new Core(resolvedOptions);
            core.Initialize(null, new TestRenderManager2D(), new TestInputBackend(), resolvedOptions);
            return core;
        }
    }
}
