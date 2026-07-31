namespace helengine {
    /// <summary>
    /// Verifies the deterministic fixed-capacity box world, deferred mutation semantics, sleeping, metrics, and diagnostics.
    /// </summary>
    public sealed class HelPhysicsWorld3DTests {
        /// <summary>
        /// Verifies the exact console-first defaults, including the twenty-hertz fixed step.
        /// </summary>
        [Fact]
        public void Settings_DefaultConstructor_UsesExactConsoleFirstProfile() {
            HelPhysicsWorldSettings3D settings = new HelPhysicsWorldSettings3D();

            Assert.Equal(32, settings.BodyCapacity);
            Assert.Equal(32, settings.ShapeCapacity);
            Assert.Equal(128, settings.CandidatePairCapacity);
            Assert.Equal(64, settings.ManifoldCapacity);
            Assert.Equal(256, settings.ContactPointCapacity);
            Assert.Equal(32, settings.IslandCapacity);
            Assert.Equal(128, settings.DeferredCommandCapacity);
            Assert.Equal(4, settings.VelocityIterationCount);
            Assert.Equal(1, settings.PenetrationCorrectionPassCount);
            Assert.Equal(1d / 20d, settings.FixedStepSeconds);
        }

        /// <summary>
        /// Verifies every positive setting boundary, fixed-step finiteness, manifold table shape, and island-to-body constraint.
        /// </summary>
        [Fact]
        public void Settings_WithInvalidCapacityStepOrWorkCount_ThrowsBeforeWorldAllocation() {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSettings(bodyCapacity: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSettings(bodyCapacity: 65535));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSettings(shapeCapacity: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSettings(shapeCapacity: 65535));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSettings(candidatePairCapacity: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSettings(manifoldCapacity: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSettings(manifoldCapacity: 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSettings(contactPointCapacity: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSettings(islandCapacity: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSettings(bodyCapacity: 1, islandCapacity: 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSettings(deferredCommandCapacity: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSettings(velocityIterationCount: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSettings(penetrationCorrectionPassCount: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSettings(fixedStepSeconds: 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSettings(fixedStepSeconds: -0.05d));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSettings(fixedStepSeconds: double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSettings(fixedStepSeconds: double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateSettings(fixedStepSeconds: double.Epsilon));
        }

        /// <summary>
        /// Verifies that a complete dynamic description preserves every authored value and derives box inertia from explicit mass.
        /// </summary>
        [Fact]
        public void BodyDescription_WithExplicitDynamicValues_PreservesInputsAndDerivesInertia() {
            HelPhysicsMaterial3D material = new HelPhysicsMaterial3D(
                PhysicsScalar.FromFloat(0.7f),
                PhysicsScalar.FromFloat(0.4f),
                PhysicsScalar.FromFloat(0.25f));
            HelPhysicsBodyDescription3D description = new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(0.5f, 0.5f, 0.5f)),
                BodyKind3D.Dynamic,
                new PhysicsVector3(1f, 2f, 3f),
                PhysicsQuaternion.Identity,
                new PhysicsVector3(4f, 5f, 6f),
                new PhysicsVector3(7f, 8f, 9f),
                PhysicsScalar.FromFloat(2f),
                material,
                17,
                19,
                23,
                PhysicsScalar.FromFloat(1.5f),
                PhysicsScalar.FromFloat(0.1f),
                PhysicsScalar.FromFloat(0.2f),
                PhysicsScalar.FromFloat(0.3f),
                PhysicsScalar.FromFloat(0.4f),
                5,
                true);

            Assert.Equal(BodyKind3D.Dynamic, description.BodyKind);
            Assert.Equal(PhysicsScalar.FromFloat(2f), description.Mass);
            Assert.Equal(PhysicsScalar.FromFloat(0.5f), description.InverseMass);
            Assert.Equal(PhysicsScalar.FromFloat(3f), description.LocalInverseInertia.Row0.X);
            Assert.Equal(PhysicsScalar.FromFloat(3f), description.LocalInverseInertia.Row1.Y);
            Assert.Equal(PhysicsScalar.FromFloat(3f), description.LocalInverseInertia.Row2.Z);
            Assert.Equal(PhysicsScalar.FromFloat(0.3f), description.LinearSleepThreshold);
            Assert.Equal(PhysicsScalar.FromFloat(0.4f), description.AngularSleepThreshold);
            Assert.Equal((ushort)5, description.SleepTicks);
            Assert.Equal(23, description.EntityBindingId);
            Assert.True(description.IsAwake);
        }

        /// <summary>
        /// Verifies unsupported modes, mass combinations, static motion, pose, damping, sleep, and awake combinations are rejected.
        /// </summary>
        [Fact]
        public void BodyDescription_WithInvalidModeMassPoseOrSleepCombination_Throws() {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateDescription(
                BodyKind3D.Dynamic,
                PhysicsScalar.Zero,
                PhysicsVector3.Zero,
                PhysicsQuaternion.Identity,
                PhysicsScalar.One,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.FromFloat(0.2f),
                PhysicsScalar.FromFloat(0.2f),
                5,
                true));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateDescription(
                BodyKind3D.Static,
                PhysicsScalar.One,
                PhysicsVector3.Zero,
                PhysicsQuaternion.Identity,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                1,
                false));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateDescription(
                BodyKind3D.Kinematic,
                PhysicsScalar.One,
                PhysicsVector3.Zero,
                PhysicsQuaternion.Identity,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                1,
                false));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateDescription(
                (BodyKind3D)99,
                PhysicsScalar.Zero,
                PhysicsVector3.Zero,
                PhysicsQuaternion.Identity,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                1,
                false));
            Assert.Throws<ArgumentException>(() => CreateDescription(
                BodyKind3D.Static,
                PhysicsScalar.Zero,
                PhysicsVector3.UnitX,
                PhysicsQuaternion.Identity,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                1,
                false));
            Assert.Throws<ArgumentException>(() => CreateDescription(
                BodyKind3D.Static,
                PhysicsScalar.Zero,
                PhysicsVector3.Zero,
                PhysicsQuaternion.Identity,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                1,
                true));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateDescription(
                BodyKind3D.Dynamic,
                PhysicsScalar.One,
                PhysicsVector3.Zero,
                default,
                PhysicsScalar.One,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.FromFloat(0.2f),
                PhysicsScalar.FromFloat(0.2f),
                5,
                true));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateDescription(
                BodyKind3D.Dynamic,
                PhysicsScalar.One,
                PhysicsVector3.Zero,
                new PhysicsQuaternion(PhysicsScalar.Zero, PhysicsScalar.Zero, PhysicsScalar.Zero, PhysicsScalar.FromFloat(2f)),
                PhysicsScalar.One,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.FromFloat(0.2f),
                PhysicsScalar.FromFloat(0.2f),
                5,
                true));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateDescription(
                BodyKind3D.Dynamic,
                PhysicsScalar.One,
                PhysicsVector3.Zero,
                PhysicsQuaternion.Identity,
                PhysicsScalar.One,
                PhysicsScalar.FromFloat(-0.1f),
                PhysicsScalar.Zero,
                PhysicsScalar.FromFloat(0.2f),
                PhysicsScalar.FromFloat(0.2f),
                5,
                true));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateDescription(
                BodyKind3D.Dynamic,
                PhysicsScalar.One,
                PhysicsVector3.Zero,
                PhysicsQuaternion.Identity,
                PhysicsScalar.One,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.FromFloat(-0.2f),
                PhysicsScalar.FromFloat(0.2f),
                5,
                true));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateDescription(
                BodyKind3D.Dynamic,
                PhysicsScalar.One,
                PhysicsVector3.Zero,
                PhysicsQuaternion.Identity,
                PhysicsScalar.One,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.FromFloat(0.2f),
                PhysicsScalar.FromFloat(0.2f),
                0,
                true));
        }

        /// <summary>
        /// Verifies a dynamic body cannot enter the sleeping set while retaining authored linear motion.
        /// </summary>
        [Fact]
        public void BodyDescription_WithInitiallySleepingDynamicLinearVelocity_Throws() {
            Assert.Throws<ArgumentException>(() => CreateSleepingDynamicDescriptionWithVelocities(
                PhysicsVector3.UnitX,
                PhysicsVector3.Zero));
        }

        /// <summary>
        /// Verifies a dynamic body cannot enter the sleeping set while retaining authored angular motion.
        /// </summary>
        [Fact]
        public void BodyDescription_WithInitiallySleepingDynamicAngularVelocity_Throws() {
            Assert.Throws<ArgumentException>(() => CreateSleepingDynamicDescriptionWithVelocities(
                PhysicsVector3.Zero,
                PhysicsVector3.UnitZ));
        }

        /// <summary>
        /// Verifies a valid zero-motion sleeping body wakes from an impulse without exposing any latent authored velocity.
        /// </summary>
        [Fact]
        public void InitiallySleepingDynamic_AfterImpulse_WakesFromZeroVelocity() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(gravity: PhysicsVector3.Zero));
            HelPhysicsBodyHandle3D handle = world.CreateBody(CreateSleepingDynamicDescriptionWithVelocities(
                PhysicsVector3.Zero,
                PhysicsVector3.Zero));
            world.Step(world.Settings.FixedStepSeconds);

            HelPhysicsBodySnapshot3D sleeping = world.GetBodySnapshot(handle);
            Assert.False(sleeping.IsAwake);
            Assert.Equal(PhysicsVector3.Zero, sleeping.LinearVelocity);
            Assert.Equal(PhysicsVector3.Zero, sleeping.AngularVelocity);

            world.ApplyImpulse(handle, PhysicsVector3.UnitX);
            world.Step(world.Settings.FixedStepSeconds);

            HelPhysicsBodySnapshot3D awake = world.GetBodySnapshot(handle);
            Assert.True(awake.IsAwake);
            Assert.Equal(PhysicsVector3.UnitX, awake.LinearVelocity);
            Assert.Equal(PhysicsVector3.Zero, awake.AngularVelocity);
        }

        /// <summary>
        /// Verifies body creation reserves a stable handle immediately but does not publish the body to simulation until the next step.
        /// </summary>
        [Fact]
        public void CreateBody_BeforeNextStep_ReturnsUnambiguousPendingSnapshot() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(gravity: PhysicsVector3.Zero));
            HelPhysicsBodyHandle3D handle = world.CreateBody(CreateDynamicDescription(PhysicsVector3.Zero, PhysicsVector3.Zero, true, 5));

            HelPhysicsBodySnapshot3D pending = world.GetBodySnapshot(handle);

            Assert.True(pending.IsPending);
            Assert.False(pending.IsActive);
            Assert.Equal(0, world.LastStepMetrics.BodyCount);

            world.Step(world.Settings.FixedStepSeconds);
            HelPhysicsBodySnapshot3D active = world.GetBodySnapshot(handle);

            Assert.False(active.IsPending);
            Assert.True(active.IsActive);
            Assert.Equal(1, world.LastStepMetrics.BodyCount);
        }

        /// <summary>
        /// Verifies all invalid public step values fail before applying a queued creation or changing metrics.
        /// </summary>
        [Fact]
        public void Step_WithInvalidTimestep_RejectsBeforeAnyWorldMutation() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(gravity: PhysicsVector3.Zero));
            HelPhysicsBodyHandle3D handle = world.CreateBody(CreateDynamicDescription(PhysicsVector3.Zero, PhysicsVector3.Zero, true, 5));

            AssertInvalidStepPreservesPendingBody(world, handle, double.NaN);
            AssertInvalidStepPreservesPendingBody(world, handle, double.PositiveInfinity);
            AssertInvalidStepPreservesPendingBody(world, handle, double.NegativeInfinity);
            AssertInvalidStepPreservesPendingBody(world, handle, 0d);
            AssertInvalidStepPreservesPendingBody(world, handle, -world.Settings.FixedStepSeconds);
            AssertInvalidStepPreservesPendingBody(world, handle, world.Settings.FixedStepSeconds * 2d);

            world.Step(world.Settings.FixedStepSeconds);

            Assert.True(world.GetBodySnapshot(handle).IsActive);
            Assert.Equal(1, world.LastStepMetrics.BodyCount);
        }

        /// <summary>
        /// Verifies queued creation, impulse, and force commands execute in insertion order at the next step boundary.
        /// </summary>
        [Fact]
        public void DeferredCommands_CreateThenImpulseThenForce_ApplyInStableNextStepOrder() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(gravity: PhysicsVector3.Zero));
            HelPhysicsBodyHandle3D handle = world.CreateBody(CreateDynamicDescription(PhysicsVector3.Zero, PhysicsVector3.Zero, true, 20));
            world.ApplyImpulse(handle, PhysicsVector3.UnitX);
            world.ApplyForce(handle, PhysicsVector3.UnitX * PhysicsScalar.FromFloat(2f));

            Assert.Equal(PhysicsScalar.Zero, world.GetBodySnapshot(handle).LinearVelocity.X);

            world.Step(world.Settings.FixedStepSeconds);

            HelPhysicsBodySnapshot3D snapshot = world.GetBodySnapshot(handle);
            AssertScalarClose(1.1f, snapshot.LinearVelocity.X, 0.0001f);
            AssertScalarClose(0.055f, snapshot.Position.X, 0.0001f);
        }

        /// <summary>
        /// Verifies an overflowing aggregate force is rejected before append so the accepted command executes once without queue replay.
        /// </summary>
        [Fact]
        public void ApplyForce_WithOverflowingDeferredAggregate_RejectsBeforeQueueMutationAndRemainsUsable() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(
                bodyCapacity: 1,
                shapeCapacity: 1,
                islandCapacity: 1,
                gravity: PhysicsVector3.Zero));
            HelPhysicsBodyHandle3D handle = world.CreateBody(CreateDynamicDescription(
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                true,
                20));
            world.Step(world.Settings.FixedStepSeconds);
            PhysicsVector3 maximumForce = new PhysicsVector3(float.MaxValue, 0f, 0f);

            world.ApplyForce(handle, PhysicsVector3.UnitX);
            Assert.Throws<ArgumentOutOfRangeException>(() => world.ApplyForce(handle, maximumForce));
            Assert.Equal(PhysicsScalar.Zero, world.GetBodySnapshot(handle).LinearVelocity.X);

            world.Step(world.Settings.FixedStepSeconds);
            PhysicsScalar velocityAfterAcceptedForce = world.GetBodySnapshot(handle).LinearVelocity.X;
            Assert.Equal(PhysicsScalar.FromFloat(0.05f), velocityAfterAcceptedForce);

            world.Step(world.Settings.FixedStepSeconds);
            Assert.Equal(velocityAfterAcceptedForce, world.GetBodySnapshot(handle).LinearVelocity.X);
            Assert.Equal(1, world.LastStepMetrics.BodyCount);
        }

        /// <summary>
        /// Verifies an overflowing aggregate impulse is rejected before append and cannot replay as a default lifecycle command.
        /// </summary>
        [Fact]
        public void ApplyImpulse_WithOverflowingDeferredAggregate_RejectsBeforeQueueMutationAndRemainsUsable() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(
                bodyCapacity: 1,
                shapeCapacity: 1,
                islandCapacity: 1,
                gravity: PhysicsVector3.Zero));
            HelPhysicsBodyHandle3D handle = world.CreateBody(CreateDynamicDescription(
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                true,
                20));
            world.Step(world.Settings.FixedStepSeconds);
            PhysicsVector3 maximumImpulse = new PhysicsVector3(float.MaxValue, 0f, 0f);

            world.ApplyImpulse(handle, PhysicsVector3.UnitX);
            Assert.Throws<ArgumentOutOfRangeException>(() => world.ApplyImpulse(handle, maximumImpulse));
            Assert.Equal(PhysicsScalar.Zero, world.GetBodySnapshot(handle).LinearVelocity.X);

            world.Step(world.Settings.FixedStepSeconds);
            PhysicsScalar velocityAfterAcceptedImpulse = world.GetBodySnapshot(handle).LinearVelocity.X;
            Assert.Equal(PhysicsScalar.One, velocityAfterAcceptedImpulse);

            world.Step(world.Settings.FixedStepSeconds);
            Assert.Equal(velocityAfterAcceptedImpulse, world.GetBodySnapshot(handle).LinearVelocity.X);
            Assert.Equal(1, world.LastStepMetrics.BodyCount);
        }

        /// <summary>
        /// Verifies next-step removal invalidates the old generation and a later creation safely reuses its slot.
        /// </summary>
        [Fact]
        public void RemoveBody_AfterNextStep_InvalidatesOldHandleAndReusesNewGeneration() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(bodyCapacity: 1, shapeCapacity: 1, islandCapacity: 1, gravity: PhysicsVector3.Zero));
            HelPhysicsBodyHandle3D first = world.CreateBody(CreateDynamicDescription(PhysicsVector3.Zero, PhysicsVector3.Zero, true, 5));
            world.Step(world.Settings.FixedStepSeconds);

            world.RemoveBody(first);
            Assert.True(world.GetBodySnapshot(first).IsActive);
            world.Step(world.Settings.FixedStepSeconds);

            Assert.Throws<InvalidOperationException>(() => world.GetBodySnapshot(first));
            HelPhysicsBodyHandle3D replacement = world.CreateBody(CreateDynamicDescription(PhysicsVector3.Zero, PhysicsVector3.Zero, true, 5));

            Assert.Equal(first.Index, replacement.Index);
            Assert.NotEqual(first.Generation, replacement.Generation);
            Assert.True(world.GetBodySnapshot(replacement).IsPending);
        }

        /// <summary>
        /// Verifies ownership validation rejects an index-and-generation collision from an independently created world.
        /// </summary>
        [Fact]
        public void GetBodySnapshot_WithHandleFromAnotherWorld_RejectsOwnershipCollision() {
            HelPhysicsWorld3D firstWorld = new HelPhysicsWorld3D(CreateSettings(gravity: PhysicsVector3.Zero));
            HelPhysicsWorld3D secondWorld = new HelPhysicsWorld3D(CreateSettings(gravity: PhysicsVector3.Zero));
            HelPhysicsBodyHandle3D firstHandle = firstWorld.CreateBody(CreateDynamicDescription(PhysicsVector3.Zero, PhysicsVector3.Zero, true, 5));
            HelPhysicsBodyHandle3D secondHandle = secondWorld.CreateBody(CreateDynamicDescription(PhysicsVector3.Zero, PhysicsVector3.Zero, true, 5));

            Assert.Equal(firstHandle.Index, secondHandle.Index);
            Assert.Equal(firstHandle.Generation, secondHandle.Generation);
            Assert.NotEqual(firstHandle.WorldId, secondHandle.WorldId);
            Assert.Throws<InvalidOperationException>(() => secondWorld.GetBodySnapshot(firstHandle));
            Assert.True(secondWorld.GetBodySnapshot(secondHandle).IsPending);
        }

        /// <summary>
        /// Verifies the monotonic ownership allocator permanently latches after issuing the final positive token and never cycles to one.
        /// </summary>
        [Fact]
        public void WorldIdAllocator_AfterPositiveRangeExhaustion_NeverReusesToken() {
            HelPhysicsWorldIdAllocator3D allocator = new HelPhysicsWorldIdAllocator3D(int.MaxValue - 1);

            uint finalToken = allocator.Allocate();
            InvalidOperationException firstFailure = Assert.Throws<InvalidOperationException>(() => allocator.Allocate());
            InvalidOperationException repeatedFailure = Assert.Throws<InvalidOperationException>(() => allocator.Allocate());

            Assert.Equal((uint)int.MaxValue, finalToken);
            Assert.True(allocator.IsExhausted);
            Assert.Equal("The HelPhysics world ownership token range is exhausted.", firstFailure.Message);
            Assert.Equal(firstFailure.Message, repeatedFailure.Message);
        }

        /// <summary>
        /// Verifies pending reservations report exact body and shape pool diagnostics without partial creation.
        /// </summary>
        [Fact]
        public void CreateBody_WhenBodyOrShapeCapacityIsExhausted_ThrowsExactDiagnostic() {
            HelPhysicsWorld3D bodyWorld = new HelPhysicsWorld3D(CreateSettings(
                bodyCapacity: 1,
                shapeCapacity: 2,
                islandCapacity: 1,
                gravity: PhysicsVector3.Zero));
            bodyWorld.CreateBody(CreateDynamicDescription(PhysicsVector3.Zero, PhysicsVector3.Zero, true, 5));

            AssertCapacityExceeded(
                () => bodyWorld.CreateBody(CreateDynamicDescription(PhysicsVector3.UnitX, PhysicsVector3.Zero, true, 5)),
                "body",
                1);

            HelPhysicsWorld3D shapeWorld = new HelPhysicsWorld3D(CreateSettings(
                bodyCapacity: 2,
                shapeCapacity: 1,
                islandCapacity: 2,
                gravity: PhysicsVector3.Zero));
            shapeWorld.CreateBody(CreateDynamicDescription(PhysicsVector3.Zero, PhysicsVector3.Zero, true, 5));

            AssertCapacityExceeded(
                () => shapeWorld.CreateBody(CreateDynamicDescription(PhysicsVector3.UnitX, PhysicsVector3.Zero, true, 5)),
                "shape",
                1);
        }

        /// <summary>
        /// Verifies command buffer exhaustion reports its exact configured capacity and never drops the rejected command.
        /// </summary>
        [Fact]
        public void DeferredCommandBuffer_WhenCapacityIsExhausted_ThrowsExactDiagnostic() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(deferredCommandCapacity: 1, gravity: PhysicsVector3.Zero));
            HelPhysicsBodyHandle3D handle = world.CreateBody(CreateDynamicDescription(PhysicsVector3.Zero, PhysicsVector3.Zero, true, 5));

            AssertCapacityExceeded(() => world.ApplyImpulse(handle, PhysicsVector3.UnitX), "deferred command", 1);

            world.Step(world.Settings.FixedStepSeconds);
            Assert.Equal(PhysicsScalar.Zero, world.GetBodySnapshot(handle).LinearVelocity.X);
        }

        /// <summary>
        /// Verifies broadphase candidate demand beyond fixed storage reports the exact candidate-pair diagnostic.
        /// </summary>
        [Fact]
        public void Step_WhenCandidateCapacityIsExceeded_ThrowsExactDiagnostic() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(
                bodyCapacity: 3,
                shapeCapacity: 3,
                candidatePairCapacity: 1,
                manifoldCapacity: 4,
                contactPointCapacity: 12,
                islandCapacity: 3,
                deferredCommandCapacity: 3,
                gravity: PhysicsVector3.Zero));
            world.CreateBody(CreateDynamicDescription(PhysicsVector3.Zero, PhysicsVector3.Zero, true, 5));
            world.CreateBody(CreateDynamicDescription(PhysicsVector3.Zero, PhysicsVector3.Zero, true, 5));
            world.CreateBody(CreateDynamicDescription(PhysicsVector3.Zero, PhysicsVector3.Zero, true, 5));

            AssertCapacityExceeded(() => world.Step(world.Settings.FixedStepSeconds), "candidate pair", 1);
        }

        /// <summary>
        /// Verifies an unexpected failure after step mutation permanently faults the world and prevents command or step replay.
        /// </summary>
        [Fact]
        public void Step_WhenPostMutationFailureOccurs_LatchesExplicitPermanentWorldFault() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(
                bodyCapacity: 3,
                shapeCapacity: 3,
                candidatePairCapacity: 1,
                manifoldCapacity: 4,
                contactPointCapacity: 12,
                islandCapacity: 3,
                deferredCommandCapacity: 3,
                gravity: PhysicsVector3.Zero));
            HelPhysicsBodyHandle3D handle = world.CreateBody(CreateDynamicDescription(PhysicsVector3.Zero, PhysicsVector3.Zero, true, 5));
            world.CreateBody(CreateDynamicDescription(PhysicsVector3.Zero, PhysicsVector3.Zero, true, 5));
            world.CreateBody(CreateDynamicDescription(PhysicsVector3.Zero, PhysicsVector3.Zero, true, 5));

            Assert.False(world.IsFaulted);
            AssertCapacityExceeded(() => world.Step(world.Settings.FixedStepSeconds), "candidate pair", 1);
            Assert.True(world.IsFaulted);
            Assert.True(world.GetBodySnapshot(handle).IsActive);

            InvalidOperationException stepException = Assert.Throws<InvalidOperationException>(
                () => world.Step(world.Settings.FixedStepSeconds));
            InvalidOperationException commandException = Assert.Throws<InvalidOperationException>(
                () => world.ApplyForce(handle, PhysicsVector3.UnitX));
            Assert.Equal("The HelPhysics world is faulted and cannot accept further simulation work.", stepException.Message);
            Assert.Equal(stepException.Message, commandException.Message);
        }

        /// <summary>
        /// Verifies two real contacts cannot partially publish beyond one configured manifold slot.
        /// </summary>
        [Fact]
        public void Step_WhenManifoldCapacityIsExceeded_ThrowsExactDiagnostic() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(
                bodyCapacity: 3,
                shapeCapacity: 3,
                candidatePairCapacity: 2,
                manifoldCapacity: 1,
                contactPointCapacity: 8,
                islandCapacity: 3,
                deferredCommandCapacity: 3,
                gravity: PhysicsVector3.Zero));
            world.CreateBody(HelPhysicsWorldFixture.CreateGroundDescription());
            world.CreateBody(CreateDynamicDescription(new PhysicsVector3(-2f, 0.5f, 0f), PhysicsVector3.Zero, true, 5));
            world.CreateBody(CreateDynamicDescription(new PhysicsVector3(2f, 0.5f, 0f), PhysicsVector3.Zero, true, 5));

            AssertCapacityExceeded(() => world.Step(world.Settings.FixedStepSeconds), "manifold", 1);
        }

        /// <summary>
        /// Verifies a full capacity-one cache reclaims a departed awake pair before retaining the only current arriving contact.
        /// </summary>
        [Fact]
        public void Step_WithCapacityOneDepartingAndArrivingContacts_ReplacesStaleManifoldAndContinues() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(
                bodyCapacity: 3,
                shapeCapacity: 3,
                candidatePairCapacity: 2,
                manifoldCapacity: 1,
                contactPointCapacity: 4,
                islandCapacity: 3,
                deferredCommandCapacity: 4,
                gravity: PhysicsVector3.Zero));
            HelPhysicsBodyHandle3D leftStatic = world.CreateBody(CreateStaticUnitDescription(PhysicsVector3.Zero, 1));
            HelPhysicsBodyHandle3D mover = world.CreateBody(CreateDynamicDescription(
                PhysicsVector3.UnitX,
                PhysicsVector3.Zero,
                true,
                20));
            HelPhysicsBodyHandle3D rightStatic = world.CreateBody(CreateStaticUnitDescription(new PhysicsVector3(3f, 0f, 0f), 2));
            world.Step(world.Settings.FixedStepSeconds);
            Assert.True(world.TryGetCachedManifold(leftStatic, mover, out _));

            world.ApplyImpulse(mover, new PhysicsVector3(20f, 0f, 0f));
            world.Step(world.Settings.FixedStepSeconds);
            world.Step(world.Settings.FixedStepSeconds);

            Assert.False(world.TryGetCachedManifold(leftStatic, mover, out _));
            Assert.True(world.TryGetCachedManifold(mover, rightStatic, out _));
            Assert.Equal(1, world.CachedManifoldCount);
            world.Step(world.Settings.FixedStepSeconds);
            Assert.False(world.IsFaulted);
        }

        /// <summary>
        /// Verifies one four-point box face manifold diagnoses contact-point capacity before cache publication.
        /// </summary>
        [Fact]
        public void Step_WhenContactPointCapacityIsExceeded_ThrowsExactDiagnostic() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(
                bodyCapacity: 2,
                shapeCapacity: 2,
                candidatePairCapacity: 1,
                manifoldCapacity: 1,
                contactPointCapacity: 3,
                islandCapacity: 2,
                deferredCommandCapacity: 2,
                gravity: PhysicsVector3.Zero));
            world.CreateBody(HelPhysicsWorldFixture.CreateGroundDescription());
            world.CreateBody(CreateDynamicDescription(new PhysicsVector3(0f, 0.5f, 0f), PhysicsVector3.Zero, true, 5));

            AssertCapacityExceeded(() => world.Step(world.Settings.FixedStepSeconds), "contact point", 3);
            Assert.Equal(0, world.CachedManifoldCount);
        }

        /// <summary>
        /// Verifies disconnected dynamic groups beyond fixed island storage report the exact island diagnostic.
        /// </summary>
        [Fact]
        public void Step_WhenIslandCapacityIsExceeded_ThrowsExactDiagnostic() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(
                bodyCapacity: 2,
                shapeCapacity: 2,
                candidatePairCapacity: 1,
                manifoldCapacity: 1,
                contactPointCapacity: 4,
                islandCapacity: 1,
                deferredCommandCapacity: 2,
                gravity: PhysicsVector3.Zero));
            world.CreateBody(CreateDynamicDescription(new PhysicsVector3(-10f, 0f, 0f), PhysicsVector3.Zero, true, 5));
            world.CreateBody(CreateDynamicDescription(new PhysicsVector3(10f, 0f, 0f), PhysicsVector3.Zero, true, 5));

            AssertCapacityExceeded(() => world.Step(world.Settings.FixedStepSeconds), "island", 1);
        }

        /// <summary>
        /// Verifies the canonical four-box stack remains tightly ordered and all dynamics sleep by ten simulated seconds.
        /// </summary>
        [Fact]
        public void Step_WithFourBoxStack_RemainsStableAndSleepsAllDynamicsByTwoHundredSteps() {
            HelPhysicsWorldFixture fixture = HelPhysicsWorldFixture.CreateFourBoxStack();

            StepWorld(fixture.World, 200);

            PhysicsScalar previousY = PhysicsScalar.FromFloat(-1f);
            for (int boxIndex = 0; boxIndex < fixture.DynamicBoxes.Length; boxIndex++) {
                HelPhysicsBodySnapshot3D snapshot = fixture.World.GetBodySnapshot(fixture.DynamicBoxes[boxIndex]);
                float expectedY = 0.5f + boxIndex;
                Assert.False(snapshot.IsAwake);
                Assert.InRange(snapshot.Position.Y.ToFloat(), expectedY - 0.03f, expectedY + 0.03f);
                Assert.InRange(snapshot.Position.X.ToFloat(), -0.02f, 0.02f);
                Assert.InRange(snapshot.Position.Z.ToFloat(), -0.02f, 0.02f);
                Assert.True(snapshot.Position.Y > previousY);
                if (boxIndex > 0) {
                    Assert.True(snapshot.Position.Y - previousY > PhysicsScalar.FromFloat(0.9f));
                }
                previousY = snapshot.Position.Y;
            }
        }

        /// <summary>
        /// Verifies exact scalar snapshots and metrics match across two independently constructed worlds for the complete horizon.
        /// </summary>
        [Fact]
        public void Step_WithIndependentReplays_ProducesExactSnapshotsAndMetricsForFullHorizon() {
            HelPhysicsWorldFixture first = HelPhysicsWorldFixture.CreateFourBoxStack();
            HelPhysicsWorldFixture second = HelPhysicsWorldFixture.CreateFourBoxStack();

            for (int stepIndex = 0; stepIndex < 200; stepIndex++) {
                first.World.Step(HelPhysicsWorldFixture.StepSeconds);
                second.World.Step(HelPhysicsWorldFixture.StepSeconds);
                AssertMetricsEqual(first.World.LastStepMetrics, second.World.LastStepMetrics);
                for (int boxIndex = 0; boxIndex < first.DynamicBoxes.Length; boxIndex++) {
                    AssertSnapshotsEqual(
                        first.World.GetBodySnapshot(first.DynamicBoxes[boxIndex]),
                        second.World.GetBodySnapshot(second.DynamicBoxes[boxIndex]));
                }
            }
        }

        /// <summary>
        /// Verifies active and settled steps publish exact body, contact, manifold, island, solver-work, and wake counters.
        /// </summary>
        [Fact]
        public void StepMetrics_AcrossActiveAndSleepingSteps_PublishExactCurrentWork() {
            HelPhysicsWorldFixture fixture = HelPhysicsWorldFixture.CreateFourBoxStack();

            fixture.World.Step(HelPhysicsWorldFixture.StepSeconds);
            HelPhysicsStepMetrics3D active = fixture.World.LastStepMetrics;

            Assert.Equal(5, active.BodyCount);
            Assert.Equal(4, active.AwakeBodyCount);
            Assert.Equal(4, active.CandidatePairCount);
            Assert.Equal(4, active.ManifoldCount);
            Assert.Equal(16, active.ContactPointCount);
            Assert.Equal(1, active.IslandCount);
            Assert.Equal(0, active.SleepingIslandCount);
            Assert.Equal(4, active.SolverIterationCount);
            Assert.Equal(0, active.ExplicitForceWakeCount);
            Assert.Equal(0, active.ExplicitImpulseWakeCount);
            Assert.Equal(0, active.NewCandidateContactWakeCount);
            Assert.Equal(0, active.MovingKinematicContactWakeCount);

            StepWorld(fixture.World, 200);
            fixture.World.Step(HelPhysicsWorldFixture.StepSeconds);
            HelPhysicsStepMetrics3D sleeping = fixture.World.LastStepMetrics;

            Assert.Equal(5, sleeping.BodyCount);
            Assert.Equal(0, sleeping.AwakeBodyCount);
            Assert.Equal(0, sleeping.CandidatePairCount);
            Assert.Equal(0, sleeping.ManifoldCount);
            Assert.Equal(0, sleeping.ContactPointCount);
            Assert.Equal(1, sleeping.IslandCount);
            Assert.Equal(1, sleeping.SleepingIslandCount);
            Assert.Equal(0, sleeping.SolverIterationCount);
        }

        /// <summary>
        /// Verifies a meaningful all-awake candidate suppresses one-tick sleep without producing a false wake transition.
        /// </summary>
        [Fact]
        public void Step_WithNewAllAwakeCandidate_SuppressesImmediateSleepWithoutWakeEvent() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(
                bodyCapacity: 2,
                shapeCapacity: 2,
                candidatePairCapacity: 1,
                manifoldCapacity: 1,
                contactPointCapacity: 4,
                islandCapacity: 2,
                deferredCommandCapacity: 2,
                gravity: PhysicsVector3.Zero));
            HelPhysicsBodyHandle3D first = world.CreateBody(CreateDynamicDescription(PhysicsVector3.Zero, PhysicsVector3.Zero, true, 1));
            HelPhysicsBodyHandle3D second = world.CreateBody(CreateDynamicDescription(PhysicsVector3.UnitY, PhysicsVector3.Zero, true, 1));

            world.Step(world.Settings.FixedStepSeconds);

            Assert.True(world.GetBodySnapshot(first).IsAwake);
            Assert.True(world.GetBodySnapshot(second).IsAwake);
            Assert.Equal(0, world.LastStepMetrics.NewCandidateContactWakeCount);

            world.Step(world.Settings.FixedStepSeconds);

            Assert.False(world.GetBodySnapshot(first).IsAwake);
            Assert.False(world.GetBodySnapshot(second).IsAwake);
        }

        /// <summary>
        /// Verifies a persistent speculative pair without a retained manifold suppresses sleep until delayed physical contact is safely solved.
        /// </summary>
        [Fact]
        public void Step_WithPersistentSpeculativeCandidate_NeverBuildsMixedAwakeIslandAtDelayedContact() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(
                bodyCapacity: 2,
                shapeCapacity: 2,
                candidatePairCapacity: 1,
                manifoldCapacity: 1,
                contactPointCapacity: 4,
                islandCapacity: 2,
                deferredCommandCapacity: 2,
                gravity: PhysicsVector3.Zero));
            HelPhysicsBodyHandle3D quietBody = world.CreateBody(new HelPhysicsBodyDescription3D(
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
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.FromFloat(0.2f),
                PhysicsScalar.FromFloat(0.2f),
                2,
                true));
            HelPhysicsBodyHandle3D movingBody = world.CreateBody(new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(0.5f, 0.5f, 0.5f)),
                BodyKind3D.Dynamic,
                new PhysicsVector3(1.009f, 0f, 0f),
                PhysicsQuaternion.Identity,
                new PhysicsVector3(-0.01f, 0f, 0f),
                PhysicsVector3.Zero,
                PhysicsScalar.One,
                HelPhysicsWorldFixture.CreateStackMaterial(),
                1,
                ushort.MaxValue,
                2,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.FromFloat(0.001f),
                PhysicsScalar.FromFloat(0.001f),
                2,
                true));
            bool observedContact = false;

            for (int stepIndex = 0; stepIndex < 25; stepIndex++) {
                world.Step(world.Settings.FixedStepSeconds);
                observedContact = observedContact || world.LastStepMetrics.ManifoldCount > 0;
            }

            Assert.True(observedContact);
            Assert.True(world.GetBodySnapshot(quietBody).IsAwake);
            Assert.Equal((ushort)0, world.GetBodySnapshot(quietBody).LowMotionStepCount);
            Assert.True(world.GetBodySnapshot(movingBody).IsAwake);
        }

        /// <summary>
        /// Verifies a cached touching contact that becomes speculative suppresses quiet credit on every still-overlapping broadphase step.
        /// </summary>
        [Fact]
        public void Step_WhenCachedStaticDynamicContactBecomesSpeculative_SuppressesSleepWithoutFalseWake() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(
                bodyCapacity: 2,
                shapeCapacity: 2,
                candidatePairCapacity: 1,
                manifoldCapacity: 1,
                contactPointCapacity: 4,
                islandCapacity: 2,
                deferredCommandCapacity: 2,
                gravity: PhysicsVector3.Zero));
            world.CreateBody(CreateStaticUnitDescription(PhysicsVector3.Zero, 1));
            HelPhysicsBodyHandle3D dynamicBody = world.CreateBody(CreateOneTickSleepDynamicDescription(
                PhysicsVector3.UnitY,
                new PhysicsVector3(0f, 0.05f, 0f),
                true,
                2));

            world.Step(world.Settings.FixedStepSeconds);

            Assert.Equal(1, world.LastStepMetrics.CandidatePairCount);
            Assert.Equal(1, world.LastStepMetrics.ManifoldCount);
            Assert.Equal(0, world.LastStepMetrics.NewCandidateContactWakeCount);
            Assert.Equal(1, world.CachedManifoldCount);
            Assert.True(world.GetBodySnapshot(dynamicBody).IsAwake);

            for (int speculativeStepIndex = 0; speculativeStepIndex < 3; speculativeStepIndex++) {
                world.Step(world.Settings.FixedStepSeconds);

                HelPhysicsBodySnapshot3D snapshot = world.GetBodySnapshot(dynamicBody);
                Assert.Equal(1, world.LastStepMetrics.CandidatePairCount);
                Assert.Equal(0, world.LastStepMetrics.ManifoldCount);
                Assert.Equal(0, world.LastStepMetrics.ContactPointCount);
                Assert.Equal(0, world.LastStepMetrics.NewCandidateContactWakeCount);
                Assert.Equal(0, world.CachedManifoldCount);
                Assert.True(snapshot.IsAwake);
                Assert.Equal((ushort)0, snapshot.LowMotionStepCount);
                Assert.Equal(PhysicsScalar.FromFloat(0.05f), snapshot.LinearVelocity.Y);
            }
        }

        /// <summary>
        /// Verifies separating dynamics suppress sleep after cached contact loss and record one wake only for the initially sleeping participant.
        /// </summary>
        [Fact]
        public void Step_WhenCachedDynamicContactBecomesSpeculative_WakesSleepingParticipantOnce() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(
                bodyCapacity: 2,
                shapeCapacity: 2,
                candidatePairCapacity: 1,
                manifoldCapacity: 1,
                contactPointCapacity: 4,
                islandCapacity: 2,
                deferredCommandCapacity: 2,
                gravity: PhysicsVector3.Zero));
            HelPhysicsBodyHandle3D sleepingBody = world.CreateBody(CreateOneTickSleepDynamicDescription(
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                false,
                1));
            HelPhysicsBodyHandle3D separatingBody = world.CreateBody(CreateOneTickSleepDynamicDescription(
                PhysicsVector3.UnitX,
                new PhysicsVector3(0.05f, 0f, 0f),
                true,
                2));

            world.Step(world.Settings.FixedStepSeconds);

            Assert.Equal(1, world.LastStepMetrics.CandidatePairCount);
            Assert.Equal(1, world.LastStepMetrics.ManifoldCount);
            Assert.Equal(1, world.LastStepMetrics.NewCandidateContactWakeCount);
            Assert.True(world.GetBodySnapshot(sleepingBody).IsAwake);
            Assert.True(world.GetBodySnapshot(separatingBody).IsAwake);

            for (int speculativeStepIndex = 0; speculativeStepIndex < 3; speculativeStepIndex++) {
                world.Step(world.Settings.FixedStepSeconds);

                HelPhysicsBodySnapshot3D sleepingSnapshot = world.GetBodySnapshot(sleepingBody);
                HelPhysicsBodySnapshot3D separatingSnapshot = world.GetBodySnapshot(separatingBody);
                Assert.Equal(1, world.LastStepMetrics.CandidatePairCount);
                Assert.Equal(0, world.LastStepMetrics.ManifoldCount);
                Assert.Equal(0, world.LastStepMetrics.NewCandidateContactWakeCount);
                Assert.True(sleepingSnapshot.IsAwake);
                Assert.True(separatingSnapshot.IsAwake);
                Assert.Equal((ushort)0, sleepingSnapshot.LowMotionStepCount);
                Assert.Equal((ushort)0, separatingSnapshot.LowMotionStepCount);
                Assert.Equal(PhysicsScalar.Zero, sleepingSnapshot.LinearVelocity.X);
                Assert.Equal(PhysicsScalar.FromFloat(0.05f), separatingSnapshot.LinearVelocity.X);
            }
        }

        /// <summary>
        /// Verifies a stable static contact may sleep and remain retained without speculative suppression or repeated wake diagnostics.
        /// </summary>
        [Fact]
        public void Step_WithStableRetainedSleepingContact_PermitsSleepWithoutRepeatedWake() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(
                bodyCapacity: 2,
                shapeCapacity: 2,
                candidatePairCapacity: 1,
                manifoldCapacity: 1,
                contactPointCapacity: 4,
                islandCapacity: 2,
                deferredCommandCapacity: 2,
                gravity: PhysicsVector3.Zero));
            world.CreateBody(CreateStaticUnitDescription(PhysicsVector3.Zero, 1));
            HelPhysicsBodyHandle3D dynamicBody = world.CreateBody(CreateOneTickSleepDynamicDescription(
                PhysicsVector3.UnitY,
                PhysicsVector3.Zero,
                true,
                2));

            world.Step(world.Settings.FixedStepSeconds);
            Assert.True(world.GetBodySnapshot(dynamicBody).IsAwake);
            Assert.Equal(1, world.LastStepMetrics.ManifoldCount);
            Assert.Equal(0, world.LastStepMetrics.NewCandidateContactWakeCount);

            world.Step(world.Settings.FixedStepSeconds);
            Assert.False(world.GetBodySnapshot(dynamicBody).IsAwake);
            Assert.Equal(1, world.LastStepMetrics.ManifoldCount);
            Assert.Equal(1, world.CachedManifoldCount);
            Assert.Equal(0, world.LastStepMetrics.NewCandidateContactWakeCount);

            for (int retainedStepIndex = 0; retainedStepIndex < 2; retainedStepIndex++) {
                world.Step(world.Settings.FixedStepSeconds);

                Assert.False(world.GetBodySnapshot(dynamicBody).IsAwake);
                Assert.Equal(0, world.LastStepMetrics.CandidatePairCount);
                Assert.Equal(0, world.LastStepMetrics.ManifoldCount);
                Assert.Equal(1, world.LastStepMetrics.IslandCount);
                Assert.Equal(1, world.LastStepMetrics.SleepingIslandCount);
                Assert.Equal(0, world.LastStepMetrics.NewCandidateContactWakeCount);
                Assert.Equal(1, world.CachedManifoldCount);
            }
        }

        /// <summary>
        /// Verifies an explicit force is deferred, wakes the complete sleeping stack once, and reports its dedicated reason.
        /// </summary>
        [Fact]
        public void ApplyForce_ToSleepingStack_WakesWholeIslandOnNextStep() {
            HelPhysicsWorldFixture fixture = HelPhysicsWorldFixture.CreateFourBoxStack();
            StepWorld(fixture.World, 200);

            fixture.World.ApplyForce(fixture.DynamicBoxes[0], new PhysicsVector3(0f, 2f, 0f));
            Assert.False(fixture.World.GetBodySnapshot(fixture.DynamicBoxes[0]).IsAwake);

            fixture.World.Step(HelPhysicsWorldFixture.StepSeconds);

            for (int boxIndex = 0; boxIndex < fixture.DynamicBoxes.Length; boxIndex++) {
                Assert.True(fixture.World.GetBodySnapshot(fixture.DynamicBoxes[boxIndex]).IsAwake);
            }
            Assert.Equal(1, fixture.World.LastStepMetrics.ExplicitForceWakeCount);
            Assert.Equal(0, fixture.World.LastStepMetrics.ExplicitImpulseWakeCount);
        }

        /// <summary>
        /// Verifies an explicit impulse is deferred, wakes the complete sleeping stack once, and changes same-step velocity.
        /// </summary>
        [Fact]
        public void ApplyImpulse_ToSleepingStack_WakesWholeIslandOnNextStep() {
            HelPhysicsWorldFixture fixture = HelPhysicsWorldFixture.CreateFourBoxStack();
            StepWorld(fixture.World, 200);

            fixture.World.ApplyImpulse(fixture.DynamicBoxes[3], new PhysicsVector3(0f, 0.1f, 0f));
            Assert.False(fixture.World.GetBodySnapshot(fixture.DynamicBoxes[3]).IsAwake);

            fixture.World.Step(HelPhysicsWorldFixture.StepSeconds);

            for (int boxIndex = 0; boxIndex < fixture.DynamicBoxes.Length; boxIndex++) {
                Assert.True(fixture.World.GetBodySnapshot(fixture.DynamicBoxes[boxIndex]).IsAwake);
            }
            Assert.Equal(0, fixture.World.LastStepMetrics.ExplicitForceWakeCount);
            Assert.Equal(1, fixture.World.LastStepMetrics.ExplicitImpulseWakeCount);
        }

        /// <summary>
        /// Verifies moving-kinematic contact uses its dedicated wake route rather than a generic candidate reason.
        /// </summary>
        [Fact]
        public void Step_WithMovingKinematicContact_ReportsDedicatedWakeReason() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(
                bodyCapacity: 2,
                shapeCapacity: 2,
                candidatePairCapacity: 1,
                manifoldCapacity: 1,
                contactPointCapacity: 4,
                islandCapacity: 2,
                deferredCommandCapacity: 2,
                gravity: PhysicsVector3.Zero));
            HelPhysicsBodyHandle3D dynamic = world.CreateBody(CreateDynamicDescription(PhysicsVector3.Zero, PhysicsVector3.Zero, false, 5));
            world.CreateBody(CreateKinematicDescription(new PhysicsVector3(0f, 0.9f, 0f), new PhysicsVector3(0.1f, 0f, 0f)));

            world.Step(world.Settings.FixedStepSeconds);

            Assert.True(world.GetBodySnapshot(dynamic).IsAwake);
            Assert.Equal(0, world.LastStepMetrics.NewCandidateContactWakeCount);
            Assert.Equal(1, world.LastStepMetrics.MovingKinematicContactWakeCount);
        }

        /// <summary>
        /// Verifies a retained same-feature contact moving 0.025 units per step cannot earn quiet credit or erase tangential motion.
        /// </summary>
        [Fact]
        public void Step_WithSlidingUnstableContact_KeepsDynamicAwakeAndPreservesVelocity() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(
                bodyCapacity: 2,
                shapeCapacity: 2,
                candidatePairCapacity: 1,
                manifoldCapacity: 1,
                contactPointCapacity: 4,
                islandCapacity: 2,
                deferredCommandCapacity: 2,
                gravity: PhysicsVector3.Zero));
            world.CreateBody(HelPhysicsWorldFixture.CreateGroundDescription());
            HelPhysicsBodyHandle3D slidingBody = world.CreateBody(CreateSlidingDynamicDescription());

            for (int stepIndex = 0; stepIndex < 8; stepIndex++) {
                world.Step(world.Settings.FixedStepSeconds);
            }

            HelPhysicsBodySnapshot3D snapshot = world.GetBodySnapshot(slidingBody);
            Assert.True(snapshot.IsAwake);
            Assert.Equal((ushort)0, snapshot.LowMotionStepCount);
            Assert.Equal(PhysicsScalar.FromFloat(0.5f), snapshot.LinearVelocity.X);
            Assert.InRange(snapshot.Position.X.ToFloat(), 0.1999f, 0.2001f);
            Assert.Equal(1, world.CachedManifoldCount);
        }

        /// <summary>
        /// Verifies constructor-activated static proxies update once and then incur no repeated linear broadphase refresh work.
        /// </summary>
        [Fact]
        public void Step_WithPopulatedStaticWorld_UpdatesProxiesOnlyDuringActivation() {
            const int staticBodyCount = 12;
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(
                bodyCapacity: staticBodyCount,
                shapeCapacity: staticBodyCount,
                candidatePairCapacity: 1,
                manifoldCapacity: 1,
                contactPointCapacity: 4,
                islandCapacity: staticBodyCount,
                deferredCommandCapacity: staticBodyCount,
                gravity: PhysicsVector3.Zero));
            for (int bodyIndex = 0; bodyIndex < staticBodyCount; bodyIndex++) {
                world.CreateBody(CreateStaticUnitDescription(
                    new PhysicsVector3(bodyIndex * 3f, 0f, 0f),
                    bodyIndex + 1));
            }

            world.Step(world.Settings.FixedStepSeconds);

            Assert.Equal(staticBodyCount, world.PhaseTwoProxyUpdateCount);
            Assert.Equal(0, world.PhaseElevenProxyUpdateCount);

            world.Step(world.Settings.FixedStepSeconds);

            Assert.Equal(0, world.PhaseTwoProxyUpdateCount);
            Assert.Equal(0, world.PhaseElevenProxyUpdateCount);
        }

        /// <summary>
        /// Verifies settled static and sleeping stack proxies receive no phase-two or post-pose refresh calls.
        /// </summary>
        [Fact]
        public void Step_WithSettledStack_PerformsZeroProxyUpdates() {
            HelPhysicsWorldFixture fixture = HelPhysicsWorldFixture.CreateFourBoxStack();
            StepWorld(fixture.World, 200);

            fixture.World.Step(fixture.World.Settings.FixedStepSeconds);

            Assert.Equal(0, fixture.World.PhaseTwoProxyUpdateCount);
            Assert.Equal(0, fixture.World.PhaseElevenProxyUpdateCount);
            Assert.Equal(0, fixture.World.LastStepMetrics.CandidatePairCount);
        }

        /// <summary>
        /// Verifies explicit island wake refreshes four changed activity flags before collision and four changed poses afterward.
        /// </summary>
        [Fact]
        public void Step_AfterExplicitStackWake_UpdatesOnlyFourDynamicProxiesPerProxyPhase() {
            HelPhysicsWorldFixture fixture = HelPhysicsWorldFixture.CreateFourBoxStack();
            StepWorld(fixture.World, 200);
            fixture.World.ApplyImpulse(fixture.DynamicBoxes[3], new PhysicsVector3(0.1f, 0f, 0f));

            fixture.World.Step(fixture.World.Settings.FixedStepSeconds);

            Assert.Equal(4, fixture.World.PhaseTwoProxyUpdateCount);
            Assert.Equal(4, fixture.World.PhaseElevenProxyUpdateCount);
            Assert.Equal(4, fixture.World.LastStepMetrics.AwakeBodyCount);
            Assert.Equal(4, fixture.World.LastStepMetrics.ManifoldCount);
        }

        /// <summary>
        /// Verifies sleeping contacts remain cached and age without narrowphase work, while removal invalidates only the affected pair.
        /// </summary>
        [Fact]
        public void SleepingContacts_AcrossQuiescentSteps_RemainCachedWithoutFalseCandidateWakes() {
            HelPhysicsWorldFixture fixture = HelPhysicsWorldFixture.CreateFourBoxStack();
            StepWorld(fixture.World, 200);
            Assert.True(fixture.World.TryGetCachedManifold(
                fixture.Ground,
                fixture.DynamicBoxes[0],
                out HelPhysicsContactManifold3D before));
            int lifetimeBefore = before.GetContact(0).PreviousStepLifetime;
            Assert.Equal(4, fixture.World.CachedManifoldCount);

            StepWorld(fixture.World, 20);

            Assert.Equal(4, fixture.World.CachedManifoldCount);
            Assert.Equal(0, fixture.World.LastStepMetrics.CandidatePairCount);
            Assert.Equal(0, fixture.World.LastStepMetrics.NewCandidateContactWakeCount);
            Assert.True(fixture.World.TryGetCachedManifold(
                fixture.Ground,
                fixture.DynamicBoxes[0],
                out HelPhysicsContactManifold3D after));
            Assert.Equal(lifetimeBefore + 20, after.GetContact(0).PreviousStepLifetime);

            fixture.World.RemoveBody(fixture.DynamicBoxes[3]);
            fixture.World.Step(HelPhysicsWorldFixture.StepSeconds);

            Assert.Equal(3, fixture.World.CachedManifoldCount);
            Assert.Throws<InvalidOperationException>(() => fixture.World.GetBodySnapshot(fixture.DynamicBoxes[3]));
        }

        /// <summary>
        /// Verifies a retained contact that is no longer touched by narrowphase or sleeping persistence expires on the next step.
        /// </summary>
        [Fact]
        public void ManifoldCache_WhenAwakePairSeparates_RemovesGenuinelyUntouchedEntry() {
            HelPhysicsWorld3D world = new HelPhysicsWorld3D(CreateSettings(
                bodyCapacity: 2,
                shapeCapacity: 2,
                candidatePairCapacity: 1,
                manifoldCapacity: 1,
                contactPointCapacity: 4,
                islandCapacity: 2,
                deferredCommandCapacity: 4,
                gravity: PhysicsVector3.Zero));
            world.CreateBody(HelPhysicsWorldFixture.CreateGroundDescription());
            HelPhysicsBodyHandle3D dynamic = world.CreateBody(CreateDynamicDescription(new PhysicsVector3(0f, 0.5f, 0f), PhysicsVector3.Zero, true, 50));
            world.Step(world.Settings.FixedStepSeconds);
            Assert.Equal(1, world.CachedManifoldCount);

            world.ApplyImpulse(dynamic, new PhysicsVector3(0f, 10f, 0f));
            world.Step(world.Settings.FixedStepSeconds);
            world.Step(world.Settings.FixedStepSeconds);

            Assert.Equal(0, world.CachedManifoldCount);
        }

        /// <summary>
        /// Verifies the runtime profiler maps current body, contact, and manifold totals from the immutable step metrics.
        /// </summary>
        [Fact]
        public void TryGetRuntimeProfilerMetrics_AfterStep_MapsSupportedTotals() {
            HelPhysicsWorldFixture fixture = HelPhysicsWorldFixture.CreateFourBoxStack();
            fixture.World.Step(HelPhysicsWorldFixture.StepSeconds);

            bool supplied = fixture.World.TryGetRuntimeProfilerMetrics(out RuntimePhysicsProfilerMetrics metrics);

            Assert.True(supplied);
            Assert.True(metrics.HasBodyCount);
            Assert.True(metrics.HasContactCount);
            Assert.True(metrics.HasConstraintCount);
            Assert.Equal(5, metrics.BodyCount);
            Assert.Equal(16, metrics.ContactCount);
            Assert.Equal(4, metrics.ConstraintCount);
        }

        /// <summary>
        /// Verifies profiler consumers cannot publicly mutate a world-owned reusable physics sample.
        /// </summary>
        [Fact]
        public void RuntimePhysicsProfilerMetrics_DoesNotExposePublicUpdateMutation() {
            System.Reflection.MethodInfo updateMethod = typeof(RuntimePhysicsProfilerMetrics).GetMethod(
                "Update",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

            Assert.Null(updateMethod);
        }

        /// <summary>
        /// Verifies the world retains only current broadphase candidates and owns no dead prior-candidate publication storage or type.
        /// </summary>
        [Fact]
        public void WorldArchitecture_DoesNotRetainPriorCandidatePublicationState() {
            System.Reflection.BindingFlags fieldFlags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic;
            Type worldType = typeof(HelPhysicsWorld3D);

            Assert.Null(worldType.GetField("PublishedCandidatePairs", fieldFlags));
            Assert.Null(worldType.GetField("StagingPublishedCandidatePairs", fieldFlags));
            Assert.Null(worldType.GetField("PublishedCandidatePairCount", fieldFlags));
            Assert.Null(worldType.Assembly.GetType("helengine.HelPhysicsPublishedCandidatePair3D"));
        }

        /// <summary>
        /// Creates a complete settings object while allowing each validation or capacity test to override one relevant value.
        /// </summary>
        /// <param name="bodyCapacity">Fixed body-slot count.</param>
        /// <param name="shapeCapacity">Fixed box-shape slot count.</param>
        /// <param name="candidatePairCapacity">Fixed candidate-pair count.</param>
        /// <param name="manifoldCapacity">Power-of-two persistent manifold count.</param>
        /// <param name="contactPointCapacity">Fixed active contact-point count.</param>
        /// <param name="islandCapacity">Fixed dynamic-island count.</param>
        /// <param name="deferredCommandCapacity">Fixed deferred mutation count.</param>
        /// <param name="velocityIterationCount">Positive velocity iteration count.</param>
        /// <param name="penetrationCorrectionPassCount">Positive positional-correction pass count.</param>
        /// <param name="fixedStepSeconds">Exact public fixed step.</param>
        /// <param name="gravity">Explicit world gravity.</param>
        /// <returns>A settings value containing every supplied override.</returns>
        static HelPhysicsWorldSettings3D CreateSettings(
            int bodyCapacity = 32,
            int shapeCapacity = 32,
            int candidatePairCapacity = 128,
            int manifoldCapacity = 64,
            int contactPointCapacity = 256,
            int islandCapacity = 32,
            int deferredCommandCapacity = 128,
            int velocityIterationCount = 4,
            int penetrationCorrectionPassCount = 1,
            double fixedStepSeconds = 1d / 20d,
            PhysicsVector3 gravity = default) {
            return new HelPhysicsWorldSettings3D(
                bodyCapacity,
                shapeCapacity,
                candidatePairCapacity,
                manifoldCapacity,
                contactPointCapacity,
                islandCapacity,
                deferredCommandCapacity,
                velocityIterationCount,
                penetrationCorrectionPassCount,
                fixedStepSeconds,
                gravity);
        }

        /// <summary>
        /// Creates a complete description used to exercise body-mode and scalar validation branches.
        /// </summary>
        /// <param name="bodyKind">Body mode to validate.</param>
        /// <param name="mass">Explicit body mass.</param>
        /// <param name="linearVelocity">Explicit initial linear velocity.</param>
        /// <param name="orientation">Explicit initial orientation.</param>
        /// <param name="gravityScale">Explicit gravity multiplier.</param>
        /// <param name="linearDamping">Explicit linear damping.</param>
        /// <param name="angularDamping">Explicit angular damping.</param>
        /// <param name="linearSleepThreshold">Explicit linear sleep speed.</param>
        /// <param name="angularSleepThreshold">Explicit angular sleep speed.</param>
        /// <param name="sleepTicks">Explicit quiet duration.</param>
        /// <param name="isAwake">Explicit initial awake state.</param>
        /// <returns>A description built from the supplied validation values.</returns>
        static HelPhysicsBodyDescription3D CreateDescription(
            BodyKind3D bodyKind,
            PhysicsScalar mass,
            PhysicsVector3 linearVelocity,
            PhysicsQuaternion orientation,
            PhysicsScalar gravityScale,
            PhysicsScalar linearDamping,
            PhysicsScalar angularDamping,
            PhysicsScalar linearSleepThreshold,
            PhysicsScalar angularSleepThreshold,
            ushort sleepTicks,
            bool isAwake) {
            return new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(0.5f, 0.5f, 0.5f)),
                bodyKind,
                PhysicsVector3.Zero,
                orientation,
                linearVelocity,
                PhysicsVector3.Zero,
                mass,
                HelPhysicsWorldFixture.CreateStackMaterial(),
                1,
                ushort.MaxValue,
                7,
                gravityScale,
                linearDamping,
                angularDamping,
                linearSleepThreshold,
                angularSleepThreshold,
                sleepTicks,
                isAwake);
        }

        /// <summary>
        /// Creates an explicitly configured dynamic unit box with zero damping for focused world tests.
        /// </summary>
        /// <param name="position">Initial world-space center.</param>
        /// <param name="linearVelocity">Initial world-space linear velocity.</param>
        /// <param name="isAwake">Initial awake state.</param>
        /// <param name="sleepTicks">Positive quiet duration.</param>
        /// <returns>A complete dynamic body description.</returns>
        static HelPhysicsBodyDescription3D CreateDynamicDescription(
            PhysicsVector3 position,
            PhysicsVector3 linearVelocity,
            bool isAwake,
            ushort sleepTicks) {
            return new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(0.5f, 0.5f, 0.5f)),
                BodyKind3D.Dynamic,
                position,
                PhysicsQuaternion.Identity,
                linearVelocity,
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
                sleepTicks,
                isAwake);
        }

        /// <summary>
        /// Creates one dynamic unit box with exact 0.1 speed thresholds and one required quiet tick for speculative-contact tests.
        /// </summary>
        /// <param name="position">Initial world-space center.</param>
        /// <param name="linearVelocity">Initial world-space linear velocity.</param>
        /// <param name="isAwake">Whether the body begins in the awake simulation set.</param>
        /// <param name="entityBindingId">Stable test ownership identifier.</param>
        /// <returns>A complete gravity-free dynamic description with exact one-tick sleep settings.</returns>
        static HelPhysicsBodyDescription3D CreateOneTickSleepDynamicDescription(
            PhysicsVector3 position,
            PhysicsVector3 linearVelocity,
            bool isAwake,
            int entityBindingId) {
            return new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(0.5f, 0.5f, 0.5f)),
                BodyKind3D.Dynamic,
                position,
                PhysicsQuaternion.Identity,
                linearVelocity,
                PhysicsVector3.Zero,
                PhysicsScalar.One,
                HelPhysicsWorldFixture.CreateStackMaterial(),
                1,
                ushort.MaxValue,
                entityBindingId,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.FromFloat(0.1f),
                PhysicsScalar.FromFloat(0.1f),
                1,
                isAwake);
        }

        /// <summary>
        /// Creates a dynamic unit box that explicitly requests sleeping state with the supplied motion for invariant tests.
        /// </summary>
        /// <param name="linearVelocity">Authored initial linear velocity.</param>
        /// <param name="angularVelocity">Authored initial angular velocity.</param>
        /// <returns>A complete dynamic description whose awake flag is false.</returns>
        static HelPhysicsBodyDescription3D CreateSleepingDynamicDescriptionWithVelocities(
            PhysicsVector3 linearVelocity,
            PhysicsVector3 angularVelocity) {
            return new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(0.5f, 0.5f, 0.5f)),
                BodyKind3D.Dynamic,
                PhysicsVector3.Zero,
                PhysicsQuaternion.Identity,
                linearVelocity,
                angularVelocity,
                PhysicsScalar.One,
                HelPhysicsWorldFixture.CreateStackMaterial(),
                1,
                ushort.MaxValue,
                1,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.FromFloat(0.2f),
                PhysicsScalar.FromFloat(0.2f),
                5,
                false);
        }

        /// <summary>
        /// Creates the exact low-threshold sliding body whose 0.025-unit per-step anchor motion must suppress sleep.
        /// </summary>
        /// <returns>An awake unit box with 0.5 horizontal speed, a 0.6 linear sleep threshold, and three sleep ticks.</returns>
        static HelPhysicsBodyDescription3D CreateSlidingDynamicDescription() {
            return new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(0.5f, 0.5f, 0.5f)),
                BodyKind3D.Dynamic,
                new PhysicsVector3(0f, 0.5f, 0f),
                PhysicsQuaternion.Identity,
                new PhysicsVector3(0.5f, 0f, 0f),
                PhysicsVector3.Zero,
                PhysicsScalar.One,
                HelPhysicsWorldFixture.CreateStackMaterial(),
                1,
                ushort.MaxValue,
                3,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.FromFloat(0.6f),
                PhysicsScalar.FromFloat(0.2f),
                3,
                true);
        }

        /// <summary>
        /// Creates a moving zero-mass kinematic unit box for dedicated wake-reason routing.
        /// </summary>
        /// <param name="position">Initial world-space center.</param>
        /// <param name="linearVelocity">Authored kinematic velocity used by contact response and wake detection.</param>
        /// <returns>A complete kinematic body description.</returns>
        static HelPhysicsBodyDescription3D CreateKinematicDescription(
            PhysicsVector3 position,
            PhysicsVector3 linearVelocity) {
            return new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(0.5f, 0.5f, 0.5f)),
                BodyKind3D.Kinematic,
                position,
                PhysicsQuaternion.Identity,
                linearVelocity,
                PhysicsVector3.Zero,
                PhysicsScalar.Zero,
                HelPhysicsWorldFixture.CreateStackMaterial(),
                1,
                ushort.MaxValue,
                2,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.FromFloat(0.2f),
                PhysicsScalar.FromFloat(0.2f),
                5,
                false);
        }

        /// <summary>
        /// Creates one immovable unit box at an explicit position for focused cache-lifecycle tests.
        /// </summary>
        /// <param name="position">World-space center of the static unit box.</param>
        /// <param name="entityBindingId">Stable test ownership identifier.</param>
        /// <returns>A complete zero-mass static unit-box description.</returns>
        static HelPhysicsBodyDescription3D CreateStaticUnitDescription(
            PhysicsVector3 position,
            int entityBindingId) {
            return new HelPhysicsBodyDescription3D(
                new HelPhysicsBoxShape3D(new PhysicsVector3(0.5f, 0.5f, 0.5f)),
                BodyKind3D.Static,
                position,
                PhysicsQuaternion.Identity,
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                PhysicsScalar.Zero,
                HelPhysicsWorldFixture.CreateStackMaterial(),
                1,
                ushort.MaxValue,
                entityBindingId,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                1,
                false);
        }

        /// <summary>
        /// Verifies one invalid step leaves a reserved body pending and all published counters unchanged.
        /// </summary>
        /// <param name="world">World containing one queued creation.</param>
        /// <param name="handle">Reserved body handle.</param>
        /// <param name="invalidStepSeconds">Invalid public step to reject.</param>
        static void AssertInvalidStepPreservesPendingBody(
            HelPhysicsWorld3D world,
            HelPhysicsBodyHandle3D handle,
            double invalidStepSeconds) {
            Assert.Throws<ArgumentOutOfRangeException>(() => world.Step(invalidStepSeconds));
            Assert.True(world.GetBodySnapshot(handle).IsPending);
            Assert.Equal(0, world.LastStepMetrics.BodyCount);
        }

        /// <summary>
        /// Verifies an operation reports the exact fixed-capacity pool name, capacity, and diagnostic message.
        /// </summary>
        /// <param name="operation">Operation expected to exceed fixed storage.</param>
        /// <param name="poolName">Expected concise pool name.</param>
        /// <param name="capacity">Expected configured capacity.</param>
        static void AssertCapacityExceeded(Action operation, string poolName, int capacity) {
            HelPhysicsCapacityExceededException exception = Assert.Throws<HelPhysicsCapacityExceededException>(operation);
            Assert.Equal(poolName, exception.PoolName);
            Assert.Equal(capacity, exception.Capacity);
            Assert.Equal($"The {poolName} pool capacity of {capacity} has been exceeded.", exception.Message);
        }

        /// <summary>
        /// Advances one world by an exact number of configured fixed steps.
        /// </summary>
        /// <param name="world">World to advance.</param>
        /// <param name="stepCount">Non-negative number of steps to run.</param>
        static void StepWorld(HelPhysicsWorld3D world, int stepCount) {
            for (int stepIndex = 0; stepIndex < stepCount; stepIndex++) {
                world.Step(world.Settings.FixedStepSeconds);
            }
        }

        /// <summary>
        /// Verifies exact scalar, state, and lifecycle equality between independent-world snapshots.
        /// </summary>
        /// <param name="first">First snapshot.</param>
        /// <param name="second">Second snapshot.</param>
        static void AssertSnapshotsEqual(HelPhysicsBodySnapshot3D first, HelPhysicsBodySnapshot3D second) {
            Assert.Equal(first.BodyKind, second.BodyKind);
            Assert.Equal(first.Position.X, second.Position.X);
            Assert.Equal(first.Position.Y, second.Position.Y);
            Assert.Equal(first.Position.Z, second.Position.Z);
            Assert.Equal(first.Orientation.X, second.Orientation.X);
            Assert.Equal(first.Orientation.Y, second.Orientation.Y);
            Assert.Equal(first.Orientation.Z, second.Orientation.Z);
            Assert.Equal(first.Orientation.W, second.Orientation.W);
            Assert.Equal(first.LinearVelocity.X, second.LinearVelocity.X);
            Assert.Equal(first.LinearVelocity.Y, second.LinearVelocity.Y);
            Assert.Equal(first.LinearVelocity.Z, second.LinearVelocity.Z);
            Assert.Equal(first.AngularVelocity.X, second.AngularVelocity.X);
            Assert.Equal(first.AngularVelocity.Y, second.AngularVelocity.Y);
            Assert.Equal(first.AngularVelocity.Z, second.AngularVelocity.Z);
            Assert.Equal(first.LowMotionStepCount, second.LowMotionStepCount);
            Assert.Equal(first.IsAwake, second.IsAwake);
            Assert.Equal(first.IsActive, second.IsActive);
            Assert.Equal(first.IsPending, second.IsPending);
        }

        /// <summary>
        /// Verifies exact equality for every immutable per-step counter across independent worlds.
        /// </summary>
        /// <param name="first">First world metrics.</param>
        /// <param name="second">Second world metrics.</param>
        static void AssertMetricsEqual(HelPhysicsStepMetrics3D first, HelPhysicsStepMetrics3D second) {
            Assert.Equal(first.BodyCount, second.BodyCount);
            Assert.Equal(first.AwakeBodyCount, second.AwakeBodyCount);
            Assert.Equal(first.CandidatePairCount, second.CandidatePairCount);
            Assert.Equal(first.ManifoldCount, second.ManifoldCount);
            Assert.Equal(first.ContactPointCount, second.ContactPointCount);
            Assert.Equal(first.IslandCount, second.IslandCount);
            Assert.Equal(first.SleepingIslandCount, second.SleepingIslandCount);
            Assert.Equal(first.SolverIterationCount, second.SolverIterationCount);
            Assert.Equal(first.ExplicitForceWakeCount, second.ExplicitForceWakeCount);
            Assert.Equal(first.ExplicitImpulseWakeCount, second.ExplicitImpulseWakeCount);
            Assert.Equal(first.NewCandidateContactWakeCount, second.NewCandidateContactWakeCount);
            Assert.Equal(first.MovingKinematicContactWakeCount, second.MovingKinematicContactWakeCount);
        }

        /// <summary>
        /// Verifies a physics scalar lies inside one explicit absolute tolerance around an expected float.
        /// </summary>
        /// <param name="expected">Expected float value.</param>
        /// <param name="actual">Actual physics scalar.</param>
        /// <param name="tolerance">Maximum accepted absolute error.</param>
        static void AssertScalarClose(float expected, PhysicsScalar actual, float tolerance) {
            Assert.InRange(actual.ToFloat(), expected - tolerance, expected + tolerance);
        }
    }
}
