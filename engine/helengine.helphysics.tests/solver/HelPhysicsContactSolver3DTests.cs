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
            HelPhysicsContactPoint3D contact = CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero);
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
            HelPhysicsContactPoint3D contact = CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero);
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
            HelPhysicsContactPoint3D contact = CreateContact(PhysicsVector3.Zero, PhysicsVector3.Zero);
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
            HelPhysicsContactPoint3D contact = CreateContact(PhysicsVector3.Zero, PhysicsVector3.UnitX);
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
            return new HelPhysicsContactPoint3D(
                PhysicsVector3.Zero,
                PhysicsVector3.UnitY,
                localAnchorA,
                localAnchorB,
                PhysicsScalar.FromFloat(0.1f),
                new HelPhysicsContactFeature3D(1u));
        }

        /// <summary>
        /// Creates a single-manifold array with one active contact in its first inline slot.
        /// </summary>
        /// <param name="contact">Contact to place in the manifold.</param>
        /// <returns>One-element manifold array suitable for solver preparation.</returns>
        static HelPhysicsContactManifold3D[] CreateManifoldArray(HelPhysicsContactPoint3D contact) {
            HelPhysicsContactManifold3D manifold = default;
            manifold.ContactCount = 1;
            manifold.SetContact(0, in contact);
            return new HelPhysicsContactManifold3D[] { manifold };
        }

        /// <summary>
        /// Creates the canonical static-zero to dynamic-one pair array used by single-contact tests.
        /// </summary>
        /// <returns>One-element pair array parallel to the test manifold array.</returns>
        static HelPhysicsPairKey3D[] CreatePairArray() {
            return new HelPhysicsPairKey3D[] { new HelPhysicsPairKey3D(0, 1) };
        }

        /// <summary>
        /// Verifies one scalar against a hand-derived float expectation within sequential-solver precision tolerance.
        /// </summary>
        /// <param name="expected">Hand-derived expected value.</param>
        /// <param name="actual">Physics scalar produced by the solver.</param>
        static void AssertClose(float expected, PhysicsScalar actual) {
            Assert.InRange(actual.ToFloat(), expected - 0.0001f, expected + 0.0001f);
        }
    }
}
