namespace helengine {
    /// <summary>
    /// Verifies force, damping, pose, and quaternion integration for fixed-pool dynamic bodies.
    /// </summary>
    public sealed class HelPhysicsPoseIntegrator3DTests {
        /// <summary>
        /// Verifies hand-derived force, gravity, rotated inertia, and rational damping results and transient accumulator clearing.
        /// </summary>
        [Fact]
        public void IntegrateVelocity_WithAwakeDynamicBody_AppliesForcesWorldInertiaAndExactDamping() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(1);
            PhysicsQuaternion orientation = PhysicsQuaternion.CreateFromAxisAngle(
                PhysicsVector3.UnitZ,
                PhysicsMath.Pi * PhysicsScalar.FromFloat(0.5f));
            HelPhysicsBodyState3D state = CreateState();
            state.Orientation = orientation;
            state.LinearVelocity = new PhysicsVector3(1f, 2f, 0f);
            state.AngularVelocity = new PhysicsVector3(0f, 2f, 0f);
            state.AccumulatedForce = new PhysicsVector3(4f, 0f, 0f);
            state.AccumulatedTorque = new PhysicsVector3(2f, 4f, 0f);
            state.InverseMass = PhysicsScalar.FromFloat(0.5f);
            state.LocalInverseInertia = PhysicsMatrix3x3.CreateDiagonal(new PhysicsVector3(1f, 2f, 3f));
            state.GravityScale = PhysicsScalar.FromFloat(2f);
            state.LinearDamping = PhysicsScalar.FromFloat(2f);
            state.AngularDamping = PhysicsScalar.FromFloat(0.5f);
            HelPhysicsBodyHandle3D handle = bodies.Allocate(state, CreateColdState(BodyKind3D.Dynamic));
            HelPhysicsBodyIntegrator3D integrator = new HelPhysicsBodyIntegrator3D();
            PhysicsVector3 gravity = new PhysicsVector3(0f, -10f, 0f);

            integrator.IntegrateVelocity(PhysicsScalar.FromFloat(0.5f), in gravity, bodies);

            ref HelPhysicsBodyState3D integrated = ref bodies.GetRequiredState(handle);
            AssertClose(1f, integrated.LinearVelocity.X);
            AssertClose(-4f, integrated.LinearVelocity.Y);
            AssertClose(1.6f, integrated.AngularVelocity.X);
            AssertClose(3.2f, integrated.AngularVelocity.Y);
            Assert.Equal(PhysicsVector3.Zero.X, integrated.AccumulatedForce.X);
            Assert.Equal(PhysicsVector3.Zero.Y, integrated.AccumulatedForce.Y);
            Assert.Equal(PhysicsVector3.Zero.Z, integrated.AccumulatedForce.Z);
            Assert.Equal(PhysicsVector3.Zero.X, integrated.AccumulatedTorque.X);
            Assert.Equal(PhysicsVector3.Zero.Y, integrated.AccumulatedTorque.Y);
            Assert.Equal(PhysicsVector3.Zero.Z, integrated.AccumulatedTorque.Z);
        }

        /// <summary>
        /// Verifies that static, kinematic, and sleeping dynamic bodies retain velocity and accumulated inputs unchanged.
        /// </summary>
        [Fact]
        public void IntegrateVelocity_WithBodiesOutsideAwakeDynamicSet_LeavesTheirStateUnchanged() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(4);
            HelPhysicsBodyState3D staticState = CreateState();
            HelPhysicsBodyState3D kinematicState = CreateState();
            HelPhysicsBodyState3D sleepingState = CreateState();
            sleepingState.IsAwake = false;
            HelPhysicsBodyHandle3D staticHandle = bodies.Allocate(staticState, CreateColdState(BodyKind3D.Static));
            HelPhysicsBodyHandle3D kinematicHandle = bodies.Allocate(kinematicState, CreateColdState(BodyKind3D.Kinematic));
            HelPhysicsBodyHandle3D sleepingHandle = bodies.Allocate(sleepingState, CreateColdState(BodyKind3D.Dynamic));
            HelPhysicsBodyIntegrator3D integrator = new HelPhysicsBodyIntegrator3D();
            PhysicsVector3 gravity = new PhysicsVector3(0f, -10f, 0f);

            integrator.IntegrateVelocity(PhysicsScalar.FromFloat(0.5f), in gravity, bodies);

            AssertUnintegrated(bodies.GetRequiredState(staticHandle));
            AssertUnintegrated(bodies.GetRequiredState(kinematicHandle));
            AssertUnintegrated(bodies.GetRequiredState(sleepingHandle));
        }

        /// <summary>
        /// Verifies semi-implicit position integration and normalized quaternion integration from world angular velocity.
        /// </summary>
        [Fact]
        public void IntegratePose_WithAwakeDynamicBody_AdvancesPositionAndNormalizesOrientation() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(1);
            HelPhysicsBodyState3D state = CreateState();
            state.Position = new PhysicsVector3(3f, 4f, 5f);
            state.Orientation = PhysicsQuaternion.Identity;
            state.LinearVelocity = new PhysicsVector3(2f, -4f, 0f);
            state.AngularVelocity = new PhysicsVector3(0f, 0f, 2f);
            HelPhysicsBodyHandle3D handle = bodies.Allocate(state, CreateColdState(BodyKind3D.Dynamic));
            HelPhysicsPoseIntegrator3D integrator = new HelPhysicsPoseIntegrator3D();

            integrator.IntegratePose(PhysicsScalar.FromFloat(0.5f), bodies);

            ref HelPhysicsBodyState3D integrated = ref bodies.GetRequiredState(handle);
            AssertClose(4f, integrated.Position.X);
            AssertClose(2f, integrated.Position.Y);
            AssertClose(5f, integrated.Position.Z);
            AssertClose(0f, integrated.Orientation.X);
            AssertClose(0f, integrated.Orientation.Y);
            AssertClose(0.4472136f, integrated.Orientation.Z);
            AssertClose(0.8944272f, integrated.Orientation.W);
            PhysicsScalar lengthSquared =
                (integrated.Orientation.X * integrated.Orientation.X) +
                (integrated.Orientation.Y * integrated.Orientation.Y) +
                (integrated.Orientation.Z * integrated.Orientation.Z) +
                (integrated.Orientation.W * integrated.Orientation.W);
            AssertClose(1f, lengthSquared);
        }

        /// <summary>
        /// Verifies that static, kinematic, and sleeping dynamic poses are never advanced by dynamic pose integration.
        /// </summary>
        [Fact]
        public void IntegratePose_WithBodiesOutsideAwakeDynamicSet_LeavesTheirPosesUnchanged() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(3);
            HelPhysicsBodyState3D staticState = CreateState();
            HelPhysicsBodyState3D kinematicState = CreateState();
            HelPhysicsBodyState3D sleepingState = CreateState();
            sleepingState.IsAwake = false;
            HelPhysicsBodyHandle3D staticHandle = bodies.Allocate(staticState, CreateColdState(BodyKind3D.Static));
            HelPhysicsBodyHandle3D kinematicHandle = bodies.Allocate(kinematicState, CreateColdState(BodyKind3D.Kinematic));
            HelPhysicsBodyHandle3D sleepingHandle = bodies.Allocate(sleepingState, CreateColdState(BodyKind3D.Dynamic));
            HelPhysicsPoseIntegrator3D integrator = new HelPhysicsPoseIntegrator3D();

            integrator.IntegratePose(PhysicsScalar.FromFloat(0.5f), bodies);

            AssertPoseUnintegrated(bodies.GetRequiredState(staticHandle));
            AssertPoseUnintegrated(bodies.GetRequiredState(kinematicHandle));
            AssertPoseUnintegrated(bodies.GetRequiredState(sleepingHandle));
        }

        /// <summary>
        /// Verifies that warmed velocity and pose integration loops reuse body-pool storage without managed allocations.
        /// </summary>
        [Fact]
        public void Integrators_AfterWarmup_AllocateNoManagedMemory() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(1);
            bodies.Allocate(CreateState(), CreateColdState(BodyKind3D.Dynamic));
            HelPhysicsBodyIntegrator3D bodyIntegrator = new HelPhysicsBodyIntegrator3D();
            HelPhysicsPoseIntegrator3D poseIntegrator = new HelPhysicsPoseIntegrator3D();
            PhysicsScalar stepSeconds = PhysicsScalar.FromFloat(0.01f);
            PhysicsVector3 gravity = new PhysicsVector3(0f, -10f, 0f);
            bodyIntegrator.IntegrateVelocity(stepSeconds, in gravity, bodies);
            poseIntegrator.IntegratePose(stepSeconds, bodies);

            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < 1024; iteration++) {
                bodyIntegrator.IntegrateVelocity(stepSeconds, in gravity, bodies);
                poseIntegrator.IntegratePose(stepSeconds, bodies);
            }
            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(allocatedBefore, allocatedAfter);
        }

        /// <summary>
        /// Creates populated awake state used to expose accidental integration or accumulator clearing.
        /// </summary>
        /// <returns>One finite dynamic-compatible hot body state.</returns>
        static HelPhysicsBodyState3D CreateState() {
            return new HelPhysicsBodyState3D {
                Position = new PhysicsVector3(1f, 2f, 3f),
                Orientation = PhysicsQuaternion.Identity,
                LinearVelocity = new PhysicsVector3(4f, 5f, 6f),
                AngularVelocity = new PhysicsVector3(0.5f, 1f, 1.5f),
                AccumulatedForce = new PhysicsVector3(2f, 3f, 4f),
                AccumulatedTorque = new PhysicsVector3(5f, 6f, 7f),
                InverseMass = PhysicsScalar.One,
                LocalInverseInertia = PhysicsMatrix3x3.Identity,
                GravityScale = PhysicsScalar.One,
                LinearDamping = PhysicsScalar.Zero,
                AngularDamping = PhysicsScalar.Zero,
                IsAwake = true
            };
        }

        /// <summary>
        /// Creates cold metadata with an explicit body kind and neutral contact material.
        /// </summary>
        /// <param name="bodyKind">Simulation motion kind to associate with the test state.</param>
        /// <returns>Cold metadata suitable for one test body.</returns>
        static HelPhysicsBodyColdState3D CreateColdState(BodyKind3D bodyKind) {
            return new HelPhysicsBodyColdState3D {
                BodyKind = bodyKind,
                Material = new HelPhysicsMaterial3D(PhysicsScalar.Zero, PhysicsScalar.Zero, PhysicsScalar.Zero),
                CollisionLayer = 1,
                CollisionMask = ushort.MaxValue
            };
        }

        /// <summary>
        /// Verifies the populated velocity and accumulator values used by non-integrated body tests.
        /// </summary>
        /// <param name="state">Body state expected to match <see cref="CreateState"/>.</param>
        static void AssertUnintegrated(HelPhysicsBodyState3D state) {
            AssertClose(4f, state.LinearVelocity.X);
            AssertClose(5f, state.LinearVelocity.Y);
            AssertClose(6f, state.LinearVelocity.Z);
            AssertClose(2f, state.AccumulatedForce.X);
            AssertClose(5f, state.AccumulatedTorque.X);
        }

        /// <summary>
        /// Verifies the populated pose values used by non-integrated body tests.
        /// </summary>
        /// <param name="state">Body state expected to retain its original position and orientation.</param>
        static void AssertPoseUnintegrated(HelPhysicsBodyState3D state) {
            AssertClose(1f, state.Position.X);
            AssertClose(2f, state.Position.Y);
            AssertClose(3f, state.Position.Z);
            AssertClose(0f, state.Orientation.X);
            AssertClose(0f, state.Orientation.Y);
            AssertClose(0f, state.Orientation.Z);
            AssertClose(1f, state.Orientation.W);
        }

        /// <summary>
        /// Verifies one scalar against a hand-derived float expectation within single-precision integration tolerance.
        /// </summary>
        /// <param name="expected">Hand-derived expected value.</param>
        /// <param name="actual">Physics scalar produced by the integration path.</param>
        static void AssertClose(float expected, PhysicsScalar actual) {
            Assert.InRange(actual.ToFloat(), expected - 0.0001f, expected + 0.0001f);
        }
    }
}
