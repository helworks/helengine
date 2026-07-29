namespace helengine.physics3d.tests {
    /// <summary>
    /// Verifies that the core exposes deterministic runtime profiler counters without depending on a platform renderer.
    /// </summary>
    public sealed class RuntimeProfilerMetricsTests {
        /// <summary>
        /// Ensures the core publishes one complete profiler snapshot for the current host frame and resets frame-owned counters before the next update begins.
        /// </summary>
        [Fact]
        public void Update_WithProfilerMetricsPhysicsRuntime_PublishesCurrentFrameMetricsAndResetsThemForTheNextFrame() {
            CoreInitializationOptions options = new CoreInitializationOptions {
                PhysicsFixedStepSeconds = 1.0d / 60.0d,
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            };
            Core core = new Core(options);
            core.Initialize(null, null, null, new PlatformInfo("test", "test-version"), options);
            TestProfilerPhysicsRuntime runtime = new TestProfilerPhysicsRuntime(7, 11, 13);
            core.AttachPhysicsRuntime(runtime);

            core.Update(1.0d / 30.0d);
            core.ReportRuntimeProfilerRenderingMetrics(17, 19);

            RuntimeProfilerMetricsSnapshot firstSnapshot = core.RuntimeProfilerMetrics;
            Assert.Equal(2, firstSnapshot.FixedUpdateCount);
            Assert.Equal(7, firstSnapshot.PhysicsBodyCount);
            Assert.Equal(11, firstSnapshot.PhysicsContactCount);
            Assert.Equal(13, firstSnapshot.PhysicsConstraintCount);
            Assert.Equal(17, firstSnapshot.DrawCallCount);
            Assert.Equal(19, firstSnapshot.TriangleCount);

            runtime.SetMetricsUnavailable();
            core.Update(0d);

            RuntimeProfilerMetricsSnapshot secondSnapshot = core.RuntimeProfilerMetrics;
            Assert.Equal(firstSnapshot.FrameNumber + 1, secondSnapshot.FrameNumber);
            Assert.Equal(0, secondSnapshot.FixedUpdateCount);
            Assert.False(secondSnapshot.HasPhysicsBodyCount);
            Assert.False(secondSnapshot.HasPhysicsContactCount);
            Assert.False(secondSnapshot.HasPhysicsConstraintCount);
            Assert.False(secondSnapshot.HasDrawCallCount);
            Assert.False(secondSnapshot.HasTriangleCount);
        }

        /// <summary>
        /// Ensures scene-operation counters belong only to the frame in which core records the safe-point commit.
        /// </summary>
        [Fact]
        public void BeginFrame_AfterSceneOperationsWereRecorded_ClearsTheSceneOperationCount() {
            RuntimeProfilerMetrics metrics = new RuntimeProfilerMetrics();

            metrics.BeginFrame();
            metrics.AddSceneOperationCount(3);
            Assert.Equal(3, metrics.GetSnapshot().SceneOperationCount);

            metrics.BeginFrame();

            RuntimeProfilerMetricsSnapshot snapshot = metrics.GetSnapshot();
            Assert.Equal(0, snapshot.SceneOperationCount);
            Assert.Equal(2, snapshot.FrameNumber);
        }
    }
}
