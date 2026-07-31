namespace helengine {
    /// <summary>
    /// Verifies prepared contact effective masses, warm starting, sequential impulses, friction, restitution, and solved writeback.
    /// </summary>
    public sealed class HelPhysicsContactSolver3DTests {
        /// <summary>
        /// Verifies that one inelastic static contact stops an approaching dynamic body along the contact normal.
        /// </summary>
        [Fact]
        public void SolveVelocityIteration_WithDownwardDynamicBody_StopsNormalMotion() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            HelPhysicsBodyHandle3D dynamicHandle = bodies.Allocate(
                CreateDynamicState(new PhysicsVector3(0f, -2f, 0f)),
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero));
            HelPhysicsPairKey3D[] pairs = CreatePairArray();
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);

            solver.Prepare(PhysicsScalar.FromFloat(0.02f), bodies, pairs, manifolds, 1);
            solver.SolveVelocityIteration(bodies);
            solver.WriteBack(manifolds);

            ref HelPhysicsBodyState3D dynamicState = ref bodies.GetRequiredState(dynamicHandle);
            AssertClose(0f, dynamicState.LinearVelocity.Y);
            AssertClose(2f, manifolds[0].GetContact(0).AccumulatedNormalImpulse);
        }

        /// <summary>
        /// Verifies a symmetric four-corner face patch does not turn purely normal motion into lateral or angular motion.
        /// </summary>
        [Fact]
        public void SolveVelocityIteration_WithSymmetricFacePatch_PreservesLateralAndAngularSymmetry() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0.8f, 0.6f, 0f));
            HelPhysicsBodyHandle3D dynamicHandle = bodies.Allocate(
                CreateDynamicState(new PhysicsVector3(0f, -0.5f, 0f)),
                CreateColdState(BodyKind3D.Dynamic, 0.8f, 0.6f, 0f));
            HelPhysicsContactManifold3D[] manifolds = new HelPhysicsContactManifold3D[] {
                CreateSymmetricFaceManifold()
            };
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(4);

            solver.Prepare(
                PhysicsScalar.FromFloat(0.05f),
                bodies,
                CreatePairArray(),
                manifolds,
                1);
            for (int iterationIndex = 0; iterationIndex < 4; iterationIndex++) {
                solver.SolveVelocityIteration(bodies);
            }

            ref HelPhysicsBodyState3D dynamicState = ref bodies.GetRequiredState(dynamicHandle);
            AssertClose(0f, dynamicState.LinearVelocity.X);
            AssertClose(0f, dynamicState.LinearVelocity.Y);
            AssertClose(0f, dynamicState.LinearVelocity.Z);
            AssertClose(0f, dynamicState.AngularVelocity.X);
            AssertClose(0f, dynamicState.AngularVelocity.Y);
            AssertClose(0f, dynamicState.AngularVelocity.Z);
        }

        /// <summary>
        /// Verifies centered four-contact patches remain solvable when valid inverse mass scales below the former absolute pivot cutoff.
        /// </summary>
        /// <param name="mass">Large finite dynamic mass whose reciprocal controls every response coefficient.</param>
        [Theory]
        [InlineData(1000000f)]
        [InlineData(10000000f)]
        [InlineData(100000000f)]
        [InlineData(1000000000f)]
        public void SolveVelocityIteration_WithHighMassCenteredPatch_StopsNormalMotion(float mass) {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            HelPhysicsBodyHandle3D dynamicHandle = bodies.Allocate(
                CreateDynamicStateWithMass(new PhysicsVector3(0f, -0.5f, 0f), mass),
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactManifold3D[] manifolds = new HelPhysicsContactManifold3D[] {
                CreateSymmetricFaceManifold()
            };
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(4);

            solver.Prepare(PhysicsScalar.FromFloat(0.05f), bodies, CreatePairArray(), manifolds, 1);
            for (int iterationIndex = 0; iterationIndex < 4; iterationIndex++) {
                solver.SolveVelocityIteration(bodies);
            }

            ref HelPhysicsBodyState3D dynamicState = ref bodies.GetRequiredState(dynamicHandle);
            AssertClose(0f, dynamicState.LinearVelocity.Y);
            AssertClose(0f, dynamicState.AngularVelocity.X);
            AssertClose(0f, dynamicState.AngularVelocity.Z);
        }

        /// <summary>
        /// Verifies a high-mass asymmetric patch produces finite coupled linear and angular response instead of rejecting its scale.
        /// </summary>
        [Fact]
        public void SolveVelocityIteration_WithHighMassAsymmetricPatch_ProducesOffCenterResponseWithoutThrowing() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            HelPhysicsBodyHandle3D dynamicHandle = bodies.Allocate(
                CreateDynamicStateWithMass(new PhysicsVector3(0f, -0.5f, 0f), 1000000000f),
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactManifold3D[] manifolds = new HelPhysicsContactManifold3D[] {
                CreateAsymmetricFaceManifold()
            };
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(2);

            solver.Prepare(PhysicsScalar.FromFloat(0.05f), bodies, CreatePairArray(), manifolds, 1);
            for (int iterationIndex = 0; iterationIndex < 4; iterationIndex++) {
                solver.SolveVelocityIteration(bodies);
            }

            ref HelPhysicsBodyState3D dynamicState = ref bodies.GetRequiredState(dynamicHandle);
            Assert.True(dynamicState.LinearVelocity.Y > PhysicsScalar.FromFloat(-0.5f));
            Assert.NotEqual(PhysicsScalar.Zero, dynamicState.AngularVelocity.Z);
        }

        /// <summary>
        /// Verifies equal high-mass dynamic participants exchange a symmetric four-contact impulse without static-body assumptions.
        /// </summary>
        [Fact]
        public void SolveVelocityIteration_WithHighMassDynamicDynamicPatch_PreservesMomentumAndStopsRelativeMotion() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            HelPhysicsBodyHandle3D firstHandle = bodies.Allocate(
                CreateDynamicStateWithMass(new PhysicsVector3(0f, 0.25f, 0f), 1000000000f),
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsBodyHandle3D secondHandle = bodies.Allocate(
                CreateDynamicStateWithMass(new PhysicsVector3(0f, -0.25f, 0f), 1000000000f),
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactManifold3D[] manifolds = new HelPhysicsContactManifold3D[] {
                CreateSymmetricFaceManifold()
            };
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(4);

            solver.Prepare(PhysicsScalar.FromFloat(0.05f), bodies, CreatePairArray(), manifolds, 1);
            for (int iterationIndex = 0; iterationIndex < 4; iterationIndex++) {
                solver.SolveVelocityIteration(bodies);
            }

            ref HelPhysicsBodyState3D firstState = ref bodies.GetRequiredState(firstHandle);
            ref HelPhysicsBodyState3D secondState = ref bodies.GetRequiredState(secondHandle);
            AssertClose(0f, firstState.LinearVelocity.Y);
            AssertClose(0f, secondState.LinearVelocity.Y);
            Assert.Equal(firstState.LinearVelocity.Y, -secondState.LinearVelocity.Y);
        }

        /// <summary>
        /// Verifies every numeric normal-block scratch array is governed by the physics scalar backend rather than raw floating-point storage.
        /// </summary>
        [Fact]
        public void NormalBlockScratch_UsesPhysicsScalarBackendForEveryNumericArray() {
            AssertPhysicsScalarScratchField("NormalBlockMatrix");
            AssertPhysicsScalarScratchField("NormalBlockConstants");
            AssertPhysicsScalarScratchField("NormalBlockOldImpulses");
            AssertPhysicsScalarScratchField("NormalBlockCandidateImpulses");
            AssertPhysicsScalarScratchField("NormalBlockWorkingMatrix");
            AssertPhysicsScalarScratchField("NormalBlockWorkingRightHandSide");
            AssertPhysicsScalarScratchField("NormalBlockWorkingSolution");
        }

        /// <summary>
        /// Verifies that restitution uses the larger material coefficient when impact speed is strictly below the threshold.
        /// </summary>
        [Fact]
        public void SolveVelocityIteration_WithImpactBelowThreshold_AppliesMaximumRestitution() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0.25f));
            HelPhysicsBodyHandle3D dynamicHandle = bodies.Allocate(
                CreateDynamicState(new PhysicsVector3(0f, -4f, 0f)),
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0.5f));
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero));
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);

            SolveOneIteration(solver, bodies, manifolds);

            AssertClose(2f, bodies.GetRequiredState(dynamicHandle).LinearVelocity.Y);
        }

        /// <summary>
        /// Verifies that an impact exactly at negative one receives no restitution bias because the threshold comparison is strict.
        /// </summary>
        [Fact]
        public void SolveVelocityIteration_WithImpactAtExactThreshold_DoesNotBounce() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 1f));
            HelPhysicsBodyHandle3D dynamicHandle = bodies.Allocate(
                CreateDynamicState(new PhysicsVector3(0f, -1f, 0f)),
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 1f));
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero));
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);

            SolveOneIteration(solver, bodies, manifolds);

            AssertClose(0f, bodies.GetRequiredState(dynamicHandle).LinearVelocity.Y);
        }

        /// <summary>
        /// Verifies that a tangential impulse inside the geometrically combined static cone cancels sliding completely.
        /// </summary>
        [Fact]
        public void SolveVelocityIteration_WithTangentialDemandInsideStaticCone_StopsSliding() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 1f, 0.5f, 0f));
            HelPhysicsBodyHandle3D dynamicHandle = bodies.Allocate(
                CreateDynamicState(new PhysicsVector3(0.75f, -2f, 0f)),
                CreateColdState(BodyKind3D.Dynamic, 0.25f, 0.5f, 0f));
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero));
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);

            SolveOneIteration(solver, bodies, manifolds);
            solver.WriteBack(manifolds);

            AssertClose(0f, bodies.GetRequiredState(dynamicHandle).LinearVelocity.X);
            AssertClose(-0.75f, manifolds[0].GetContact(0).AccumulatedTangentImpulse1);
        }

        /// <summary>
        /// Verifies that sliding friction is clamped to the geometric dynamic cone after static friction is exceeded.
        /// </summary>
        [Fact]
        public void SolveVelocityIteration_WithTangentialDemandOutsideStaticCone_ClampsToDynamicCone() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 1f, 1f, 0f));
            HelPhysicsBodyHandle3D dynamicHandle = bodies.Allocate(
                CreateDynamicState(new PhysicsVector3(1.1f, -2f, 0f)),
                CreateColdState(BodyKind3D.Dynamic, 0.25f, 0.04f, 0f));
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero));
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);

            SolveOneIteration(solver, bodies, manifolds);
            solver.WriteBack(manifolds);

            AssertClose(0.7f, bodies.GetRequiredState(dynamicHandle).LinearVelocity.X);
            AssertClose(-0.4f, manifolds[0].GetContact(0).AccumulatedTangentImpulse1);
        }

        /// <summary>
        /// Verifies maximum finite friction coefficients combine during preparation without overflowing an intermediate product.
        /// </summary>
        [Fact]
        public void Prepare_WithMaximumFiniteFriction_DoesNotOverflow() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(
                CreateStaticState(),
                CreateColdState(BodyKind3D.Static, float.MaxValue, float.MaxValue, 0f));
            bodies.Allocate(
                CreateDynamicState(new PhysicsVector3(0.5f, -1f, 0f)),
                CreateColdState(BodyKind3D.Dynamic, float.MaxValue, float.MaxValue, 0f));
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero));
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);

            solver.Prepare(PhysicsScalar.FromFloat(0.02f), bodies, CreatePairArray(), manifolds, 1);
            solver.WriteBack(manifolds);

            Assert.Equal((uint)1, manifolds[0].GetContact(0).Feature.Value);
        }

        /// <summary>
        /// Verifies that an off-center contact uses orientation-derived world inertia for both effective mass and angular response.
        /// </summary>
        [Fact]
        public void SolveVelocityIteration_WithRotatedOffCenterContact_AppliesAngularImpulseUsingWorldInertia() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            HelPhysicsBodyState3D dynamicState = CreateDynamicState(new PhysicsVector3(0f, -2f, 0f));
            dynamicState.Orientation = PhysicsQuaternion.CreateFromAxisAngle(
                PhysicsVector3.UnitY,
                PhysicsMath.Pi * PhysicsScalar.FromFloat(0.5f));
            dynamicState.LocalInverseInertia = PhysicsMatrix3x3.CreateDiagonal(new PhysicsVector3(1f, 1f, 4f));
            HelPhysicsBodyHandle3D dynamicHandle = bodies.Allocate(
                dynamicState,
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(CreateContact(PhysicsVector3.Zero, PhysicsVector3.UnitZ));
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);

            SolveOneIteration(solver, bodies, manifolds);

            ref HelPhysicsBodyState3D solvedState = ref bodies.GetRequiredState(dynamicHandle);
            AssertClose(-1f, solvedState.LinearVelocity.Y);
            AssertClose(1f, solvedState.AngularVelocity.Z);
        }

        /// <summary>
        /// Verifies that warm starting applies all three cached impulses in the deterministic contact basis before iterations run.
        /// </summary>
        [Fact]
        public void WarmStart_WithCachedNormalAndTangentImpulses_AppliesCombinedImpulse() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 1f, 1f, 0f));
            HelPhysicsBodyHandle3D dynamicHandle = bodies.Allocate(
                CreateDynamicState(PhysicsVector3.Zero),
                CreateColdState(BodyKind3D.Dynamic, 1f, 1f, 0f));
            HelPhysicsContactPoint3D contact = CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero);
            contact.AccumulatedNormalImpulse = PhysicsScalar.FromFloat(2f);
            contact.AccumulatedTangentImpulse0 = PhysicsScalar.FromFloat(3f);
            contact.AccumulatedTangentImpulse1 = PhysicsScalar.FromFloat(-1f);
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(contact);
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);

            solver.Prepare(PhysicsScalar.FromFloat(0.02f), bodies, CreatePairArray(), manifolds, 1);
            solver.WarmStart(bodies);

            ref HelPhysicsBodyState3D warmedState = ref bodies.GetRequiredState(dynamicHandle);
            AssertClose(-1f, warmedState.LinearVelocity.X);
            AssertClose(2f, warmedState.LinearVelocity.Y);
            AssertClose(3f, warmedState.LinearVelocity.Z);
        }

        /// <summary>
        /// Verifies that a separating contact cannot accumulate a negative normal impulse that pulls bodies together.
        /// </summary>
        [Fact]
        public void SolveVelocityIteration_WithSeparatingBody_DoesNotApplyAttractiveImpulse() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            HelPhysicsBodyHandle3D dynamicHandle = bodies.Allocate(
                CreateDynamicState(new PhysicsVector3(0f, 2f, 0f)),
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero));
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);

            SolveOneIteration(solver, bodies, manifolds);
            solver.WriteBack(manifolds);

            AssertClose(2f, bodies.GetRequiredState(dynamicHandle).LinearVelocity.Y);
            AssertClose(0f, manifolds[0].GetContact(0).AccumulatedNormalImpulse);
        }

        /// <summary>
        /// Verifies impulse signs when the responsive dynamic participant is body A rather than body B.
        /// </summary>
        [Fact]
        public void SolveVelocityIteration_WithDynamicBodyA_StopsMotionTowardStaticBodyB() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            HelPhysicsBodyHandle3D dynamicHandle = bodies.Allocate(
                CreateDynamicState(new PhysicsVector3(0f, 2f, 0f)),
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero));
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);

            SolveOneIteration(solver, bodies, manifolds);
            solver.WriteBack(manifolds);

            AssertClose(0f, bodies.GetRequiredState(dynamicHandle).LinearVelocity.Y);
            AssertClose(2f, manifolds[0].GetContact(0).AccumulatedNormalImpulse);
        }

        /// <summary>
        /// Verifies equal dynamic bodies exchange equal-and-opposite normal response through their summed effective mass.
        /// </summary>
        [Fact]
        public void SolveVelocityIteration_WithTwoDynamicBodies_StopsEqualClosingMotion() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            HelPhysicsBodyHandle3D bodyAHandle = bodies.Allocate(
                CreateDynamicState(new PhysicsVector3(0f, 1f, 0f)),
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsBodyHandle3D bodyBHandle = bodies.Allocate(
                CreateDynamicState(new PhysicsVector3(0f, -1f, 0f)),
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero));
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);

            SolveOneIteration(solver, bodies, manifolds);
            solver.WriteBack(manifolds);

            AssertClose(0f, bodies.GetRequiredState(bodyAHandle).LinearVelocity.Y);
            AssertClose(0f, bodies.GetRequiredState(bodyBHandle).LinearVelocity.Y);
            AssertClose(1f, manifolds[0].GetContact(0).AccumulatedNormalImpulse);
        }

        /// <summary>
        /// Verifies kinematic point velocity drives dynamic response while the kinematic body receives no solver mutation.
        /// </summary>
        [Fact]
        public void SolveVelocityIteration_WithMovingKinematicBody_ContributesVelocityWithoutMutation() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            HelPhysicsBodyHandle3D kinematicHandle = bodies.Allocate(
                CreateDynamicState(new PhysicsVector3(0f, 2f, 0f)),
                CreateColdState(BodyKind3D.Kinematic, 0f, 0f, 0f));
            HelPhysicsBodyHandle3D dynamicHandle = bodies.Allocate(
                CreateDynamicState(PhysicsVector3.Zero),
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero));
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);

            SolveOneIteration(solver, bodies, manifolds);

            AssertClose(2f, bodies.GetRequiredState(kinematicHandle).LinearVelocity.Y);
            AssertClose(2f, bodies.GetRequiredState(dynamicHandle).LinearVelocity.Y);
        }

        /// <summary>
        /// Verifies a contact with no responsive inverse mass produces no impulse or body mutation.
        /// </summary>
        [Fact]
        public void SolveVelocityIteration_WithZeroEffectiveMass_LeavesBothBodiesAndImpulseUnchanged() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            HelPhysicsBodyHandle3D bodyAHandle = bodies.Allocate(
                CreateDynamicState(new PhysicsVector3(0f, 2f, 0f)),
                CreateColdState(BodyKind3D.Kinematic, 0f, 0f, 0f));
            HelPhysicsBodyHandle3D bodyBHandle = bodies.Allocate(
                CreateDynamicState(PhysicsVector3.Zero),
                CreateColdState(BodyKind3D.Kinematic, 0f, 0f, 0f));
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero));
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);

            SolveOneIteration(solver, bodies, manifolds);
            solver.WriteBack(manifolds);

            AssertClose(2f, bodies.GetRequiredState(bodyAHandle).LinearVelocity.Y);
            AssertClose(0f, bodies.GetRequiredState(bodyBHandle).LinearVelocity.Y);
            AssertClose(0f, manifolds[0].GetContact(0).AccumulatedNormalImpulse);
        }

        /// <summary>
        /// Verifies solved friction along deterministic tangent zero cancels sliding on the world Z axis.
        /// </summary>
        [Fact]
        public void SolveVelocityIteration_WithTangentZeroSliding_StopsWorldZMotion() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 1f, 0.5f, 0f));
            HelPhysicsBodyHandle3D dynamicHandle = bodies.Allocate(
                CreateDynamicState(new PhysicsVector3(0f, -2f, 0.75f)),
                CreateColdState(BodyKind3D.Dynamic, 1f, 0.5f, 0f));
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero));
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);

            SolveOneIteration(solver, bodies, manifolds);
            solver.WriteBack(manifolds);

            AssertClose(0f, bodies.GetRequiredState(dynamicHandle).LinearVelocity.Z);
            AssertClose(-0.75f, manifolds[0].GetContact(0).AccumulatedTangentImpulse0);
            AssertClose(0f, manifolds[0].GetContact(0).AccumulatedTangentImpulse1);
        }

        /// <summary>
        /// Verifies manifold and inline-contact mappings restore each distinct prepared impulse to its exact destination.
        /// </summary>
        [Fact]
        public void WriteBack_WithMultipleManifoldsAndContacts_MapsEveryPreparedImpulseExactly() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(4);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            bodies.Allocate(CreateDynamicState(PhysicsVector3.Zero), CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            bodies.Allocate(CreateDynamicState(PhysicsVector3.Zero), CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactPoint3D contact0 = CreateContactWithData(
                41u,
                PhysicsVector3.UnitY,
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                0.1f);
            contact0.AccumulatedNormalImpulse = PhysicsScalar.FromFloat(1f);
            contact0.AccumulatedTangentImpulse0 = PhysicsScalar.FromFloat(2f);
            contact0.AccumulatedTangentImpulse1 = PhysicsScalar.FromFloat(3f);
            HelPhysicsContactPoint3D contact1 = CreateContactWithData(
                42u,
                PhysicsVector3.UnitY,
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                0.1f);
            contact1.AccumulatedNormalImpulse = PhysicsScalar.FromFloat(4f);
            contact1.AccumulatedTangentImpulse0 = PhysicsScalar.FromFloat(5f);
            contact1.AccumulatedTangentImpulse1 = PhysicsScalar.FromFloat(6f);
            HelPhysicsContactPoint3D contact2 = CreateContactWithData(
                43u,
                PhysicsVector3.UnitY,
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                0.1f);
            contact2.AccumulatedNormalImpulse = PhysicsScalar.FromFloat(7f);
            contact2.AccumulatedTangentImpulse0 = PhysicsScalar.FromFloat(8f);
            contact2.AccumulatedTangentImpulse1 = PhysicsScalar.FromFloat(9f);
            HelPhysicsContactManifold3D firstManifold = default;
            firstManifold.ContactCount = 2;
            firstManifold.SetContact(0, in contact0);
            firstManifold.SetContact(1, in contact1);
            HelPhysicsContactManifold3D[] manifolds = new HelPhysicsContactManifold3D[] {
                firstManifold,
                CreateManifold(contact2)
            };
            HelPhysicsPairKey3D[] pairs = new HelPhysicsPairKey3D[] {
                new HelPhysicsPairKey3D(0, 1),
                new HelPhysicsPairKey3D(2, 3)
            };
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(3);
            solver.Prepare(PhysicsScalar.FromFloat(0.02f), bodies, pairs, manifolds, 2);
            contact0.AccumulatedNormalImpulse = PhysicsScalar.Zero;
            contact0.AccumulatedTangentImpulse0 = PhysicsScalar.Zero;
            contact0.AccumulatedTangentImpulse1 = PhysicsScalar.Zero;
            contact1.AccumulatedNormalImpulse = PhysicsScalar.Zero;
            contact1.AccumulatedTangentImpulse0 = PhysicsScalar.Zero;
            contact1.AccumulatedTangentImpulse1 = PhysicsScalar.Zero;
            contact2.AccumulatedNormalImpulse = PhysicsScalar.Zero;
            contact2.AccumulatedTangentImpulse0 = PhysicsScalar.Zero;
            contact2.AccumulatedTangentImpulse1 = PhysicsScalar.Zero;
            manifolds[0].SetContact(0, in contact0);
            manifolds[0].SetContact(1, in contact1);
            manifolds[1].SetContact(0, in contact2);

            solver.WriteBack(manifolds);

            AssertContactImpulses(1f, 2f, 3f, manifolds[0].GetContact(0));
            AssertContactImpulses(4f, 5f, 6f, manifolds[0].GetContact(1));
            AssertContactImpulses(7f, 8f, 9f, manifolds[1].GetContact(0));
        }

        /// <summary>
        /// Verifies exact slop and correction fraction against a static contact without modifying kinetic velocity or cached impulses.
        /// </summary>
        [Fact]
        public void CorrectPenetration_WithCentralStaticContact_ChangesOnlyDynamicPose() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            HelPhysicsBodyState3D dynamicState = CreateDynamicState(new PhysicsVector3(1f, -2f, 3f));
            dynamicState.AngularVelocity = new PhysicsVector3(4f, 5f, 6f);
            HelPhysicsBodyHandle3D dynamicHandle = bodies.Allocate(
                dynamicState,
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactPoint3D contact = CreateContact(
                new PhysicsVector3(0f, 0.105f, 0f),
                PhysicsVector3.Zero);
            contact.PenetrationDepth = PhysicsScalar.FromFloat(0.105f);
            contact.AccumulatedNormalImpulse = PhysicsScalar.FromFloat(0.7f);
            contact.AccumulatedTangentImpulse0 = PhysicsScalar.FromFloat(0.2f);
            contact.AccumulatedTangentImpulse1 = PhysicsScalar.FromFloat(-0.3f);
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(contact);
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);
            solver.Prepare(PhysicsScalar.FromFloat(0.02f), bodies, CreatePairArray(), manifolds, 1);

            solver.CorrectPenetration(bodies);
            solver.WriteBack(manifolds);

            ref HelPhysicsBodyState3D correctedState = ref bodies.GetRequiredState(dynamicHandle);
            AssertClose(0.02f, correctedState.Position.Y);
            AssertClose(1f, correctedState.LinearVelocity.X);
            AssertClose(-2f, correctedState.LinearVelocity.Y);
            AssertClose(3f, correctedState.LinearVelocity.Z);
            AssertClose(4f, correctedState.AngularVelocity.X);
            AssertClose(5f, correctedState.AngularVelocity.Y);
            AssertClose(6f, correctedState.AngularVelocity.Z);
            AssertClose(0.7f, manifolds[0].GetContact(0).AccumulatedNormalImpulse);
            AssertClose(0.2f, manifolds[0].GetContact(0).AccumulatedTangentImpulse0);
            AssertClose(-0.3f, manifolds[0].GetContact(0).AccumulatedTangentImpulse1);
        }

        /// <summary>
        /// Verifies repeated correction rebuilds current contact geometry so overlap converges monotonically without creating separation.
        /// </summary>
        /// <param name="passCount">Number of correction passes to execute against updated poses.</param>
        /// <param name="expectedCenterY">Analytical center after removing twenty percent of remaining overlap beyond slop each pass.</param>
        [Theory]
        [InlineData(1, 0.919f)]
        [InlineData(2, 0.9342f)]
        [InlineData(5, 0.9638704f)]
        [InlineData(10, 0.9847995f)]
        public void CorrectPenetration_WithRepeatedPasses_ConvergesFromCurrentAnchors(
            int passCount,
            float expectedCenterY) {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            HelPhysicsBodyState3D dynamicState = CreateDynamicState(new PhysicsVector3(0.25f, 0f, 0f));
            dynamicState.Position = new PhysicsVector3(0f, 0.9f, 0f);
            HelPhysicsBodyHandle3D dynamicHandle = bodies.Allocate(
                dynamicState,
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactPoint3D contact = CreateContactWithData(
                41u,
                PhysicsVector3.UnitY,
                new PhysicsVector3(0f, 0.5f, 0f),
                new PhysicsVector3(0f, -0.5f, 0f),
                0.1f);
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(contact);
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);
            solver.Prepare(PhysicsScalar.FromFloat(0.05f), bodies, CreatePairArray(), manifolds, 1);

            for (int passIndex = 0; passIndex < passCount; passIndex++) {
                solver.CorrectPenetration(bodies);
            }

            ref HelPhysicsBodyState3D corrected = ref bodies.GetRequiredState(dynamicHandle);
            Assert.InRange(corrected.Position.Y.ToFloat(), expectedCenterY - 0.0002f, expectedCenterY + 0.0002f);
            Assert.True(corrected.Position.Y <= PhysicsScalar.FromFloat(0.995f));
            Assert.Equal(PhysicsScalar.FromFloat(0.25f), corrected.LinearVelocity.X);
            Assert.Equal(PhysicsScalar.Zero, corrected.LinearVelocity.Y);
            Assert.Equal(PhysicsScalar.Zero, corrected.AngularVelocity.Z);
        }

        /// <summary>
        /// Verifies that equal responsive bodies share central positional separation according to inverse mass.
        /// </summary>
        [Fact]
        public void CorrectPenetration_WithTwoEqualDynamicBodies_SplitsPositionChangeEvenly() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            HelPhysicsBodyHandle3D bodyAHandle = bodies.Allocate(
                CreateDynamicState(PhysicsVector3.Zero),
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsBodyHandle3D bodyBHandle = bodies.Allocate(
                CreateDynamicState(PhysicsVector3.Zero),
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactPoint3D contact = CreateContact(
                new PhysicsVector3(0f, 0.0525f, 0f),
                new PhysicsVector3(0f, -0.0525f, 0f));
            contact.PenetrationDepth = PhysicsScalar.FromFloat(0.105f);
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(contact);
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);
            solver.Prepare(PhysicsScalar.FromFloat(0.02f), bodies, CreatePairArray(), manifolds, 1);

            solver.CorrectPenetration(bodies);

            AssertClose(-0.01f, bodies.GetRequiredState(bodyAHandle).Position.Y);
            AssertClose(0.01f, bodies.GetRequiredState(bodyBHandle).Position.Y);
        }

        /// <summary>
        /// Verifies that one correction pass cannot separate a deeply penetrating contact by more than the exact maximum.
        /// </summary>
        [Fact]
        public void CorrectPenetration_WithDeepOverlap_ClampsMaximumCorrection() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            HelPhysicsBodyHandle3D dynamicHandle = bodies.Allocate(
                CreateDynamicState(PhysicsVector3.Zero),
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactPoint3D contact = CreateContact(
                new PhysicsVector3(0f, 2f, 0f),
                PhysicsVector3.Zero);
            contact.PenetrationDepth = PhysicsScalar.FromFloat(2f);
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(contact);
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);
            solver.Prepare(PhysicsScalar.FromFloat(0.02f), bodies, CreatePairArray(), manifolds, 1);

            solver.CorrectPenetration(bodies);

            AssertClose(0.2f, bodies.GetRequiredState(dynamicHandle).Position.Y);
        }

        /// <summary>
        /// Verifies that off-center positional correction rotates a responsive body without injecting angular velocity.
        /// </summary>
        [Fact]
        public void CorrectPenetration_WithOffCenterContact_ChangesOrientationButNotAngularVelocity() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            HelPhysicsBodyHandle3D dynamicHandle = bodies.Allocate(
                CreateDynamicState(PhysicsVector3.Zero),
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactPoint3D contact = CreateContact(
                new PhysicsVector3(0f, 0.105f, 0f),
                PhysicsVector3.UnitX);
            contact.PenetrationDepth = PhysicsScalar.FromFloat(0.105f);
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(contact);
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);
            solver.Prepare(PhysicsScalar.FromFloat(0.02f), bodies, CreatePairArray(), manifolds, 1);

            solver.CorrectPenetration(bodies);

            ref HelPhysicsBodyState3D correctedState = ref bodies.GetRequiredState(dynamicHandle);
            AssertClose(0.01f, correctedState.Position.Y);
            AssertClose(0.00499994f, correctedState.Orientation.Z);
            AssertClose(0.9999875f, correctedState.Orientation.W);
            AssertClose(0f, correctedState.AngularVelocity.Z);
        }

        /// <summary>
        /// Verifies that every warmed solver phase reuses constructor-owned arrays without managed allocation.
        /// </summary>
        [Fact]
        public void SolverPhases_AfterWarmup_AllocateNoManagedMemory() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0.5f, 0.25f, 0f));
            bodies.Allocate(
                CreateDynamicState(new PhysicsVector3(1f, -2f, 0f)),
                CreateColdState(BodyKind3D.Dynamic, 0.5f, 0.25f, 0f));
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero));
            HelPhysicsPairKey3D[] pairs = CreatePairArray();
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);
            PhysicsScalar stepSeconds = PhysicsScalar.FromFloat(0.02f);
            solver.Prepare(stepSeconds, bodies, pairs, manifolds, 1);
            solver.WarmStart(bodies);
            solver.SolveVelocityIteration(bodies);
            solver.CorrectPenetration(bodies);
            solver.WriteBack(manifolds);

            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < 1024; iteration++) {
                solver.Prepare(stepSeconds, bodies, pairs, manifolds, 1);
                solver.WarmStart(bodies);
                solver.SolveVelocityIteration(bodies);
                solver.CorrectPenetration(bodies);
                solver.WriteBack(manifolds);
            }
            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(allocatedBefore, allocatedAfter);
        }

        /// <summary>
        /// Verifies that preparation diagnoses contact demand beyond constructor-owned fixed constraint storage.
        /// </summary>
        [Fact]
        public void Prepare_WhenContactsExceedFixedCapacity_ThrowsExactCapacityException() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            bodies.Allocate(CreateDynamicState(new PhysicsVector3(0f, -1f, 0f)), CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactManifold3D manifold = default;
            manifold.ContactCount = 2;
            HelPhysicsContactPoint3D contact = CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero);
            manifold.SetContact(0, in contact);
            manifold.SetContact(1, in contact);
            HelPhysicsContactManifold3D[] manifolds = new HelPhysicsContactManifold3D[] { manifold };
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);

            HelPhysicsCapacityExceededException exception = Assert.Throws<HelPhysicsCapacityExceededException>(
                () => solver.Prepare(PhysicsScalar.FromFloat(0.02f), bodies, CreatePairArray(), manifolds, 1));

            Assert.Equal("solver constraint", exception.PoolName);
            Assert.Equal(1, exception.Capacity);
        }

        /// <summary>
        /// Verifies that a later invalid pair cannot replace constraints prepared and solved by the preceding successful call.
        /// </summary>
        [Fact]
        public void Prepare_WithInvalidLaterPair_PreservesPreviouslyPreparedConstraints() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(4);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            bodies.Allocate(
                CreateDynamicState(new PhysicsVector3(0f, -2f, 0f)),
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            bodies.Allocate(
                CreateDynamicState(new PhysicsVector3(0f, -3f, 0f)),
                CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactManifold3D[] previousManifolds = CreateManifoldArray(
                CreateContactWithData(11u, PhysicsVector3.UnitY, PhysicsVector3.Zero, PhysicsVector3.Zero, 0.1f));
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(2);
            solver.Prepare(
                PhysicsScalar.FromFloat(0.02f),
                bodies,
                new HelPhysicsPairKey3D[] { new HelPhysicsPairKey3D(0, 1) },
                previousManifolds,
                1);
            solver.SolveVelocityIteration(bodies);
            HelPhysicsContactManifold3D[] invalidManifolds = new HelPhysicsContactManifold3D[] {
                CreateManifold(CreateContactWithData(12u, PhysicsVector3.UnitY, PhysicsVector3.Zero, PhysicsVector3.Zero, 0.1f)),
                CreateManifold(CreateContactWithData(13u, PhysicsVector3.UnitY, PhysicsVector3.Zero, PhysicsVector3.Zero, 0.1f))
            };
            HelPhysicsPairKey3D[] invalidPairs = new HelPhysicsPairKey3D[] {
                new HelPhysicsPairKey3D(2, 3),
                new HelPhysicsPairKey3D(0, 8)
            };

            Assert.Throws<ArgumentOutOfRangeException>(() => solver.Prepare(
                PhysicsScalar.FromFloat(0.02f),
                bodies,
                invalidPairs,
                invalidManifolds,
                2));
            solver.WriteBack(previousManifolds);

            AssertClose(2f, previousManifolds[0].GetContact(0).AccumulatedNormalImpulse);
        }

        /// <summary>
        /// Verifies that parallel pair and manifold arrays must describe exactly the same number of slots.
        /// </summary>
        [Fact]
        public void Prepare_WithMismatchedParallelArrayLengths_ThrowsArgumentException() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            bodies.Allocate(CreateDynamicState(PhysicsVector3.Zero), CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsPairKey3D[] pairs = new HelPhysicsPairKey3D[] {
                new HelPhysicsPairKey3D(0, 1),
                new HelPhysicsPairKey3D(0, 1)
            };
            HelPhysicsContactManifold3D[] manifolds = CreateManifoldArray(CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero));
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(2);

            Assert.Throws<ArgumentException>(() => solver.Prepare(
                PhysicsScalar.FromFloat(0.02f),
                bodies,
                pairs,
                manifolds,
                1));
        }

        /// <summary>
        /// Verifies that a default pair cannot alias one body as both contact participants.
        /// </summary>
        [Fact]
        public void Prepare_WithDefaultSelfPair_ThrowsArgumentOutOfRangeException() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(1);
            bodies.Allocate(CreateDynamicState(PhysicsVector3.Zero), CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => solver.Prepare(
                PhysicsScalar.FromFloat(0.02f),
                bodies,
                new HelPhysicsPairKey3D[] { default },
                CreateManifoldArray(CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero)),
                1));
        }

        /// <summary>
        /// Verifies that two active manifold slots cannot claim the same canonical body pair.
        /// </summary>
        [Fact]
        public void Prepare_WithDuplicateCanonicalPair_ThrowsInvalidOperationException() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            bodies.Allocate(CreateDynamicState(PhysicsVector3.Zero), CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsPairKey3D pair = new HelPhysicsPairKey3D(0, 1);
            HelPhysicsContactManifold3D manifold = CreateManifold(CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero));
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(2);

            Assert.Throws<InvalidOperationException>(() => solver.Prepare(
                PhysicsScalar.FromFloat(0.02f),
                bodies,
                new HelPhysicsPairKey3D[] { pair, pair },
                new HelPhysicsContactManifold3D[] { manifold, manifold },
                2));
        }

        /// <summary>
        /// Verifies that a canonical in-range pair still requires both fixed body slots to be occupied.
        /// </summary>
        [Fact]
        public void Prepare_WithUnoccupiedBodyIndex_ThrowsInvalidOperationException() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);

            Assert.Throws<InvalidOperationException>(() => solver.Prepare(
                PhysicsScalar.FromFloat(0.02f),
                bodies,
                new HelPhysicsPairKey3D[] { new HelPhysicsPairKey3D(0, 1) },
                CreateManifoldArray(CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero)),
                1));
        }

        /// <summary>
        /// Verifies that contact preparation rejects a zero normal before tangent construction can fail partway through publication.
        /// </summary>
        [Fact]
        public void Prepare_WithZeroContactNormal_ThrowsInvalidOperationException() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            bodies.Allocate(CreateDynamicState(PhysicsVector3.Zero), CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactPoint3D contact = CreateContactWithData(
                14u,
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                0.1f);
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);

            Assert.Throws<InvalidOperationException>(() => solver.Prepare(
                PhysicsScalar.FromFloat(0.02f),
                bodies,
                CreatePairArray(),
                CreateManifoldArray(contact),
                1));
        }

        /// <summary>
        /// Verifies that contact preparation rejects negative overlap depth instead of publishing invalid correction data.
        /// </summary>
        [Fact]
        public void Prepare_WithNegativePenetrationDepth_ThrowsInvalidOperationException() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            bodies.Allocate(CreateDynamicState(PhysicsVector3.Zero), CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactPoint3D contact = CreateContactWithData(
                15u,
                PhysicsVector3.UnitY,
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                -0.1f);
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(1);

            Assert.Throws<InvalidOperationException>(() => solver.Prepare(
                PhysicsScalar.FromFloat(0.02f),
                bodies,
                CreatePairArray(),
                CreateManifoldArray(contact),
                1));
        }

        /// <summary>
        /// Verifies that same-count contact reordering is rejected by feature identity without modifying the reordered manifold.
        /// </summary>
        [Fact]
        public void WriteBack_WithReorderedSameCountFeatures_ThrowsWithoutChangingManifold() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(2);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            bodies.Allocate(CreateDynamicState(PhysicsVector3.Zero), CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactPoint3D firstContact = CreateContactWithData(
                21u,
                PhysicsVector3.UnitY,
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                0.1f);
            firstContact.AccumulatedNormalImpulse = PhysicsScalar.FromFloat(1f);
            HelPhysicsContactPoint3D secondContact = CreateContactWithData(
                22u,
                PhysicsVector3.UnitY,
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                0.1f);
            secondContact.AccumulatedNormalImpulse = PhysicsScalar.FromFloat(2f);
            HelPhysicsContactManifold3D manifold = default;
            manifold.ContactCount = 2;
            manifold.SetContact(0, in firstContact);
            manifold.SetContact(1, in secondContact);
            HelPhysicsContactManifold3D[] manifolds = new HelPhysicsContactManifold3D[] { manifold };
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(2);
            solver.Prepare(PhysicsScalar.FromFloat(0.02f), bodies, CreatePairArray(), manifolds, 1);
            firstContact.AccumulatedNormalImpulse = PhysicsScalar.FromFloat(91f);
            secondContact.AccumulatedNormalImpulse = PhysicsScalar.FromFloat(92f);
            manifolds[0].SetContact(0, in secondContact);
            manifolds[0].SetContact(1, in firstContact);

            Assert.Throws<InvalidOperationException>(() => solver.WriteBack(manifolds));

            Assert.Equal((uint)22, manifolds[0].GetContact(0).Feature.Value);
            Assert.Equal((uint)21, manifolds[0].GetContact(1).Feature.Value);
            AssertClose(92f, manifolds[0].GetContact(0).AccumulatedNormalImpulse);
            AssertClose(91f, manifolds[0].GetContact(1).AccumulatedNormalImpulse);
        }

        /// <summary>
        /// Verifies that a later destination count mismatch is found before an earlier valid contact receives any solved impulse.
        /// </summary>
        [Fact]
        public void WriteBack_WithLaterInvalidDestination_DoesNotWriteEarlierContact() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(4);
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            bodies.Allocate(CreateDynamicState(PhysicsVector3.Zero), CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            bodies.Allocate(CreateStaticState(), CreateColdState(BodyKind3D.Static, 0f, 0f, 0f));
            bodies.Allocate(CreateDynamicState(PhysicsVector3.Zero), CreateColdState(BodyKind3D.Dynamic, 0f, 0f, 0f));
            HelPhysicsContactPoint3D firstContact = CreateContactWithData(
                31u,
                PhysicsVector3.UnitY,
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                0.1f);
            firstContact.AccumulatedNormalImpulse = PhysicsScalar.FromFloat(1f);
            HelPhysicsContactPoint3D secondContact = CreateContactWithData(
                32u,
                PhysicsVector3.UnitY,
                PhysicsVector3.Zero,
                PhysicsVector3.Zero,
                0.1f);
            secondContact.AccumulatedNormalImpulse = PhysicsScalar.FromFloat(2f);
            HelPhysicsContactManifold3D[] manifolds = new HelPhysicsContactManifold3D[] {
                CreateManifold(firstContact),
                CreateManifold(secondContact)
            };
            HelPhysicsPairKey3D[] pairs = new HelPhysicsPairKey3D[] {
                new HelPhysicsPairKey3D(0, 1),
                new HelPhysicsPairKey3D(2, 3)
            };
            HelPhysicsContactSolver3D solver = new HelPhysicsContactSolver3D(2);
            solver.Prepare(PhysicsScalar.FromFloat(0.02f), bodies, pairs, manifolds, 2);
            firstContact.AccumulatedNormalImpulse = PhysicsScalar.FromFloat(99f);
            manifolds[0].SetContact(0, in firstContact);
            manifolds[1].Reset();

            Assert.Throws<InvalidOperationException>(() => solver.WriteBack(manifolds));

            AssertClose(99f, manifolds[0].GetContact(0).AccumulatedNormalImpulse);
            Assert.Equal(0, manifolds[1].ContactCount);
        }

        /// <summary>
        /// Runs one prepared sequential velocity iteration for a single canonical body pair.
        /// </summary>
        /// <param name="solver">Fixed-capacity solver to prepare and execute.</param>
        /// <param name="bodies">Two-body pool containing static body zero and dynamic body one.</param>
        /// <param name="manifolds">Single current manifold to solve.</param>
        static void SolveOneIteration(
            HelPhysicsContactSolver3D solver,
            HelPhysicsBodyPool3D bodies,
            HelPhysicsContactManifold3D[] manifolds) {
            solver.Prepare(PhysicsScalar.FromFloat(0.02f), bodies, CreatePairArray(), manifolds, 1);
            solver.SolveVelocityIteration(bodies);
        }

        /// <summary>
        /// Creates immovable finite body state for the first body in each contact pair.
        /// </summary>
        /// <returns>Static-compatible hot body state at the world origin.</returns>
        static HelPhysicsBodyState3D CreateStaticState() {
            return new HelPhysicsBodyState3D {
                Position = PhysicsVector3.Zero,
                Orientation = PhysicsQuaternion.Identity,
                LinearVelocity = PhysicsVector3.Zero,
                AngularVelocity = PhysicsVector3.Zero,
                InverseMass = PhysicsScalar.Zero,
                LocalInverseInertia = default,
                GravityScale = PhysicsScalar.Zero,
                LinearDamping = PhysicsScalar.Zero,
                AngularDamping = PhysicsScalar.Zero,
                IsAwake = false
            };
        }

        /// <summary>
        /// Creates awake unit-mass body state with identity inverse inertia and the requested linear velocity.
        /// </summary>
        /// <param name="linearVelocity">Initial world-space linear velocity.</param>
        /// <returns>Dynamic-compatible hot body state.</returns>
        static HelPhysicsBodyState3D CreateDynamicState(PhysicsVector3 linearVelocity) {
            return new HelPhysicsBodyState3D {
                Position = PhysicsVector3.Zero,
                Orientation = PhysicsQuaternion.Identity,
                LinearVelocity = linearVelocity,
                AngularVelocity = PhysicsVector3.Zero,
                InverseMass = PhysicsScalar.One,
                LocalInverseInertia = PhysicsMatrix3x3.Identity,
                GravityScale = PhysicsScalar.One,
                LinearDamping = PhysicsScalar.Zero,
                AngularDamping = PhysicsScalar.Zero,
                IsAwake = true
            };
        }

        /// <summary>
        /// Creates awake dynamic state whose reciprocal mass and inertia scale together from one explicit large finite mass.
        /// </summary>
        /// <param name="linearVelocity">Initial world-space linear velocity.</param>
        /// <param name="mass">Positive finite mass used to derive reciprocal response.</param>
        /// <returns>Dynamic-compatible hot state with uniformly mass-scaled inverse inertia.</returns>
        static HelPhysicsBodyState3D CreateDynamicStateWithMass(
            PhysicsVector3 linearVelocity,
            float mass) {
            PhysicsScalar inverseMass = PhysicsScalar.One / PhysicsScalar.FromFloat(mass);
            return new HelPhysicsBodyState3D {
                Position = PhysicsVector3.Zero,
                Orientation = PhysicsQuaternion.Identity,
                LinearVelocity = linearVelocity,
                AngularVelocity = PhysicsVector3.Zero,
                InverseMass = inverseMass,
                LocalInverseInertia = PhysicsMatrix3x3.CreateDiagonal(new PhysicsVector3(
                    inverseMass,
                    inverseMass,
                    inverseMass)),
                GravityScale = PhysicsScalar.One,
                LinearDamping = PhysicsScalar.Zero,
                AngularDamping = PhysicsScalar.Zero,
                IsAwake = true
            };
        }

        /// <summary>
        /// Creates cold metadata with explicit material coefficients for one solver participant.
        /// </summary>
        /// <param name="bodyKind">Simulation motion kind.</param>
        /// <param name="staticFriction">Authored static friction coefficient.</param>
        /// <param name="dynamicFriction">Authored dynamic friction coefficient.</param>
        /// <param name="restitution">Authored restitution coefficient.</param>
        /// <returns>Cold body metadata containing the requested contact response.</returns>
        static HelPhysicsBodyColdState3D CreateColdState(
            BodyKind3D bodyKind,
            float staticFriction,
            float dynamicFriction,
            float restitution) {
            return new HelPhysicsBodyColdState3D {
                BodyKind = bodyKind,
                Material = new HelPhysicsMaterial3D(
                    PhysicsScalar.FromFloat(staticFriction),
                    PhysicsScalar.FromFloat(dynamicFriction),
                    PhysicsScalar.FromFloat(restitution)),
                CollisionLayer = 1,
                CollisionMask = ushort.MaxValue
            };
        }

        /// <summary>
        /// Creates one upward-normal contact with explicit local lever arms and deterministic feature provenance.
        /// </summary>
        /// <param name="localAnchorA">Contact anchor in static body local space.</param>
        /// <param name="localAnchorB">Contact anchor in dynamic body local space.</param>
        /// <returns>Fresh one-point contact with zero accumulated impulses.</returns>
        static HelPhysicsContactPoint3D CreateContact(PhysicsVector3 localAnchorA, PhysicsVector3 localAnchorB) {
            return CreateContactWithData(
                1u,
                PhysicsVector3.UnitY,
                localAnchorA,
                localAnchorB,
                0.1f);
        }

        /// <summary>
        /// Creates one contact with explicit feature, normal, local lever arms, and penetration for validation and mapping tests.
        /// </summary>
        /// <param name="featureValue">Stable feature identity stored by the contact.</param>
        /// <param name="normal">World-space contact normal directed from body A toward body B.</param>
        /// <param name="localAnchorA">Contact anchor in body A local space.</param>
        /// <param name="localAnchorB">Contact anchor in body B local space.</param>
        /// <param name="penetrationDepth">Authored overlap depth supplied to preparation.</param>
        /// <returns>Fresh contact containing the requested geometry and zero solver impulses.</returns>
        static HelPhysicsContactPoint3D CreateContactWithData(
            uint featureValue,
            PhysicsVector3 normal,
            PhysicsVector3 localAnchorA,
            PhysicsVector3 localAnchorB,
            float penetrationDepth) {
            return new HelPhysicsContactPoint3D(
                PhysicsVector3.Zero,
                normal,
                localAnchorA,
                localAnchorB,
                PhysicsScalar.FromFloat(penetrationDepth),
                new HelPhysicsContactFeature3D(featureValue));
        }

        /// <summary>
        /// Creates one manifold value with a single active contact.
        /// </summary>
        /// <param name="contact">Contact to place in the first inline slot.</param>
        /// <returns>One-contact manifold value.</returns>
        static HelPhysicsContactManifold3D CreateManifold(HelPhysicsContactPoint3D contact) {
            HelPhysicsContactManifold3D manifold = default;
            manifold.ContactCount = 1;
            manifold.SetContact(0, in contact);
            return manifold;
        }

        /// <summary>
        /// Creates four upward-normal contacts at the corners of a centered unit-box bottom face.
        /// </summary>
        /// <returns>A symmetric four-contact face manifold with distinct stable features.</returns>
        static HelPhysicsContactManifold3D CreateSymmetricFaceManifold() {
            HelPhysicsContactManifold3D manifold = default;
            manifold.ContactCount = 4;
            HelPhysicsContactPoint3D contact0 = CreateContactWithData(
                1u,
                PhysicsVector3.UnitY,
                new PhysicsVector3(-0.5f, 0f, -0.5f),
                new PhysicsVector3(-0.5f, -0.5f, -0.5f),
                0f);
            HelPhysicsContactPoint3D contact1 = CreateContactWithData(
                2u,
                PhysicsVector3.UnitY,
                new PhysicsVector3(0.5f, 0f, -0.5f),
                new PhysicsVector3(0.5f, -0.5f, -0.5f),
                0f);
            HelPhysicsContactPoint3D contact2 = CreateContactWithData(
                3u,
                PhysicsVector3.UnitY,
                new PhysicsVector3(-0.5f, 0f, 0.5f),
                new PhysicsVector3(-0.5f, -0.5f, 0.5f),
                0f);
            HelPhysicsContactPoint3D contact3 = CreateContactWithData(
                4u,
                PhysicsVector3.UnitY,
                new PhysicsVector3(0.5f, 0f, 0.5f),
                new PhysicsVector3(0.5f, -0.5f, 0.5f),
                0f);
            manifold.SetContact(0, in contact0);
            manifold.SetContact(1, in contact1);
            manifold.SetContact(2, in contact2);
            manifold.SetContact(3, in contact3);
            return manifold;
        }

        /// <summary>
        /// Creates two upward-normal contacts shifted to one side so coupled response must include angular motion.
        /// </summary>
        /// <returns>An asymmetric two-contact face manifold with distinct stable features.</returns>
        static HelPhysicsContactManifold3D CreateAsymmetricFaceManifold() {
            HelPhysicsContactManifold3D manifold = default;
            manifold.ContactCount = 2;
            HelPhysicsContactPoint3D contact0 = CreateContactWithData(
                11u,
                PhysicsVector3.UnitY,
                new PhysicsVector3(0.2f, 0f, -0.4f),
                new PhysicsVector3(0.2f, -0.5f, -0.4f),
                0f);
            HelPhysicsContactPoint3D contact1 = CreateContactWithData(
                12u,
                PhysicsVector3.UnitY,
                new PhysicsVector3(0.5f, 0f, 0.4f),
                new PhysicsVector3(0.5f, -0.5f, 0.4f),
                0f);
            manifold.SetContact(0, in contact0);
            manifold.SetContact(1, in contact1);
            return manifold;
        }

        /// <summary>
        /// Creates a single-manifold array with one active contact in its first inline slot.
        /// </summary>
        /// <param name="contact">Contact to place in the manifold.</param>
        /// <returns>One-element manifold array suitable for solver preparation.</returns>
        static HelPhysicsContactManifold3D[] CreateManifoldArray(HelPhysicsContactPoint3D contact) {
            return new HelPhysicsContactManifold3D[] { CreateManifold(contact) };
        }

        /// <summary>
        /// Creates the canonical static-zero to dynamic-one pair array used by single-contact tests.
        /// </summary>
        /// <returns>One-element pair array parallel to the test manifold array.</returns>
        static HelPhysicsPairKey3D[] CreatePairArray() {
            return new HelPhysicsPairKey3D[] { new HelPhysicsPairKey3D(0, 1) };
        }

        /// <summary>
        /// Verifies one named normal-block field exists and uses a physics-scalar array as its runtime storage type.
        /// </summary>
        /// <param name="fieldName">Exact non-public solver field to inspect.</param>
        static void AssertPhysicsScalarScratchField(string fieldName) {
            System.Reflection.FieldInfo field = typeof(HelPhysicsContactSolver3D).GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(field);
            Assert.Equal(typeof(PhysicsScalar[]), field.FieldType);
        }

        /// <summary>
        /// Verifies one scalar against a hand-derived float expectation within sequential-solver precision tolerance.
        /// </summary>
        /// <param name="expected">Hand-derived expected value.</param>
        /// <param name="actual">Physics scalar produced by the solver.</param>
        static void AssertClose(float expected, PhysicsScalar actual) {
            Assert.InRange(actual.ToFloat(), expected - 0.0001f, expected + 0.0001f);
        }

        /// <summary>
        /// Verifies all three solved impulse components against independent literal expectations.
        /// </summary>
        /// <param name="expectedNormal">Expected normal impulse.</param>
        /// <param name="expectedTangent0">Expected first tangent impulse.</param>
        /// <param name="expectedTangent1">Expected second tangent impulse.</param>
        /// <param name="contact">Contact whose solved impulse state is inspected.</param>
        static void AssertContactImpulses(
            float expectedNormal,
            float expectedTangent0,
            float expectedTangent1,
            HelPhysicsContactPoint3D contact) {
            AssertClose(expectedNormal, contact.AccumulatedNormalImpulse);
            AssertClose(expectedTangent0, contact.AccumulatedTangentImpulse0);
            AssertClose(expectedTangent1, contact.AccumulatedTangentImpulse1);
        }
    }
}
