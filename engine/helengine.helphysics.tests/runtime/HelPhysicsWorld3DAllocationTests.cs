namespace helengine {
    /// <summary>
    /// Verifies settled stepping, deferred mutation, wake routing, and profiler access remain allocation-free after warmup.
    /// </summary>
    public sealed class HelPhysicsWorld3DAllocationTests {
        /// <summary>
        /// Verifies one thousand settled fixed steps allocate exactly zero managed bytes on the calling thread.
        /// </summary>
        [Fact]
        public void Step_AfterSettledWarmup_AllocatesZeroBytesAcrossOneThousandSteps() {
            HelPhysicsWorldFixture fixture = HelPhysicsWorldFixture.CreateFourBoxStack();
            StepWorld(fixture.World, 200);
            StepWorld(fixture.World, 16);
            ForceCollection();

            long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int stepIndex = 0; stepIndex < 1000; stepIndex++) {
                fixture.World.Step(HelPhysicsWorldFixture.StepSeconds);
            }
            long bytesAfter = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(bytesBefore, bytesAfter);
            for (int boxIndex = 0; boxIndex < fixture.DynamicBoxes.Length; boxIndex++) {
                Assert.False(fixture.World.GetBodySnapshot(fixture.DynamicBoxes[boxIndex]).IsAwake);
            }
        }

        /// <summary>
        /// Verifies one thousand force and impulse enqueues use only constructor-owned command storage.
        /// </summary>
        [Fact]
        public void DeferredForceAndImpulseCommands_AfterWarmup_AllocateZeroBytes() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D(
                1,
                1,
                1,
                1,
                4,
                1,
                1024,
                4,
                1,
                HelPhysicsWorldFixture.StepSeconds,
                PhysicsVector3.Zero);
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(settings);
            HelPhysicsBodyHandle3D handle = world.CreateBody(CreateDynamicDescription());
            world.Step(settings.FixedStepSeconds);
            StepWorld(world, 5);
            ForceCollection();
            PhysicsVector3 tinyInput = new PhysicsVector3(0f, 0.00001f, 0f);

            long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int commandIndex = 0; commandIndex < 1000; commandIndex++) {
                if ((commandIndex & 1) == 0) {
                    world.ApplyForce(handle, tinyInput);
                } else {
                    world.ApplyImpulse(handle, tinyInput);
                }
            }
            long bytesAfter = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(bytesBefore, bytesAfter);
            Assert.False(world.GetBodySnapshot(handle).IsAwake);
            world.Step(settings.FixedStepSeconds);
            Assert.True(world.GetBodySnapshot(handle).IsAwake);
            Assert.Equal(1, world.LastStepMetrics.ExplicitForceWakeCount);
        }

        /// <summary>
        /// Verifies repeated profiler reads reuse one world-owned metrics object and allocate no managed bytes.
        /// </summary>
        [Fact]
        public void TryGetRuntimeProfilerMetrics_AfterWarmup_AllocatesZeroBytesAndReusesSample() {
            HelPhysicsWorldFixture fixture = HelPhysicsWorldFixture.CreateFourBoxStack();
            fixture.World.Step(HelPhysicsWorldFixture.StepSeconds);
            Assert.True(fixture.World.TryGetRuntimeProfilerMetrics(out RuntimePhysicsProfilerMetrics warmedMetrics));
            ForceCollection();
            RuntimePhysicsProfilerMetrics lastMetrics = null;
            bool lastResult = false;

            long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int readIndex = 0; readIndex < 1000; readIndex++) {
                lastResult = fixture.World.TryGetRuntimeProfilerMetrics(out lastMetrics);
            }
            long bytesAfter = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(bytesBefore, bytesAfter);
            Assert.True(lastResult);
            Assert.Same(warmedMetrics, lastMetrics);
            Assert.Equal(fixture.World.LastStepMetrics.BodyCount, lastMetrics.BodyCount);
            Assert.Equal(fixture.World.LastStepMetrics.ContactPointCount, lastMetrics.ContactCount);
            Assert.Equal(fixture.World.LastStepMetrics.ManifoldCount, lastMetrics.ConstraintCount);
        }

        /// <summary>
        /// Creates one complete isolated dynamic description for command allocation coverage.
        /// </summary>
        /// <returns>An initially awake zero-gravity unit box with aggressive sleep settings.</returns>
        static HelPhysicsBodyDescription3D CreateDynamicDescription() {
            return new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(0.5f, 0.5f, 0.5f)),
                BodyKind3D.Dynamic,
                PhysicsVector3.Zero,
                PhysicsQuaternion.Identity,
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                PhysicsScalar.One,
                HelPhysicsWorldFixture.CreateStackMaterial(),
                1,
                ushort.MaxValue,
                1,
                PhysicsScalar.One,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.FromFloat(0.2f),
                PhysicsScalar.FromFloat(0.2f),
                5,
                true);
        }

        /// <summary>
        /// Advances one world by a fixed number of its configured steps without test-framework work inside the loop.
        /// </summary>
        /// <param name="world">World to advance.</param>
        /// <param name="stepCount">Non-negative number of fixed steps.</param>
        static void StepWorld(HelPhysicsWorld3D world, int stepCount) {
            for (int stepIndex = 0; stepIndex < stepCount; stepIndex++) {
                world.Step(world.Settings.FixedStepSeconds);
            }
        }

        /// <summary>
        /// Forces pending managed collections to complete before a measured current-thread allocation interval.
        /// </summary>
        static void ForceCollection() {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
