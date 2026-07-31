namespace helengine.helphysics.tests {
    /// <summary>
    /// Verifies the Core-facing HelPhysics adapter preserves exact fixed-step, synchronization, and profiler behavior.
    /// </summary>
    public sealed class HelPhysicsSceneRuntime3DTests {
        /// <summary>
        /// Ensures a Core step that differs from the world configuration is rejected before simulation state changes.
        /// </summary>
        [Fact]
        public void Step_WithDifferentFixedStep_RejectsBeforeSimulation() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D();
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(settings);
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(
                new float3(0f, 10f, 0f),
                float3.One,
                BodyKind3D.Dynamic);
            binder.BindHierarchy(entity);
            HelPhysicsSceneRuntime3D runtime = new HelPhysicsSceneRuntime3D(binder);

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => runtime.Step(1d / 60d));

            Assert.Contains("fixed-step settings do not match", exception.Message, StringComparison.Ordinal);
            Assert.Equal(0, binder.World.LastStepMetrics.BodyCount);
            Assert.Equal(10f, entity.Position.Y);
        }

        /// <summary>
        /// Ensures an exact Core step delegates through the binder so dynamic simulation output reaches the entity.
        /// </summary>
        [Fact]
        public void Step_WithConfiguredFixedStep_AdvancesBinderAndSynchronizesDynamicEntity() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D();
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(settings);
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(
                new float3(0f, 10f, 0f),
                float3.One,
                BodyKind3D.Dynamic);
            binder.BindHierarchy(entity);
            HelPhysicsSceneRuntime3D runtime = new HelPhysicsSceneRuntime3D(binder);

            runtime.Step(settings.FixedStepSeconds);

            Assert.Equal(1, binder.World.LastStepMetrics.BodyCount);
            Assert.True(entity.Position.Y < 10f);
        }

        /// <summary>
        /// Ensures the adapter exposes the exact binder supplied at construction for lifecycle inspection.
        /// </summary>
        [Fact]
        public void Constructor_WithBinder_ExposesSameBinder() {
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(new HelPhysicsWorldSettings3D());

            HelPhysicsSceneRuntime3D runtime = new HelPhysicsSceneRuntime3D(binder);

            Assert.Same(binder, runtime.Binder);
        }

        /// <summary>
        /// Ensures profiler queries are forwarded to the binder-owned world without replacing its reusable metrics sample.
        /// </summary>
        [Fact]
        public void TryGetRuntimeProfilerMetrics_AfterStep_ForwardsWorldMetrics() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D();
            HelPhysicsSceneBinder3D binder = HelPhysicsRuntimeFactory3D.Create(settings);
            Entity entity = HelPhysicsTestSceneFactory3D.CreateBoxEntity(
                float3.Zero,
                float3.One,
                BodyKind3D.Static);
            binder.BindHierarchy(entity);
            HelPhysicsSceneRuntime3D runtime = new HelPhysicsSceneRuntime3D(binder);
            runtime.Step(settings.FixedStepSeconds);

            bool available = runtime.TryGetRuntimeProfilerMetrics(out RuntimePhysicsProfilerMetrics metrics);
            binder.World.TryGetRuntimeProfilerMetrics(out RuntimePhysicsProfilerMetrics worldMetrics);

            Assert.True(available);
            Assert.Same(worldMetrics, metrics);
            Assert.Equal(1, metrics.BodyCount);
        }
    }
}
