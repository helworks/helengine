namespace helengine {
    /// <summary>
    /// Verifies whole-island quiet qualification, atomic sleeping, wake propagation, and wake diagnostics.
    /// </summary>
    public sealed class HelPhysicsIslandSleeper3DTests {
        /// <summary>
        /// Verifies that a connected island sleeps at its greatest required quiet duration and clears all kinetic and transient state.
        /// </summary>
        [Fact]
        public void EvaluateSleep_WhenWholeIslandReachesGreatestSleepTicks_SleepsAndZeroesEveryMember() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            HelPhysicsBodyState3D firstState = CreateDynamicState(true);
            firstState.LinearVelocity = new PhysicsVector3(0.125f, 0f, 0f);
            firstState.AngularVelocity = new PhysicsVector3(0f, 0.0625f, 0f);
            firstState.AccumulatedForce = new PhysicsVector3(1f, 2f, 3f);
            firstState.AccumulatedTorque = new PhysicsVector3(4f, 5f, 6f);
            HelPhysicsBodyState3D secondState = CreateDynamicState(true);
            secondState.LinearVelocity = new PhysicsVector3(0.0625f, 0f, 0f);
            secondState.AngularVelocity = new PhysicsVector3(0f, 0.125f, 0f);
            secondState.AccumulatedForce = new PhysicsVector3(7f, 8f, 9f);
            secondState.AccumulatedTorque = new PhysicsVector3(10f, 11f, 12f);
            HelPhysicsBodyHandle3D firstHandle = bodies.Allocate(
                firstState,
                CreateColdState(BodyKind3D.Dynamic, 0.015625f, 0.015625f, 2));
            HelPhysicsBodyHandle3D secondHandle = bodies.Allocate(
                secondState,
                CreateColdState(BodyKind3D.Dynamic, 0.015625f, 0.015625f, 3));
            HelPhysicsIslandBuilder3D islands = BuildConnectedPair(bodies, 0, 1);
            HelPhysicsIslandSleeper3D sleeper = new HelPhysicsIslandSleeper3D(2);

            EvaluateOneStep(sleeper, bodies, islands);
            EvaluateOneStep(sleeper, bodies, islands);

            Assert.True(bodies.GetRequiredState(firstHandle).IsAwake);
            Assert.True(bodies.GetRequiredState(secondHandle).IsAwake);
            Assert.Equal((ushort)2, bodies.GetRequiredState(firstHandle).LowMotionStepCount);
            Assert.Equal((ushort)2, bodies.GetRequiredState(secondHandle).LowMotionStepCount);

            EvaluateOneStep(sleeper, bodies, islands);

            AssertSleepingAndCleared(bodies.GetRequiredState(firstHandle), 3);
            AssertSleepingAndCleared(bodies.GetRequiredState(secondHandle), 3);
        }

        /// <summary>
        /// Verifies that one member above its own linear threshold prevents sleep and resets every member's shared quiet count.
        /// </summary>
        [Fact]
        public void EvaluateSleep_WithOneFastMember_KeepsWholeIslandAwakeAndResetsCounters() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            HelPhysicsBodyState3D quietState = CreateDynamicState(true);
            quietState.LowMotionStepCount = 7;
            HelPhysicsBodyState3D fastState = CreateDynamicState(true);
            fastState.LinearVelocity = new PhysicsVector3(0.25f, 0f, 0f);
            fastState.LowMotionStepCount = 7;
            HelPhysicsBodyHandle3D quietHandle = bodies.Allocate(
                quietState,
                CreateColdState(BodyKind3D.Dynamic, 0.015625f, 0.015625f, 2));
            HelPhysicsBodyHandle3D fastHandle = bodies.Allocate(
                fastState,
                CreateColdState(BodyKind3D.Dynamic, 0.015625f, 0.015625f, 2));
            HelPhysicsIslandBuilder3D islands = BuildConnectedPair(bodies, 0, 1);
            HelPhysicsIslandSleeper3D sleeper = new HelPhysicsIslandSleeper3D(2);

            EvaluateOneStep(sleeper, bodies, islands);

            Assert.True(bodies.GetRequiredState(quietHandle).IsAwake);
            Assert.True(bodies.GetRequiredState(fastHandle).IsAwake);
            Assert.Equal((ushort)0, bodies.GetRequiredState(quietHandle).LowMotionStepCount);
            Assert.Equal((ushort)0, bodies.GetRequiredState(fastHandle).LowMotionStepCount);
        }

        /// <summary>
        /// Verifies independently that one member above its angular threshold prevents whole-island quiet credit.
        /// </summary>
        [Fact]
        public void EvaluateSleep_WithOneAngularFastMember_KeepsWholeIslandAwakeAndResetsCounters() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            HelPhysicsBodyState3D quietState = CreateDynamicState(true);
            quietState.LowMotionStepCount = 3;
            HelPhysicsBodyState3D fastState = CreateDynamicState(true);
            fastState.AngularVelocity = new PhysicsVector3(0f, 0.25f, 0f);
            fastState.LowMotionStepCount = 3;
            bodies.Allocate(
                quietState,
                CreateColdState(BodyKind3D.Dynamic, 0.015625f, 0.015625f, 2));
            bodies.Allocate(
                fastState,
                CreateColdState(BodyKind3D.Dynamic, 0.015625f, 0.015625f, 2));
            HelPhysicsIslandBuilder3D islands = BuildConnectedPair(bodies, 0, 1);
            HelPhysicsIslandSleeper3D sleeper = new HelPhysicsIslandSleeper3D(2);

            EvaluateOneStep(sleeper, bodies, islands);

            Assert.True(bodies.GetRequiredStateByIndex(0).IsAwake);
            Assert.True(bodies.GetRequiredStateByIndex(1).IsAwake);
            Assert.Equal((ushort)0, bodies.GetRequiredStateByIndex(0).LowMotionStepCount);
            Assert.Equal((ushort)0, bodies.GetRequiredStateByIndex(1).LowMotionStepCount);
        }

        /// <summary>
        /// Verifies inclusive squared thresholds and saturating quiet-count advancement at the unsigned-short maximum.
        /// </summary>
        [Fact]
        public void EvaluateSleep_AtExactThresholdAndMaximumDuration_SaturatesAndSleeps() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(1);
            HelPhysicsBodyState3D state = CreateDynamicState(true);
            state.LinearVelocity = new PhysicsVector3(0.125f, 0f, 0f);
            state.AngularVelocity = new PhysicsVector3(0f, 0.125f, 0f);
            state.LowMotionStepCount = ushort.MaxValue - 1;
            HelPhysicsBodyHandle3D handle = bodies.Allocate(
                state,
                CreateColdState(BodyKind3D.Dynamic, 0.015625f, 0.015625f, ushort.MaxValue));
            HelPhysicsIslandBuilder3D islands = BuildIsolatedBodies(bodies, 1);
            HelPhysicsIslandSleeper3D sleeper = new HelPhysicsIslandSleeper3D(1);

            EvaluateOneStep(sleeper, bodies, islands);

            Assert.False(bodies.GetRequiredState(handle).IsAwake);
            Assert.Equal(ushort.MaxValue, bodies.GetRequiredState(handle).LowMotionStepCount);
        }

        /// <summary>
        /// Verifies that explicit force wakes one complete prior island, records one event, and blocks same-step quiet credit.
        /// </summary>
        [Fact]
        public void WakeForExplicitForce_WithSleepingMember_WakesIslandOnceAndPreventsImmediateResleep() {
            HelPhysicsBodyPool3D bodies = CreateSleepingDynamicBodies(2, 1);
            HelPhysicsIslandBuilder3D islands = BuildConnectedPair(bodies, 0, 1);
            HelPhysicsIslandSleeper3D sleeper = new HelPhysicsIslandSleeper3D(2);

            sleeper.BeginStep();
            sleeper.WakeForExplicitForce(1, bodies, islands);
            sleeper.WakeForExplicitForce(0, bodies, islands);

            AssertAwakeWithCounter(bodies.GetRequiredStateByIndex(0), 0);
            AssertAwakeWithCounter(bodies.GetRequiredStateByIndex(1), 0);
            Assert.Equal(1, sleeper.WakeEventCount);
            Assert.Equal(HelPhysicsWakeReason3D.ExplicitForce, sleeper.GetWakeEventReason(0));
            Assert.Equal(1, sleeper.GetWakeCount(HelPhysicsWakeReason3D.ExplicitForce));
            Assert.Equal(0, sleeper.GetWakeCount(HelPhysicsWakeReason3D.None));

            sleeper.EvaluateSleep(bodies, islands);

            AssertAwakeWithCounter(bodies.GetRequiredStateByIndex(0), 0);
            AssertAwakeWithCounter(bodies.GetRequiredStateByIndex(1), 0);

            EvaluateOneStep(sleeper, bodies, islands);

            Assert.False(bodies.GetRequiredStateByIndex(0).IsAwake);
            Assert.False(bodies.GetRequiredStateByIndex(1).IsAwake);
        }

        /// <summary>
        /// Verifies that an explicit impulse has its own diagnostic reason and resets the complete island quiet state.
        /// </summary>
        [Fact]
        public void WakeForExplicitImpulse_WithSleepingMember_RecordsExplicitImpulseReason() {
            HelPhysicsBodyPool3D bodies = CreateSleepingDynamicBodies(2, 4);
            HelPhysicsIslandBuilder3D islands = BuildConnectedPair(bodies, 0, 1);
            HelPhysicsIslandSleeper3D sleeper = new HelPhysicsIslandSleeper3D(2);

            sleeper.BeginStep();
            sleeper.WakeForExplicitImpulse(0, bodies, islands);

            AssertAwakeWithCounter(bodies.GetRequiredStateByIndex(0), 0);
            AssertAwakeWithCounter(bodies.GetRequiredStateByIndex(1), 0);
            Assert.Equal(1, sleeper.WakeEventCount);
            Assert.Equal(HelPhysicsWakeReason3D.ExplicitImpulse, sleeper.GetWakeEventReason(0));
            Assert.Equal(1, sleeper.GetWakeCount(HelPhysicsWakeReason3D.ExplicitImpulse));
        }

        /// <summary>
        /// Verifies that first-step force and impulse inputs mark awake targets before islands exist and suppress current-island quiet credit after build.
        /// </summary>
        [Fact]
        public void ExplicitWake_WithAwakeBodyAndNoPriorIslands_MarksCurrentStepWithoutEvent() {
            HelPhysicsBodyPool3D forceBodies = new HelPhysicsBodyPool3D(1);
            HelPhysicsBodyState3D forceState = CreateDynamicState(true);
            forceState.LowMotionStepCount = 4;
            forceBodies.Allocate(forceState, CreateColdState(BodyKind3D.Dynamic, 0.01f, 0.01f, 1));
            HelPhysicsIslandBuilder3D forceIslands = new HelPhysicsIslandBuilder3D(1, 1);
            HelPhysicsIslandSleeper3D forceSleeper = new HelPhysicsIslandSleeper3D(1);
            forceSleeper.BeginStep();

            forceSleeper.WakeForExplicitForce(0, forceBodies, forceIslands);
            forceIslands.Build(
                forceBodies,
                Array.Empty<HelPhysicsPairKey3D>(),
                Array.Empty<HelPhysicsContactManifold3D>(),
                0);
            forceSleeper.EvaluateSleep(forceBodies, forceIslands);

            Assert.True(forceBodies.GetRequiredStateByIndex(0).IsAwake);
            Assert.Equal((ushort)0, forceBodies.GetRequiredStateByIndex(0).LowMotionStepCount);
            Assert.Equal(0, forceSleeper.WakeEventCount);

            HelPhysicsBodyPool3D impulseBodies = new HelPhysicsBodyPool3D(1);
            HelPhysicsBodyState3D impulseState = CreateDynamicState(true);
            impulseState.LowMotionStepCount = 5;
            impulseBodies.Allocate(impulseState, CreateColdState(BodyKind3D.Dynamic, 0.01f, 0.01f, 1));
            HelPhysicsIslandBuilder3D impulseIslands = new HelPhysicsIslandBuilder3D(1, 1);
            HelPhysicsIslandSleeper3D impulseSleeper = new HelPhysicsIslandSleeper3D(1);
            impulseSleeper.BeginStep();

            impulseSleeper.WakeForExplicitImpulse(0, impulseBodies, impulseIslands);
            impulseIslands.Build(
                impulseBodies,
                Array.Empty<HelPhysicsPairKey3D>(),
                Array.Empty<HelPhysicsContactManifold3D>(),
                0);
            impulseSleeper.EvaluateSleep(impulseBodies, impulseIslands);

            Assert.True(impulseBodies.GetRequiredStateByIndex(0).IsAwake);
            Assert.Equal((ushort)0, impulseBodies.GetRequiredStateByIndex(0).LowMotionStepCount);
            Assert.Equal(0, impulseSleeper.WakeEventCount);
        }

        /// <summary>
        /// Verifies that a meaningful candidate touching a sleeping body wakes only connected participant islands and records one event per transition.
        /// </summary>
        [Fact]
        public void WakeForNewCandidateContact_WithSleepingParticipant_WakesConnectedIslandButNotUnrelatedIsland() {
            HelPhysicsBodyPool3D bodies = CreateSleepingDynamicBodies(4, 5);
            bodies.GetRequiredStateByIndex(3).IsAwake = true;
            HelPhysicsIslandBuilder3D islands = BuildConnectedPair(bodies, 0, 1);
            HelPhysicsIslandSleeper3D sleeper = new HelPhysicsIslandSleeper3D(4);
            HelPhysicsCandidatePair3D candidate = new HelPhysicsCandidatePair3D(1, 3);

            sleeper.BeginStep();
            sleeper.WakeForNewCandidateContact(candidate, bodies, islands);

            Assert.True(bodies.GetRequiredStateByIndex(0).IsAwake);
            Assert.True(bodies.GetRequiredStateByIndex(1).IsAwake);
            Assert.False(bodies.GetRequiredStateByIndex(2).IsAwake);
            Assert.True(bodies.GetRequiredStateByIndex(3).IsAwake);
            Assert.Equal((ushort)0, bodies.GetRequiredStateByIndex(0).LowMotionStepCount);
            Assert.Equal((ushort)0, bodies.GetRequiredStateByIndex(1).LowMotionStepCount);
            Assert.Equal((ushort)5, bodies.GetRequiredStateByIndex(2).LowMotionStepCount);
            Assert.Equal((ushort)0, bodies.GetRequiredStateByIndex(3).LowMotionStepCount);
            Assert.Equal(1, sleeper.WakeEventCount);
            Assert.Equal(HelPhysicsWakeReason3D.NewCandidateContact, sleeper.GetWakeEventReason(0));
            Assert.Equal(1, sleeper.GetWakeCount(HelPhysicsWakeReason3D.NewCandidateContact));
        }

        /// <summary>
        /// Verifies that first-step candidates between awake dynamics require no prior island publication and produce no wake event.
        /// </summary>
        [Fact]
        public void WakeForNewCandidateContact_WithOnlyAwakeDynamicsAndNoPriorIslands_DoesNothing() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            HelPhysicsBodyState3D firstState = CreateDynamicState(true);
            firstState.LowMotionStepCount = 2;
            HelPhysicsBodyState3D secondState = CreateDynamicState(true);
            secondState.LowMotionStepCount = 3;
            bodies.Allocate(firstState, CreateColdState(BodyKind3D.Dynamic, 0.01f, 0.01f, 2));
            bodies.Allocate(secondState, CreateColdState(BodyKind3D.Dynamic, 0.01f, 0.01f, 2));
            HelPhysicsIslandBuilder3D islands = new HelPhysicsIslandBuilder3D(2, 2);
            HelPhysicsIslandSleeper3D sleeper = new HelPhysicsIslandSleeper3D(2);

            sleeper.BeginStep();
            sleeper.WakeForNewCandidateContact(
                new HelPhysicsCandidatePair3D(0, 1),
                bodies,
                islands);

            Assert.True(bodies.GetRequiredStateByIndex(0).IsAwake);
            Assert.True(bodies.GetRequiredStateByIndex(1).IsAwake);
            Assert.Equal((ushort)2, bodies.GetRequiredStateByIndex(0).LowMotionStepCount);
            Assert.Equal((ushort)3, bodies.GetRequiredStateByIndex(1).LowMotionStepCount);
            Assert.Equal(0, sleeper.WakeEventCount);
        }

        /// <summary>
        /// Verifies that an active contact with moving kinematic velocity wakes only the connected dynamic island.
        /// </summary>
        [Fact]
        public void WakeForMovingKinematicContact_WithActiveContact_WakesConnectedDynamicIsland() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(4);
            bodies.Allocate(CreateSleepingState(6), CreateColdState(BodyKind3D.Dynamic, 0.01f, 0.01f, 2));
            bodies.Allocate(CreateSleepingState(6), CreateColdState(BodyKind3D.Dynamic, 0.01f, 0.01f, 2));
            bodies.Allocate(CreateSleepingState(6), CreateColdState(BodyKind3D.Dynamic, 0.01f, 0.01f, 2));
            HelPhysicsBodyState3D kinematicState = CreateDynamicState(false);
            kinematicState.LinearVelocity = new PhysicsVector3(0f, 1f, 0f);
            bodies.Allocate(kinematicState, CreateColdState(BodyKind3D.Kinematic, 0.01f, 0.01f, 2));
            HelPhysicsIslandBuilder3D islands = BuildConnectedPair(bodies, 0, 1);
            HelPhysicsIslandSleeper3D sleeper = new HelPhysicsIslandSleeper3D(4);
            HelPhysicsPairKey3D pair = new HelPhysicsPairKey3D(0, 3);
            HelPhysicsContactManifold3D manifold = CreateActiveManifold();

            sleeper.BeginStep();
            sleeper.WakeForMovingKinematicContact(pair, in manifold, bodies, islands);

            Assert.True(bodies.GetRequiredStateByIndex(0).IsAwake);
            Assert.True(bodies.GetRequiredStateByIndex(1).IsAwake);
            Assert.False(bodies.GetRequiredStateByIndex(2).IsAwake);
            Assert.Equal((ushort)0, bodies.GetRequiredStateByIndex(0).LowMotionStepCount);
            Assert.Equal((ushort)0, bodies.GetRequiredStateByIndex(1).LowMotionStepCount);
            Assert.Equal((ushort)6, bodies.GetRequiredStateByIndex(2).LowMotionStepCount);
            Assert.Equal(1, sleeper.WakeEventCount);
            Assert.Equal(HelPhysicsWakeReason3D.MovingKinematicContact, sleeper.GetWakeEventReason(0));
            Assert.Equal(1, sleeper.GetWakeCount(HelPhysicsWakeReason3D.MovingKinematicContact));
        }

        /// <summary>
        /// Verifies that a stationary kinematic contact does not disturb a sleeping dynamic island.
        /// </summary>
        [Fact]
        public void WakeForMovingKinematicContact_WithStationaryKinematic_LeavesIslandSleeping() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateSleepingState(8), CreateColdState(BodyKind3D.Dynamic, 0.01f, 0.01f, 2));
            bodies.Allocate(CreateDynamicState(false), CreateColdState(BodyKind3D.Kinematic, 0.01f, 0.01f, 2));
            HelPhysicsIslandBuilder3D islands = BuildIsolatedBodies(bodies, 2);
            HelPhysicsIslandSleeper3D sleeper = new HelPhysicsIslandSleeper3D(2);
            HelPhysicsPairKey3D pair = new HelPhysicsPairKey3D(0, 1);
            HelPhysicsContactManifold3D manifold = CreateActiveManifold();

            sleeper.BeginStep();
            sleeper.WakeForMovingKinematicContact(pair, in manifold, bodies, islands);

            Assert.False(bodies.GetRequiredStateByIndex(0).IsAwake);
            Assert.Equal((ushort)8, bodies.GetRequiredStateByIndex(0).LowMotionStepCount);
            Assert.Equal(0, sleeper.WakeEventCount);
            Assert.Equal(0, sleeper.GetWakeCount(HelPhysicsWakeReason3D.MovingKinematicContact));
        }

        /// <summary>
        /// Verifies that sleep evaluation diagnoses default invalid cold sleep settings before mutating hot state.
        /// </summary>
        [Fact]
        public void EvaluateSleep_WithInvalidColdSleepSettings_ThrowsWithoutMutatingBody() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(1);
            HelPhysicsBodyState3D state = CreateDynamicState(true);
            state.LowMotionStepCount = 9;
            bodies.Allocate(state, new HelPhysicsBodyColdState3D {
                BodyKind = BodyKind3D.Dynamic,
                Material = new HelPhysicsMaterial3D(PhysicsScalar.Zero, PhysicsScalar.Zero, PhysicsScalar.Zero)
            });
            HelPhysicsIslandBuilder3D islands = BuildIsolatedBodies(bodies, 1);
            HelPhysicsIslandSleeper3D sleeper = new HelPhysicsIslandSleeper3D(1);

            sleeper.BeginStep();
            Assert.Throws<InvalidOperationException>(() => sleeper.EvaluateSleep(bodies, islands));

            Assert.True(bodies.GetRequiredStateByIndex(0).IsAwake);
            Assert.Equal((ushort)9, bodies.GetRequiredStateByIndex(0).LowMotionStepCount);
        }

        /// <summary>
        /// Verifies that sleeper construction and evaluation reject incompatible fixed body capacities.
        /// </summary>
        [Fact]
        public void FixedCapacityValidation_WithInvalidOrMismatchedCapacity_Throws() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelPhysicsIslandSleeper3D(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelPhysicsIslandSleeper3D(65535));

            HelPhysicsBodyPool3D bodies = CreateSleepingDynamicBodies(1, 1);
            HelPhysicsIslandBuilder3D islands = BuildIsolatedBodies(bodies, 1);
            HelPhysicsIslandSleeper3D sleeper = new HelPhysicsIslandSleeper3D(2);
            sleeper.BeginStep();

            Assert.Throws<ArgumentException>(() => sleeper.EvaluateSleep(bodies, islands));
        }

        /// <summary>
        /// Verifies that warmed sleep evaluation, every wake path, transient reset, and diagnostic access allocate no managed memory.
        /// </summary>
        [Fact]
        public void SleepAndWakePaths_AfterWarmup_AllocateNoManagedMemory() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(4);
            bodies.Allocate(CreateSleepingState(1), CreateColdState(BodyKind3D.Dynamic, 0.01f, 0.01f, 1));
            bodies.Allocate(CreateSleepingState(1), CreateColdState(BodyKind3D.Dynamic, 0.01f, 0.01f, 1));
            bodies.Allocate(CreateSleepingState(1), CreateColdState(BodyKind3D.Dynamic, 0.01f, 0.01f, 1));
            HelPhysicsBodyState3D kinematicState = CreateDynamicState(false);
            kinematicState.AngularVelocity = new PhysicsVector3(0f, 0f, 1f);
            bodies.Allocate(kinematicState, CreateColdState(BodyKind3D.Kinematic, 0.01f, 0.01f, 1));
            HelPhysicsIslandBuilder3D islands = BuildConnectedPair(bodies, 0, 1);
            HelPhysicsIslandSleeper3D sleeper = new HelPhysicsIslandSleeper3D(4);
            HelPhysicsCandidatePair3D candidate = new HelPhysicsCandidatePair3D(1, 2);
            HelPhysicsPairKey3D kinematicPair = new HelPhysicsPairKey3D(0, 3);
            HelPhysicsContactManifold3D manifold = CreateActiveManifold();
            RunWakeAndSleepCycle(sleeper, bodies, islands, candidate, kinematicPair, in manifold);
            sleeper.BeginStep();
            sleeper.EvaluateSleep(bodies, islands);

            HelPhysicsWakeReason3D lastReason = HelPhysicsWakeReason3D.None;
            int lastCandidateWakeCount = 0;
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < 1024; iteration++) {
                RunWakeAndSleepCycle(sleeper, bodies, islands, candidate, kinematicPair, in manifold);
                lastReason = sleeper.GetWakeEventReason(0);
                lastCandidateWakeCount = sleeper.GetWakeCount(HelPhysicsWakeReason3D.NewCandidateContact);
                sleeper.BeginStep();
                sleeper.EvaluateSleep(bodies, islands);
            }
            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(allocatedBefore, allocatedAfter);
            Assert.Equal(HelPhysicsWakeReason3D.NewCandidateContact, lastReason);
            Assert.Equal(2, lastCandidateWakeCount);
        }

        /// <summary>
        /// Runs one explicit sequence that wakes two prior islands and blocks quiet credit for the wake step.
        /// </summary>
        /// <param name="sleeper">Sleeper whose transient storage and diagnostics are exercised.</param>
        /// <param name="bodies">Pool containing three dynamic bodies and one moving kinematic body.</param>
        /// <param name="islands">Prior dynamic island publication used for propagation.</param>
        /// <param name="candidate">New candidate connecting the two prior dynamic islands.</param>
        /// <param name="kinematicPair">Active dynamic-kinematic contact pair.</param>
        /// <param name="manifold">Active contact manifold for the moving kinematic pair.</param>
        static void RunWakeAndSleepCycle(
            HelPhysicsIslandSleeper3D sleeper,
            HelPhysicsBodyPool3D bodies,
            HelPhysicsIslandBuilder3D islands,
            HelPhysicsCandidatePair3D candidate,
            HelPhysicsPairKey3D kinematicPair,
            in HelPhysicsContactManifold3D manifold) {
            sleeper.BeginStep();
            sleeper.WakeForNewCandidateContact(candidate, bodies, islands);
            sleeper.WakeForMovingKinematicContact(kinematicPair, in manifold, bodies, islands);
            sleeper.WakeForExplicitForce(0, bodies, islands);
            sleeper.WakeForExplicitImpulse(2, bodies, islands);
            sleeper.EvaluateSleep(bodies, islands);
        }

        /// <summary>
        /// Begins a fresh simulation step and evaluates all current islands once.
        /// </summary>
        /// <param name="sleeper">Sleeper to reset and evaluate.</param>
        /// <param name="bodies">Body pool containing current island members.</param>
        /// <param name="islands">Current published island ranges.</param>
        static void EvaluateOneStep(
            HelPhysicsIslandSleeper3D sleeper,
            HelPhysicsBodyPool3D bodies,
            HelPhysicsIslandBuilder3D islands) {
            sleeper.BeginStep();
            sleeper.EvaluateSleep(bodies, islands);
        }

        /// <summary>
        /// Creates a fully occupied pool of sleeping isolated dynamic bodies with identical quiet counters.
        /// </summary>
        /// <param name="bodyCount">Number of occupied dynamic slots to create.</param>
        /// <param name="lowMotionStepCount">Initial quiet count assigned to every body.</param>
        /// <returns>A pool of sleeping dynamics with valid sleep settings.</returns>
        static HelPhysicsBodyPool3D CreateSleepingDynamicBodies(int bodyCount, ushort lowMotionStepCount) {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(bodyCount);
            for (int bodyIndex = 0; bodyIndex < bodyCount; bodyIndex++) {
                bodies.Allocate(
                    CreateSleepingState(lowMotionStepCount),
                    CreateColdState(BodyKind3D.Dynamic, 0.01f, 0.01f, 1));
            }

            return bodies;
        }

        /// <summary>
        /// Creates finite dynamic-compatible state with the requested awake flag.
        /// </summary>
        /// <param name="isAwake">Whether integration currently processes the body.</param>
        /// <returns>Identity-oriented state with zero velocity and unit response values.</returns>
        static HelPhysicsBodyState3D CreateDynamicState(bool isAwake) {
            return new HelPhysicsBodyState3D {
                Orientation = PhysicsQuaternion.Identity,
                InverseMass = PhysicsScalar.One,
                LocalInverseInertia = PhysicsMatrix3x3.Identity,
                GravityScale = PhysicsScalar.One,
                IsAwake = isAwake
            };
        }

        /// <summary>
        /// Creates sleeping dynamic state with an explicit prior quiet duration.
        /// </summary>
        /// <param name="lowMotionStepCount">Prior low-motion step count to preserve or reset.</param>
        /// <returns>Sleeping zero-velocity body state.</returns>
        static HelPhysicsBodyState3D CreateSleepingState(ushort lowMotionStepCount) {
            HelPhysicsBodyState3D state = CreateDynamicState(false);
            state.LowMotionStepCount = lowMotionStepCount;
            return state;
        }

        /// <summary>
        /// Creates complete cold metadata with explicit squared sleep thresholds and required tick count.
        /// </summary>
        /// <param name="bodyKind">Simulation participation mode.</param>
        /// <param name="linearSleepThresholdSquared">Non-negative squared linear speed threshold.</param>
        /// <param name="angularSleepThresholdSquared">Non-negative squared angular speed threshold.</param>
        /// <param name="sleepTicks">Positive quiet duration required before sleep.</param>
        /// <returns>Cold state suitable for island sleep evaluation.</returns>
        static HelPhysicsBodyColdState3D CreateColdState(
            BodyKind3D bodyKind,
            float linearSleepThresholdSquared,
            float angularSleepThresholdSquared,
            ushort sleepTicks) {
            return new HelPhysicsBodyColdState3D(
                default,
                bodyKind,
                new HelPhysicsMaterial3D(PhysicsScalar.Zero, PhysicsScalar.Zero, PhysicsScalar.Zero),
                1,
                ushort.MaxValue,
                0,
                PhysicsScalar.FromFloat(linearSleepThresholdSquared),
                PhysicsScalar.FromFloat(angularSleepThresholdSquared),
                sleepTicks);
        }

        /// <summary>
        /// Builds current islands containing one active dynamic-dynamic contact and all other dynamic bodies as isolated members.
        /// </summary>
        /// <param name="bodies">Body pool whose capacity sizes fixed builder storage.</param>
        /// <param name="firstBodyIndex">First dynamic contact participant.</param>
        /// <param name="secondBodyIndex">Second dynamic contact participant.</param>
        /// <returns>A builder with one successful current publication.</returns>
        static HelPhysicsIslandBuilder3D BuildConnectedPair(
            HelPhysicsBodyPool3D bodies,
            int firstBodyIndex,
            int secondBodyIndex) {
            HelPhysicsIslandBuilder3D islands = new HelPhysicsIslandBuilder3D(bodies.Capacity, bodies.Capacity);
            HelPhysicsPairKey3D[] pairs = new HelPhysicsPairKey3D[] {
                new HelPhysicsPairKey3D(firstBodyIndex, secondBodyIndex)
            };
            islands.Build(
                bodies,
                pairs,
                new HelPhysicsContactManifold3D[] { CreateActiveManifold() },
                1);
            return islands;
        }

        /// <summary>
        /// Builds islands without any active manifolds so every occupied dynamic body remains isolated.
        /// </summary>
        /// <param name="bodies">Body pool whose dynamic occupants become islands.</param>
        /// <param name="islandCapacity">Fixed island capacity to allocate.</param>
        /// <returns>A builder with one successful isolated-body publication.</returns>
        static HelPhysicsIslandBuilder3D BuildIsolatedBodies(
            HelPhysicsBodyPool3D bodies,
            int islandCapacity) {
            HelPhysicsIslandBuilder3D islands = new HelPhysicsIslandBuilder3D(bodies.Capacity, islandCapacity);
            islands.Build(
                bodies,
                Array.Empty<HelPhysicsPairKey3D>(),
                Array.Empty<HelPhysicsContactManifold3D>(),
                0);
            return islands;
        }

        /// <summary>
        /// Creates one manifold with a leading active contact count.
        /// </summary>
        /// <returns>A manifold considered active by island and wake validation.</returns>
        static HelPhysicsContactManifold3D CreateActiveManifold() {
            HelPhysicsContactManifold3D manifold = default;
            manifold.ContactCount = 1;
            return manifold;
        }

        /// <summary>
        /// Verifies that one body is awake and owns the expected synchronized quiet counter.
        /// </summary>
        /// <param name="state">Body state to inspect.</param>
        /// <param name="expectedCounter">Expected low-motion count.</param>
        static void AssertAwakeWithCounter(HelPhysicsBodyState3D state, ushort expectedCounter) {
            Assert.True(state.IsAwake);
            Assert.Equal(expectedCounter, state.LowMotionStepCount);
        }

        /// <summary>
        /// Verifies a sleeping body's synchronized count and complete velocity, force, and torque clearing.
        /// </summary>
        /// <param name="state">Body state expected to have slept atomically with its island.</param>
        /// <param name="expectedCounter">Expected shared quiet duration at transition.</param>
        static void AssertSleepingAndCleared(HelPhysicsBodyState3D state, ushort expectedCounter) {
            Assert.False(state.IsAwake);
            Assert.Equal(expectedCounter, state.LowMotionStepCount);
            AssertVectorZero(state.LinearVelocity);
            AssertVectorZero(state.AngularVelocity);
            AssertVectorZero(state.AccumulatedForce);
            AssertVectorZero(state.AccumulatedTorque);
        }

        /// <summary>
        /// Verifies every component of one physics vector equals the exact scalar zero value.
        /// </summary>
        /// <param name="vector">Vector expected to have been explicitly cleared.</param>
        static void AssertVectorZero(PhysicsVector3 vector) {
            Assert.Equal(PhysicsScalar.Zero, vector.X);
            Assert.Equal(PhysicsScalar.Zero, vector.Y);
            Assert.Equal(PhysicsScalar.Zero, vector.Z);
        }
    }
}
