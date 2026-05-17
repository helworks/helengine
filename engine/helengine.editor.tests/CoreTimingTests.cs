using helengine;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies core update timing state for explicit and measured update paths.
    /// </summary>
    public class CoreTimingTests {
        /// <summary>
        /// Ensures the first measured update reports zero delta values instead of a startup spike.
        /// </summary>
        [Fact]
        public void Update_WhenCalledFirstTimeWithoutExplicitElapsedSeconds_SetsDeltaPropertiesToZero() {
            TestClockDrivenCore core = CreateClockDrivenCore(1.0d);

            core.Update();

            Assert.Equal(0f, core.DeltaTime);
            Assert.Equal(0f, core.UnscaledDeltaTime);
            Assert.Equal(0d, core.FrameDeltaSeconds, 10);
            Assert.Equal(0d, core.TotalElapsedSeconds, 10);
        }

        /// <summary>
        /// Ensures later measured updates derive delta from elapsed wall-clock time.
        /// </summary>
        [Fact]
        public void Update_WhenCalledAgainWithoutExplicitElapsedSeconds_UsesMeasuredElapsedSeconds() {
            TestClockDrivenCore core = CreateClockDrivenCore(1.0d, 1.05d);

            core.Update();
            core.Update();

            Assert.Equal(0.05f, core.DeltaTime, 3);
            Assert.Equal(core.DeltaTime, core.UnscaledDeltaTime, 6);
            Assert.Equal(0.05d, core.FrameDeltaSeconds, 3);
            Assert.Equal(0.05d, core.TotalElapsedSeconds, 3);
        }

        /// <summary>
        /// Ensures explicit elapsed frame time is recorded and accumulated by the core.
        /// </summary>
        [Fact]
        public void Update_WhenCalledWithExplicitElapsedSeconds_UpdatesDeltaPropertiesAndAccumulatedTime() {
            Core core = CreateCore();

            core.Update(0.25d);
            core.Update(0.5d);

            Assert.Equal(0.5f, core.DeltaTime, 6);
            Assert.Equal(0.5f, core.UnscaledDeltaTime, 6);
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
        /// Ensures update components can read the current core delta values during update execution.
        /// </summary>
        [Fact]
        public void UpdateComponent_WhenRunningInsideCoreUpdate_CanReadCurrentDeltaTime() {
            TestClockDrivenCore core = CreateClockDrivenCore(1.0d, 1.1d);
            Entity entity = new Entity();
            entity.InitComponents();
            TestDeltaTimeProbeComponent component = new TestDeltaTimeProbeComponent();
            entity.AddComponent(component);

            core.Update();
            core.Update();

            Assert.Equal(0.1f, component.LastObservedDeltaTime, 3);
            Assert.Equal(0.1f, component.LastObservedUnscaledDeltaTime, 3);
            Assert.Equal(1, component.ObservedUpdateCount);
        }

        /// <summary>
        /// Ensures core initialization records the supplied platform metadata for runtime consumers.
        /// </summary>
        [Fact]
        public void Initialize_WithPlatformInfo_StoresPlatformInfo() {
            Core core = new Core();

            core.Initialize(null, new TestRenderManager2D(), new TestInputBackend(), TestPlatformInfo.Shared);

            Assert.Same(TestPlatformInfo.Shared, core.PlatformInfo);
        }

        /// <summary>
        /// Ensures core initialization rejects missing platform metadata instead of inventing defaults.
        /// </summary>
        [Fact]
        public void Initialize_WithoutPlatformInfo_ThrowsArgumentNullException() {
            Core core = new Core();

            Assert.Throws<ArgumentNullException>(() => core.Initialize(null, new TestRenderManager2D(), new TestInputBackend(), (PlatformInfo)null));
        }

        /// <summary>
        /// Ensures repeated draw calls continue to use queued draw-duration measurements and update the exposed draw timing state.
        /// </summary>
        [Fact]
        public void Draw_WhenCalledRepeatedly_StoresTheLatestMeasuredDrawDuration() {
            TestClockDrivenCore core = CreateClockDrivenCore();
            core.QueueMeasuredDrawMilliseconds(new[] { 12.5d, 7.25d });

            core.Draw();
            Assert.Equal(12.5d, core.LastRenderManager3DDrawMilliseconds, 10);

            core.Draw();
            Assert.Equal(7.25d, core.LastRenderManager3DDrawMilliseconds, 10);
        }

        /// <summary>
        /// Ensures repeated draw calls continue to expose the latest render-manager draw-call count through the shared core contract.
        /// </summary>
        [Fact]
        public void Draw_WhenCalledRepeatedly_StoresTheLatestDrawCallCount() {
            TestClockDrivenCore core = CreateClockDrivenCore();
            TestRenderManager3D renderManager = Assert.IsType<TestRenderManager3D>(core.RenderManager3D);
            renderManager.QueueDrawCallCounts(new[] { 9, 4 });

            core.Draw();
            Assert.Equal(9, core.LastRenderManager3DDrawCallCount);

            core.Draw();
            Assert.Equal(4, core.LastRenderManager3DDrawCallCount);
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
            core.Initialize(null, new TestRenderManager2D(), new TestInputBackend(), TestPlatformInfo.Shared, resolvedOptions);
            return core;
        }

        /// <summary>
        /// Creates and initializes one deterministic clock-driven core instance for measured-update tests.
        /// </summary>
        /// <param name="measuredUpdateSeconds">Measured update times returned by subsequent parameterless update calls.</param>
        /// <returns>Initialized deterministic core instance.</returns>
        TestClockDrivenCore CreateClockDrivenCore(params double[] measuredUpdateSeconds) {
            if (measuredUpdateSeconds == null) {
                throw new ArgumentNullException(nameof(measuredUpdateSeconds));
            }

            TestClockDrivenCore core = new TestClockDrivenCore(measuredUpdateSeconds);
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), TestPlatformInfo.Shared);
            return core;
        }
    }
}
