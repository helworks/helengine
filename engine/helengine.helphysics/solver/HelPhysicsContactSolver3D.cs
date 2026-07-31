namespace helengine {
    /// <summary>
    /// Prepares fixed-capacity scalar contacts, solves coupled manifold normals and sequential friction, and writes solved impulses back.
    /// </summary>
    sealed class HelPhysicsContactSolver3D {
        /// <summary>
        /// Stores the exact incoming normal-speed boundary below which restitution becomes active.
        /// </summary>
        static readonly PhysicsScalar RestitutionImpactThreshold = PhysicsScalar.FromFloat(-1f);

        /// <summary>
        /// Stores the fixed inline contact count supported by one box manifold and its normal block.
        /// </summary>
        const int MaximumManifoldContactCount = 4;

        /// <summary>
        /// Stores one constructor-owned constraint slot for every contact the solver can process in a step.
        /// </summary>
        HelPhysicsContactConstraint3D[] Constraints;

        /// <summary>
        /// Stores constructor-owned constraints built only after complete input validation and swapped into active use atomically.
        /// </summary>
        HelPhysicsContactConstraint3D[] StagingConstraints;

        /// <summary>
        /// Stores the dedicated split-correction pass that consumes prepared contact pose data.
        /// </summary>
        readonly HelPhysicsPenetrationCorrector3D PenetrationCorrector;

        /// <summary>
        /// Stores the current manifold's dense four-by-four normal velocity response matrix in row-major order.
        /// </summary>
        readonly PhysicsScalar[] NormalBlockMatrix;

        /// <summary>
        /// Stores the current normal LCP constant term after removing already accumulated normal impulses.
        /// </summary>
        readonly PhysicsScalar[] NormalBlockConstants;

        /// <summary>
        /// Stores the accumulated normal impulses applied when the current block solve begins.
        /// </summary>
        readonly PhysicsScalar[] NormalBlockOldImpulses;

        /// <summary>
        /// Stores the candidate non-negative total impulse vector selected by active-set enumeration.
        /// </summary>
        readonly PhysicsScalar[] NormalBlockCandidateImpulses;

        /// <summary>
        /// Stores active contact indices compressed for one candidate complementarity subset.
        /// </summary>
        readonly int[] NormalBlockActiveIndices;

        /// <summary>
        /// Stores the compressed coefficient matrix used by deterministic Gaussian elimination.
        /// </summary>
        readonly PhysicsScalar[] NormalBlockWorkingMatrix;

        /// <summary>
        /// Stores the compressed right-hand side used by deterministic Gaussian elimination.
        /// </summary>
        readonly PhysicsScalar[] NormalBlockWorkingRightHandSide;

        /// <summary>
        /// Stores the compressed active impulse solution returned by deterministic Gaussian elimination.
        /// </summary>
        readonly PhysicsScalar[] NormalBlockWorkingSolution;

        /// <summary>
        /// Stores how many leading entries of <see cref="Constraints"/> were prepared for the current step.
        /// </summary>
        int ConstraintCount;

        /// <summary>
        /// Stores the exact parallel manifold-array length accepted by the last successful preparation.
        /// </summary>
        int PreparedManifoldArrayLength;

        /// <summary>
        /// Stores how many positional passes consumed the current prepared constraints so only later passes rebuild pose-dependent geometry.
        /// </summary>
        int PenetrationCorrectionPassIndex;

        /// <summary>
        /// Initializes fixed contact-constraint storage for the lifetime of this solver.
        /// </summary>
        /// <param name="contactCapacity">Positive maximum number of contacts that one step may prepare.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="contactCapacity"/> is not positive.</exception>
        public HelPhysicsContactSolver3D(int contactCapacity) {
            if (contactCapacity <= 0) {
                throw new ArgumentOutOfRangeException(nameof(contactCapacity), "Contact solver capacity must be positive.");
            }

            Constraints = new HelPhysicsContactConstraint3D[contactCapacity];
            StagingConstraints = new HelPhysicsContactConstraint3D[contactCapacity];
            PenetrationCorrector = new HelPhysicsPenetrationCorrector3D();
            NormalBlockMatrix = new PhysicsScalar[MaximumManifoldContactCount * MaximumManifoldContactCount];
            NormalBlockConstants = new PhysicsScalar[MaximumManifoldContactCount];
            NormalBlockOldImpulses = new PhysicsScalar[MaximumManifoldContactCount];
            NormalBlockCandidateImpulses = new PhysicsScalar[MaximumManifoldContactCount];
            NormalBlockActiveIndices = new int[MaximumManifoldContactCount];
            NormalBlockWorkingMatrix = new PhysicsScalar[MaximumManifoldContactCount * MaximumManifoldContactCount];
            NormalBlockWorkingRightHandSide = new PhysicsScalar[MaximumManifoldContactCount];
            NormalBlockWorkingSolution = new PhysicsScalar[MaximumManifoldContactCount];
        }

        /// <summary>
        /// Builds one fixed solver constraint per current manifold contact in deterministic manifold and contact order.
        /// </summary>
        /// <param name="stepSeconds">Positive simulation duration associated with the prepared constraints.</param>
        /// <param name="bodies">Fixed body pool addressed by the parallel pair keys.</param>
        /// <param name="pairs">Canonical body pairs parallel to <paramref name="manifolds"/>.</param>
        /// <param name="manifolds">Current manifolds whose contacts and cached impulses are prepared.</param>
        /// <param name="manifoldCount">Number of leading parallel pair and manifold entries to prepare.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the step or manifold count is invalid.</exception>
        /// <exception cref="ArgumentNullException">Thrown when a required pool or array is <see langword="null"/>.</exception>
        /// <exception cref="HelPhysicsCapacityExceededException">Thrown when current contacts exceed constructor-owned constraint storage.</exception>
        public void Prepare(
            PhysicsScalar stepSeconds,
            HelPhysicsBodyPool3D bodies,
            HelPhysicsPairKey3D[] pairs,
            HelPhysicsContactManifold3D[] manifolds,
            int manifoldCount) {
            if (stepSeconds <= PhysicsScalar.Zero) {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds), "Contact preparation requires a positive simulation step.");
            }

            if (bodies == null) {
                throw new ArgumentNullException(nameof(bodies));
            } else if (pairs == null) {
                throw new ArgumentNullException(nameof(pairs));
            } else if (manifolds == null) {
                throw new ArgumentNullException(nameof(manifolds));
            }

            if (pairs.Length != manifolds.Length) {
                throw new ArgumentException("Pair and manifold arrays must have equal lengths.", nameof(pairs));
            }

            if (manifoldCount < 0 || manifoldCount > manifolds.Length) {
                throw new ArgumentOutOfRangeException(nameof(manifoldCount), "Manifold count must fit the parallel input arrays.");
            }

            int requiredConstraintCount = ValidatePrepareInputs(bodies, pairs, manifolds, manifoldCount, Constraints.Length);
            int stagingConstraintCount = BuildConstraints(
                bodies,
                pairs,
                manifolds,
                manifoldCount,
                StagingConstraints);
            if (stagingConstraintCount != requiredConstraintCount) {
                throw new InvalidOperationException("Validated contact count changed while constraints were being staged.");
            }

            HelPhysicsContactConstraint3D[] previousConstraints = Constraints;
            Constraints = StagingConstraints;
            StagingConstraints = previousConstraints;
            ConstraintCount = stagingConstraintCount;
            PreparedManifoldArrayLength = manifolds.Length;
            PenetrationCorrectionPassIndex = 0;
        }

        /// <summary>
        /// Validates every active pair, body, manifold, contact, and derived constraint without mutating active or staging storage.
        /// </summary>
        /// <param name="bodies">Fixed body pool addressed by active pair keys.</param>
        /// <param name="pairs">Canonical body pairs parallel to active manifolds.</param>
        /// <param name="manifolds">Current contact manifolds to validate.</param>
        /// <param name="manifoldCount">Number of leading parallel entries that are active.</param>
        /// <param name="contactCapacity">Maximum number of contacts fixed solver storage can retain.</param>
        /// <returns>Total validated active contact count.</returns>
        /// <exception cref="HelPhysicsCapacityExceededException">Thrown when active contacts exceed fixed solver storage.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an active pair is non-canonical or outside body-pool range.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a pair is duplicated, a body is absent, or required solver data is invalid.</exception>
        static int ValidatePrepareInputs(
            HelPhysicsBodyPool3D bodies,
            HelPhysicsPairKey3D[] pairs,
            HelPhysicsContactManifold3D[] manifolds,
            int manifoldCount,
            int contactCapacity) {
            int requiredConstraintCount = 0;
            for (int manifoldIndex = 0; manifoldIndex < manifoldCount; manifoldIndex++) {
                HelPhysicsPairKey3D pair = pairs[manifoldIndex];
                ValidatePair(pair, pairs, manifoldIndex, bodies);
                ref HelPhysicsBodyState3D bodyA = ref bodies.GetRequiredStateByIndex(pair.FirstBodyIndex);
                ref HelPhysicsBodyState3D bodyB = ref bodies.GetRequiredStateByIndex(pair.SecondBodyIndex);
                ref HelPhysicsBodyColdState3D coldStateA = ref bodies.GetRequiredColdStateByIndex(pair.FirstBodyIndex);
                ref HelPhysicsBodyColdState3D coldStateB = ref bodies.GetRequiredColdStateByIndex(pair.SecondBodyIndex);
                ValidateBodyState(in bodyA);
                ValidateBodyState(in bodyB);

                HelPhysicsContactManifold3D manifold = manifolds[manifoldIndex];
                if (manifold.ContactCount < 0 || manifold.ContactCount > 4) {
                    throw new InvalidOperationException("Contact manifolds must contain between zero and four contacts.");
                }

                if (manifold.ContactCount > contactCapacity - requiredConstraintCount) {
                    throw new HelPhysicsCapacityExceededException("solver constraint", contactCapacity);
                }

                requiredConstraintCount += manifold.ContactCount;
                for (int contactIndex = 0; contactIndex < manifold.ContactCount; contactIndex++) {
                    HelPhysicsContactPoint3D contact = manifold.GetContact(contactIndex);
                    ValidateContact(in contact);
                    CreateConstraint(
                        pair,
                        manifoldIndex,
                        contactIndex,
                        manifold.ContactCount,
                        in bodyA,
                        in bodyB,
                        in coldStateA,
                        in coldStateB,
                        in contact);
                }
            }

            return requiredConstraintCount;
        }

        /// <summary>
        /// Builds validated contacts into inactive constructor-owned storage without publishing them to solver phases.
        /// </summary>
        /// <param name="bodies">Fixed body pool addressed by active pair keys.</param>
        /// <param name="pairs">Canonical body pairs parallel to active manifolds.</param>
        /// <param name="manifolds">Validated current contact manifolds.</param>
        /// <param name="manifoldCount">Number of leading parallel entries that are active.</param>
        /// <param name="destination">Inactive constructor-owned constraint storage to populate.</param>
        /// <returns>Number of constraints written to <paramref name="destination"/>.</returns>
        static int BuildConstraints(
            HelPhysicsBodyPool3D bodies,
            HelPhysicsPairKey3D[] pairs,
            HelPhysicsContactManifold3D[] manifolds,
            int manifoldCount,
            HelPhysicsContactConstraint3D[] destination) {
            int destinationIndex = 0;
            for (int manifoldIndex = 0; manifoldIndex < manifoldCount; manifoldIndex++) {
                HelPhysicsPairKey3D pair = pairs[manifoldIndex];
                ref HelPhysicsBodyState3D bodyA = ref bodies.GetRequiredStateByIndex(pair.FirstBodyIndex);
                ref HelPhysicsBodyState3D bodyB = ref bodies.GetRequiredStateByIndex(pair.SecondBodyIndex);
                ref HelPhysicsBodyColdState3D coldStateA = ref bodies.GetRequiredColdStateByIndex(pair.FirstBodyIndex);
                ref HelPhysicsBodyColdState3D coldStateB = ref bodies.GetRequiredColdStateByIndex(pair.SecondBodyIndex);
                HelPhysicsContactManifold3D manifold = manifolds[manifoldIndex];
                for (int contactIndex = 0; contactIndex < manifold.ContactCount; contactIndex++) {
                    HelPhysicsContactPoint3D contact = manifold.GetContact(contactIndex);
                    destination[destinationIndex++] = CreateConstraint(
                        pair,
                        manifoldIndex,
                        contactIndex,
                        manifold.ContactCount,
                        in bodyA,
                        in bodyB,
                        in coldStateA,
                        in coldStateB,
                        in contact);
                }
            }

            return destinationIndex;
        }

        /// <summary>
        /// Validates canonical ordering, body range and occupancy, and uniqueness among earlier active pair slots.
        /// </summary>
        /// <param name="pair">Active pair key to validate.</param>
        /// <param name="pairs">Parallel pair array containing this and earlier active keys.</param>
        /// <param name="pairIndex">Current active pair index.</param>
        /// <param name="bodies">Fixed body pool the pair must address.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when indices are non-canonical or outside pool capacity.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a body is unoccupied or the pair duplicates an earlier key.</exception>
        static void ValidatePair(
            HelPhysicsPairKey3D pair,
            HelPhysicsPairKey3D[] pairs,
            int pairIndex,
            HelPhysicsBodyPool3D bodies) {
            if (pair.FirstBodyIndex < 0 || pair.SecondBodyIndex <= pair.FirstBodyIndex) {
                throw new ArgumentOutOfRangeException(nameof(pairs), "Active pair keys must contain two distinct body indices in canonical ascending order.");
            }

            if (pair.SecondBodyIndex >= bodies.Capacity) {
                throw new ArgumentOutOfRangeException(nameof(pairs), "Active pair keys must address body indices within pool capacity.");
            }

            if (!bodies.IsOccupied(pair.FirstBodyIndex) || !bodies.IsOccupied(pair.SecondBodyIndex)) {
                throw new InvalidOperationException("Active pair keys must address two occupied body slots.");
            }

            for (int previousPairIndex = 0; previousPairIndex < pairIndex; previousPairIndex++) {
                if (pairs[previousPairIndex] == pair) {
                    throw new InvalidOperationException("Each active body pair may own only one current manifold.");
                }
            }
        }

        /// <summary>
        /// Validates hot body values that constraint derivation reads directly.
        /// </summary>
        /// <param name="body">Occupied body state to validate before any constraint is staged.</param>
        /// <exception cref="InvalidOperationException">Thrown when inverse mass is negative or orientation is not unit length.</exception>
        static void ValidateBodyState(in HelPhysicsBodyState3D body) {
            if (body.InverseMass < PhysicsScalar.Zero) {
                throw new InvalidOperationException("Body inverse mass must be non-negative before contact preparation.");
            }

            double orientationLengthSquared =
                ((double)body.Orientation.X.ToFloat() * body.Orientation.X.ToFloat()) +
                ((double)body.Orientation.Y.ToFloat() * body.Orientation.Y.ToFloat()) +
                ((double)body.Orientation.Z.ToFloat() * body.Orientation.Z.ToFloat()) +
                ((double)body.Orientation.W.ToFloat() * body.Orientation.W.ToFloat());
            if (Math.Abs(orientationLengthSquared - 1d) > 0.0001d) {
                throw new InvalidOperationException("Body orientation must be unit length before contact preparation.");
            }
        }

        /// <summary>
        /// Validates contact values required by basis construction, correction, and non-attractive warm starting.
        /// </summary>
        /// <param name="contact">Current contact to validate before deriving a constraint.</param>
        /// <exception cref="InvalidOperationException">Thrown when the normal is not unit length, penetration is negative, or normal impulse is attractive.</exception>
        static void ValidateContact(in HelPhysicsContactPoint3D contact) {
            double normalLengthSquared =
                ((double)contact.Normal.X.ToFloat() * contact.Normal.X.ToFloat()) +
                ((double)contact.Normal.Y.ToFloat() * contact.Normal.Y.ToFloat()) +
                ((double)contact.Normal.Z.ToFloat() * contact.Normal.Z.ToFloat());
            if (Math.Abs(normalLengthSquared - 1d) > 0.0001d) {
                throw new InvalidOperationException("Contact normals must be unit length before constraint preparation.");
            }

            if (contact.PenetrationDepth < PhysicsScalar.Zero) {
                throw new InvalidOperationException("Contact penetration depth must be non-negative before constraint preparation.");
            }

            if (contact.AccumulatedNormalImpulse < PhysicsScalar.Zero) {
                throw new InvalidOperationException("Accumulated normal contact impulse must be non-negative before warm starting.");
            }
        }

        /// <summary>
        /// Derives one complete solver constraint from already validated body, material, and contact data.
        /// </summary>
        /// <param name="pair">Canonical body pair owning the contact.</param>
        /// <param name="manifoldIndex">Source manifold index for later writeback.</param>
        /// <param name="contactIndex">Source inline contact index for later writeback.</param>
        /// <param name="manifoldContactCount">Complete active source manifold contact count.</param>
        /// <param name="bodyA">Current body A hot state.</param>
        /// <param name="bodyB">Current body B hot state.</param>
        /// <param name="coldStateA">Current body A cold state and material.</param>
        /// <param name="coldStateB">Current body B cold state and material.</param>
        /// <param name="contact">Validated current contact geometry and cached impulses.</param>
        /// <returns>Fully derived contact constraint ready for inactive staging storage.</returns>
        static HelPhysicsContactConstraint3D CreateConstraint(
            HelPhysicsPairKey3D pair,
            int manifoldIndex,
            int contactIndex,
            int manifoldContactCount,
            in HelPhysicsBodyState3D bodyA,
            in HelPhysicsBodyState3D bodyB,
            in HelPhysicsBodyColdState3D coldStateA,
            in HelPhysicsBodyColdState3D coldStateB,
            in HelPhysicsContactPoint3D contact) {
            bool respondsA = coldStateA.BodyKind == BodyKind3D.Dynamic && bodyA.IsAwake;
            bool respondsB = coldStateB.BodyKind == BodyKind3D.Dynamic && bodyB.IsAwake;
            PhysicsScalar inverseMassA = respondsA ? bodyA.InverseMass : PhysicsScalar.Zero;
            PhysicsScalar inverseMassB = respondsB ? bodyB.InverseMass : PhysicsScalar.Zero;
            PhysicsMatrix3x3 worldInverseInertiaA = default;
            PhysicsMatrix3x3 worldInverseInertiaB = default;
            if (respondsA) {
                PhysicsMatrix3x3 rotationA = PhysicsMatrix3x3.CreateFromQuaternion(bodyA.Orientation);
                worldInverseInertiaA = rotationA * bodyA.LocalInverseInertia * rotationA.Transposed();
            }
            if (respondsB) {
                PhysicsMatrix3x3 rotationB = PhysicsMatrix3x3.CreateFromQuaternion(bodyB.Orientation);
                worldInverseInertiaB = rotationB * bodyB.LocalInverseInertia * rotationB.Transposed();
            }

            HelPhysicsContactConstraint3D constraint = default;
            constraint.BodyAIndex = pair.FirstBodyIndex;
            constraint.BodyBIndex = pair.SecondBodyIndex;
            constraint.ManifoldIndex = manifoldIndex;
            constraint.ContactIndex = contactIndex;
            constraint.ManifoldContactCount = manifoldContactCount;
            constraint.Feature = contact.Feature;
            constraint.Normal = contact.Normal;
            constraint.Tangent0 = CreateFirstTangent(contact.Normal);
            constraint.Tangent1 = PhysicsVector3.Cross(contact.Normal, constraint.Tangent0);
            constraint.LocalAnchorA = contact.LocalAnchorA;
            constraint.LocalAnchorB = contact.LocalAnchorB;
            constraint.LeverArmA = bodyA.Orientation.Rotate(contact.LocalAnchorA);
            constraint.LeverArmB = bodyB.Orientation.Rotate(contact.LocalAnchorB);
            constraint.WorldInverseInertiaA = worldInverseInertiaA;
            constraint.WorldInverseInertiaB = worldInverseInertiaB;
            constraint.InverseMassA = inverseMassA;
            constraint.InverseMassB = inverseMassB;
            constraint.NormalEffectiveMass = ComputeEffectiveMass(
                constraint.Normal,
                constraint.LeverArmA,
                constraint.LeverArmB,
                inverseMassA,
                inverseMassB,
                worldInverseInertiaA,
                worldInverseInertiaB);
            constraint.TangentEffectiveMass0 = ComputeEffectiveMass(
                constraint.Tangent0,
                constraint.LeverArmA,
                constraint.LeverArmB,
                inverseMassA,
                inverseMassB,
                worldInverseInertiaA,
                worldInverseInertiaB);
            constraint.TangentEffectiveMass1 = ComputeEffectiveMass(
                constraint.Tangent1,
                constraint.LeverArmA,
                constraint.LeverArmB,
                inverseMassA,
                inverseMassB,
                worldInverseInertiaA,
                worldInverseInertiaB);
            PhysicsVector3 relativeVelocity = ComputeRelativeVelocity(
                in bodyA,
                in bodyB,
                constraint.LeverArmA,
                constraint.LeverArmB);
            PhysicsScalar incomingNormalVelocity = PhysicsVector3.Dot(relativeVelocity, constraint.Normal);
            constraint.RestitutionVelocity = PhysicsScalar.Zero;
            PhysicsScalar restitution = PhysicsScalar.Max(
                coldStateA.Material.Restitution,
                coldStateB.Material.Restitution);
            if (incomingNormalVelocity < RestitutionImpactThreshold) {
                constraint.RestitutionVelocity = -(restitution * incomingNormalVelocity);
            }
            constraint.StaticFriction = CombineFriction(
                coldStateA.Material.StaticFriction,
                coldStateB.Material.StaticFriction);
            constraint.DynamicFriction = CombineFriction(
                coldStateA.Material.DynamicFriction,
                coldStateB.Material.DynamicFriction);
            constraint.PenetrationDepth = contact.PenetrationDepth;
            constraint.AccumulatedNormalImpulse = contact.AccumulatedNormalImpulse;
            constraint.AccumulatedTangentImpulse0 = contact.AccumulatedTangentImpulse0;
            constraint.AccumulatedTangentImpulse1 = contact.AccumulatedTangentImpulse1;
            constraint.RespondsA = respondsA;
            constraint.RespondsB = respondsB;
            return constraint;
        }

        /// <summary>
        /// Computes the geometric mean of two finite non-negative friction coefficients without multiplying them first.
        /// </summary>
        /// <param name="first">First validated material friction coefficient.</param>
        /// <param name="second">Second validated material friction coefficient.</param>
        /// <returns>The finite geometric mean of the supplied coefficients.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an input is negative or the result cannot be represented by <see cref="PhysicsScalar"/>.</exception>
        static PhysicsScalar CombineFriction(PhysicsScalar first, PhysicsScalar second) {
            double firstValue = first.ToFloat();
            double secondValue = second.ToFloat();
            if (firstValue < 0d || secondValue < 0d) {
                throw new ArgumentOutOfRangeException(nameof(first), "Friction coefficients must be non-negative.");
            }

            double largerValue = Math.Max(firstValue, secondValue);
            if (largerValue == 0d) {
                return PhysicsScalar.Zero;
            }

            double smallerValue = Math.Min(firstValue, secondValue);
            double combinedValue = largerValue * Math.Sqrt(smallerValue / largerValue);
            if (double.IsNaN(combinedValue) || double.IsInfinity(combinedValue) || combinedValue > float.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(first), "Combined friction must be a finite physics scalar.");
            }

            return PhysicsScalar.FromFloat((float)combinedValue);
        }

        /// <summary>
        /// Applies every prepared contact's cached normal and tangent impulses to its responsive bodies.
        /// </summary>
        /// <param name="bodies">Fixed body pool containing every prepared pair.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bodies"/> is <see langword="null"/>.</exception>
        public void WarmStart(HelPhysicsBodyPool3D bodies) {
            if (bodies == null) {
                throw new ArgumentNullException(nameof(bodies));
            }

            for (int constraintIndex = 0; constraintIndex < ConstraintCount; constraintIndex++) {
                ref HelPhysicsContactConstraint3D constraint = ref Constraints[constraintIndex];
                PhysicsVector3 impulse =
                    (constraint.Normal * constraint.AccumulatedNormalImpulse) +
                    (constraint.Tangent0 * constraint.AccumulatedTangentImpulse0) +
                    (constraint.Tangent1 * constraint.AccumulatedTangentImpulse1);
                ApplyImpulse(ref constraint, impulse, bodies);
            }
        }

        /// <summary>
        /// Runs one deterministic symmetric iteration with forward and reverse manifold sweeps of coupled normals followed by ordered friction.
        /// </summary>
        /// <param name="bodies">Fixed body pool containing every prepared pair.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bodies"/> is <see langword="null"/>.</exception>
        public void SolveVelocityIteration(HelPhysicsBodyPool3D bodies) {
            if (bodies == null) {
                throw new ArgumentNullException(nameof(bodies));
            }

            int manifoldStartIndex = 0;
            while (manifoldStartIndex < ConstraintCount) {
                int manifoldContactCount = Constraints[manifoldStartIndex].ManifoldContactCount;
                SolveManifoldVelocityBlock(manifoldStartIndex, manifoldContactCount, bodies);
                manifoldStartIndex += manifoldContactCount;
            }

            int manifoldEndIndex = ConstraintCount;
            while (manifoldEndIndex > 0) {
                int manifoldContactCount = Constraints[manifoldEndIndex - 1].ManifoldContactCount;
                manifoldStartIndex = manifoldEndIndex - manifoldContactCount;
                SolveManifoldVelocityBlock(manifoldStartIndex, manifoldContactCount, bodies);
                manifoldEndIndex = manifoldStartIndex;
            }
        }

        /// <summary>
        /// Solves one contiguous manifold's coupled normals and ordered friction constraints during the current directional sweep.
        /// </summary>
        /// <param name="manifoldStartIndex">First prepared constraint owned by the manifold.</param>
        /// <param name="manifoldContactCount">One-through-four number of contiguous manifold contacts.</param>
        /// <param name="bodies">Fixed body pool receiving normal and friction impulses.</param>
        void SolveManifoldVelocityBlock(
            int manifoldStartIndex,
            int manifoldContactCount,
            HelPhysicsBodyPool3D bodies) {
            SolveNormalBlock(manifoldStartIndex, manifoldContactCount, bodies);
            for (int contactOffset = 0; contactOffset < manifoldContactCount; contactOffset++) {
                SolveFrictionConstraint(manifoldStartIndex + contactOffset, bodies);
            }
        }

        /// <summary>
        /// Replaces one manifold's accumulated normal impulses with the deterministic non-negative active-set solution of its coupled contact response.
        /// </summary>
        /// <param name="manifoldStartIndex">First contiguous prepared constraint owned by the manifold.</param>
        /// <param name="manifoldContactCount">One-through-four contact count owned by the manifold.</param>
        /// <param name="bodies">Fixed body pool containing both manifold participants.</param>
        /// <exception cref="InvalidOperationException">Thrown when prepared grouping is invalid or no finite complementarity solution exists.</exception>
        void SolveNormalBlock(
            int manifoldStartIndex,
            int manifoldContactCount,
            HelPhysicsBodyPool3D bodies) {
            ValidateNormalBlockRange(manifoldStartIndex, manifoldContactCount);
            bool hasNormalResponse = false;
            for (int contactOffset = 0; contactOffset < manifoldContactCount; contactOffset++) {
                ref HelPhysicsContactConstraint3D constraint = ref Constraints[manifoldStartIndex + contactOffset];
                NormalBlockOldImpulses[contactOffset] = constraint.AccumulatedNormalImpulse;
                hasNormalResponse = hasNormalResponse || constraint.NormalEffectiveMass > PhysicsScalar.Zero;
            }

            if (!hasNormalResponse) {
                return;
            }

            BuildNormalBlockMatrix(manifoldStartIndex, manifoldContactCount);
            BuildNormalBlockConstants(manifoldStartIndex, manifoldContactCount, bodies);
            if (!TrySolveNormalComplementarity(manifoldContactCount)) {
                throw new InvalidOperationException("The prepared contact manifold does not have a finite normal complementarity solution.");
            }

            for (int contactOffset = 0; contactOffset < manifoldContactCount; contactOffset++) {
                ref HelPhysicsContactConstraint3D constraint = ref Constraints[manifoldStartIndex + contactOffset];
                PhysicsScalar newImpulse = NormalBlockCandidateImpulses[contactOffset];
                PhysicsScalar oldImpulse = constraint.AccumulatedNormalImpulse;
                constraint.AccumulatedNormalImpulse = newImpulse;
                ApplyImpulse(ref constraint, constraint.Normal * (newImpulse - oldImpulse), bodies);
            }
        }

        /// <summary>
        /// Validates contiguous same-manifold contact metadata before a normal block can mutate body velocity.
        /// </summary>
        /// <param name="manifoldStartIndex">First prepared constraint in the block.</param>
        /// <param name="manifoldContactCount">Prepared block size.</param>
        /// <exception cref="InvalidOperationException">Thrown when block size, range, manifold identity, or contact order is inconsistent.</exception>
        void ValidateNormalBlockRange(int manifoldStartIndex, int manifoldContactCount) {
            if (manifoldContactCount < 1 || manifoldContactCount > MaximumManifoldContactCount) {
                throw new InvalidOperationException("Prepared normal blocks must contain one through four contacts.");
            }

            if (manifoldStartIndex < 0 || manifoldStartIndex > ConstraintCount - manifoldContactCount) {
                throw new InvalidOperationException("Prepared normal block ranges must fit current constraint storage.");
            }

            int manifoldIndex = Constraints[manifoldStartIndex].ManifoldIndex;
            for (int contactOffset = 0; contactOffset < manifoldContactCount; contactOffset++) {
                ref HelPhysicsContactConstraint3D constraint = ref Constraints[manifoldStartIndex + contactOffset];
                if (constraint.ManifoldIndex != manifoldIndex ||
                    constraint.ManifoldContactCount != manifoldContactCount ||
                    constraint.ContactIndex != contactOffset) {
                    throw new InvalidOperationException("Prepared normal blocks require contiguous contacts in exact manifold order.");
                }
            }
        }

        /// <summary>
        /// Builds the dense matrix mapping every candidate normal impulse to every contact's resulting normal velocity.
        /// </summary>
        /// <param name="manifoldStartIndex">First prepared constraint in the current manifold.</param>
        /// <param name="manifoldContactCount">Number of current manifold contacts.</param>
        void BuildNormalBlockMatrix(int manifoldStartIndex, int manifoldContactCount) {
            for (int rowIndex = 0; rowIndex < manifoldContactCount; rowIndex++) {
                ref HelPhysicsContactConstraint3D rowConstraint = ref Constraints[manifoldStartIndex + rowIndex];
                for (int columnIndex = 0; columnIndex < manifoldContactCount; columnIndex++) {
                    ref HelPhysicsContactConstraint3D columnConstraint = ref Constraints[manifoldStartIndex + columnIndex];
                    NormalBlockMatrix[(rowIndex * MaximumManifoldContactCount) + columnIndex] =
                        ComputeNormalVelocityCoupling(in rowConstraint, in columnConstraint);
                }
            }
        }

        /// <summary>
        /// Builds the LCP constant vector from current contact velocities after analytically removing the already applied normal impulse vector.
        /// </summary>
        /// <param name="manifoldStartIndex">First prepared constraint in the current manifold.</param>
        /// <param name="manifoldContactCount">Number of current manifold contacts.</param>
        /// <param name="bodies">Fixed body pool containing current velocities.</param>
        void BuildNormalBlockConstants(
            int manifoldStartIndex,
            int manifoldContactCount,
            HelPhysicsBodyPool3D bodies) {
            for (int rowIndex = 0; rowIndex < manifoldContactCount; rowIndex++) {
                ref HelPhysicsContactConstraint3D constraint = ref Constraints[manifoldStartIndex + rowIndex];
                ref HelPhysicsBodyState3D bodyA = ref bodies.GetRequiredStateByIndex(constraint.BodyAIndex);
                ref HelPhysicsBodyState3D bodyB = ref bodies.GetRequiredStateByIndex(constraint.BodyBIndex);
                PhysicsVector3 relativeVelocity = ComputeRelativeVelocity(
                    in bodyA,
                    in bodyB,
                    constraint.LeverArmA,
                    constraint.LeverArmB);
                PhysicsScalar constant =
                    PhysicsVector3.Dot(relativeVelocity, constraint.Normal) -
                    constraint.RestitutionVelocity;
                for (int columnIndex = 0; columnIndex < manifoldContactCount; columnIndex++) {
                    constant -= NormalBlockMatrix[(rowIndex * MaximumManifoldContactCount) + columnIndex] *
                        NormalBlockOldImpulses[columnIndex];
                }

                NormalBlockConstants[rowIndex] = constant;
            }
        }

        /// <summary>
        /// Enumerates all contact-active subsets in stable bit order and retains the first finite vector satisfying normal complementarity.
        /// </summary>
        /// <param name="manifoldContactCount">Number of rows and columns in the current normal block.</param>
        /// <returns><see langword="true"/> when one subset supplies a valid non-negative impulse and separating-velocity solution.</returns>
        bool TrySolveNormalComplementarity(int manifoldContactCount) {
            int subsetCount = 1 << manifoldContactCount;
            for (int activeMask = 0; activeMask < subsetCount; activeMask++) {
                if (TrySolveNormalActiveSet(manifoldContactCount, activeMask)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Solves one candidate active subset and verifies non-negative impulses and inactive separating velocities against the full block.
        /// </summary>
        /// <param name="manifoldContactCount">Number of current normal contacts.</param>
        /// <param name="activeMask">Bit mask selecting contacts whose final normal velocity equals restitution target.</param>
        /// <returns><see langword="true"/> when this subset satisfies the complete complementarity conditions.</returns>
        bool TrySolveNormalActiveSet(int manifoldContactCount, int activeMask) {
            int activeCount = 0;
            for (int contactIndex = 0; contactIndex < manifoldContactCount; contactIndex++) {
                NormalBlockCandidateImpulses[contactIndex] = PhysicsScalar.Zero;
                if ((activeMask & (1 << contactIndex)) != 0) {
                    NormalBlockActiveIndices[activeCount++] = contactIndex;
                }
            }

            if (activeCount > 0 && !TrySolveNormalLinearSystem(activeCount)) {
                return false;
            }

            for (int activeIndex = 0; activeIndex < activeCount; activeIndex++) {
                PhysicsScalar impulse = NormalBlockWorkingSolution[activeIndex];
                PhysicsScalar impulseTolerance =
                    PhysicsScalar.Max(PhysicsScalar.One, PhysicsScalar.Abs(impulse)) *
                    PhysicsScalar.ComputationalRelativeTolerance;
                if (impulse < -impulseTolerance) {
                    return false;
                }

                NormalBlockCandidateImpulses[NormalBlockActiveIndices[activeIndex]] =
                    impulse > PhysicsScalar.Zero ? impulse : PhysicsScalar.Zero;
            }

            for (int rowIndex = 0; rowIndex < manifoldContactCount; rowIndex++) {
                PhysicsScalar normalVelocity = NormalBlockConstants[rowIndex];
                PhysicsScalar rowScale = PhysicsScalar.Abs(normalVelocity);
                for (int columnIndex = 0; columnIndex < manifoldContactCount; columnIndex++) {
                    PhysicsScalar response =
                        NormalBlockMatrix[(rowIndex * MaximumManifoldContactCount) + columnIndex] *
                        NormalBlockCandidateImpulses[columnIndex];
                    normalVelocity += response;
                    rowScale = PhysicsScalar.Max(rowScale, PhysicsScalar.Abs(response));
                }

                PhysicsScalar rowTolerance =
                    PhysicsScalar.Max(PhysicsScalar.One, rowScale) *
                    PhysicsScalar.ComputationalRelativeTolerance;
                bool isActive = (activeMask & (1 << rowIndex)) != 0;
                if (isActive) {
                    if (PhysicsScalar.Abs(normalVelocity) > rowTolerance * PhysicsScalar.FromFloat(8f)) {
                        return false;
                    }
                } else if (normalVelocity < -rowTolerance) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Solves row-normalized active-contact equations with deterministic scale-aware partial-pivot Gaussian elimination.
        /// </summary>
        /// <param name="activeCount">Positive number of selected active contacts.</param>
        /// <returns><see langword="true"/> when every pivot and solved value is finite and non-degenerate.</returns>
        bool TrySolveNormalLinearSystem(int activeCount) {
            return BuildNormalWorkingSystem(activeCount) &&
                EliminateNormalWorkingSystem(activeCount);
        }

        /// <summary>
        /// Rebuilds one row-normalized active system so pivot classification depends on relative shape rather than absolute mass scale.
        /// </summary>
        /// <param name="activeCount">Positive number of selected active contacts.</param>
        /// <returns><see langword="true"/> when every active equation has representable nonzero response scale.</returns>
        bool BuildNormalWorkingSystem(int activeCount) {
            for (int rowIndex = 0; rowIndex < activeCount; rowIndex++) {
                int sourceRowIndex = NormalBlockActiveIndices[rowIndex];
                PhysicsScalar rowScale = PhysicsScalar.Zero;
                for (int columnIndex = 0; columnIndex < activeCount; columnIndex++) {
                    int sourceColumnIndex = NormalBlockActiveIndices[columnIndex];
                    PhysicsScalar coefficient =
                        NormalBlockMatrix[(sourceRowIndex * MaximumManifoldContactCount) + sourceColumnIndex];
                    NormalBlockWorkingMatrix[(rowIndex * MaximumManifoldContactCount) + columnIndex] = coefficient;
                    rowScale = PhysicsScalar.Max(rowScale, PhysicsScalar.Abs(coefficient));
                }

                if (rowScale == PhysicsScalar.Zero) {
                    return false;
                }

                NormalBlockWorkingRightHandSide[rowIndex] = -NormalBlockConstants[sourceRowIndex] / rowScale;
                NormalBlockWorkingSolution[rowIndex] = PhysicsScalar.Zero;
                for (int columnIndex = 0; columnIndex < activeCount; columnIndex++) {
                    NormalBlockWorkingMatrix[(rowIndex * MaximumManifoldContactCount) + columnIndex] /= rowScale;
                }
            }

            return true;
        }

        /// <summary>
        /// Eliminates the already row-normalized working system with scale-aware partial pivots and backend-scalar arithmetic.
        /// </summary>
        /// <param name="activeCount">Positive dimension of the populated working system.</param>
        /// <returns><see langword="true"/> when every normalized pivot remains representably distinct from singularity.</returns>
        bool EliminateNormalWorkingSystem(int activeCount) {
            for (int pivotIndex = 0; pivotIndex < activeCount; pivotIndex++) {
                int pivotRowIndex = FindNormalPivotRow(pivotIndex, activeCount);
                PhysicsScalar pivotMagnitude = PhysicsScalar.Abs(
                    NormalBlockWorkingMatrix[(pivotRowIndex * MaximumManifoldContactCount) + pivotIndex]);
                if (pivotMagnitude <= PhysicsScalar.ComputationalRelativeTolerance) {
                    return false;
                }

                if (pivotRowIndex != pivotIndex) {
                    SwapNormalWorkingRows(pivotIndex, pivotRowIndex, activeCount);
                }

                PhysicsScalar pivot = NormalBlockWorkingMatrix[(pivotIndex * MaximumManifoldContactCount) + pivotIndex];
                for (int rowIndex = pivotIndex + 1; rowIndex < activeCount; rowIndex++) {
                    PhysicsScalar factor =
                        NormalBlockWorkingMatrix[(rowIndex * MaximumManifoldContactCount) + pivotIndex] / pivot;
                    NormalBlockWorkingMatrix[(rowIndex * MaximumManifoldContactCount) + pivotIndex] = PhysicsScalar.Zero;
                    for (int columnIndex = pivotIndex + 1; columnIndex < activeCount; columnIndex++) {
                        NormalBlockWorkingMatrix[(rowIndex * MaximumManifoldContactCount) + columnIndex] -=
                            factor * NormalBlockWorkingMatrix[(pivotIndex * MaximumManifoldContactCount) + columnIndex];
                    }

                    NormalBlockWorkingRightHandSide[rowIndex] -=
                        factor * NormalBlockWorkingRightHandSide[pivotIndex];
                }
            }

            for (int rowIndex = activeCount - 1; rowIndex >= 0; rowIndex--) {
                PhysicsScalar value = NormalBlockWorkingRightHandSide[rowIndex];
                for (int columnIndex = rowIndex + 1; columnIndex < activeCount; columnIndex++) {
                    value -= NormalBlockWorkingMatrix[(rowIndex * MaximumManifoldContactCount) + columnIndex] *
                        NormalBlockWorkingSolution[columnIndex];
                }

                value /= NormalBlockWorkingMatrix[(rowIndex * MaximumManifoldContactCount) + rowIndex];
                NormalBlockWorkingSolution[rowIndex] = value;
            }

            return true;
        }

        /// <summary>
        /// Finds the largest available pivot magnitude with stable lowest-row tie order.
        /// </summary>
        /// <param name="pivotIndex">Current matrix column and first eligible row.</param>
        /// <param name="activeCount">Compressed matrix dimension.</param>
        /// <returns>Selected pivot row index.</returns>
        int FindNormalPivotRow(int pivotIndex, int activeCount) {
            int pivotRowIndex = pivotIndex;
            PhysicsScalar pivotMagnitude = PhysicsScalar.Abs(
                NormalBlockWorkingMatrix[(pivotIndex * MaximumManifoldContactCount) + pivotIndex]);
            for (int rowIndex = pivotIndex + 1; rowIndex < activeCount; rowIndex++) {
                PhysicsScalar candidateMagnitude = PhysicsScalar.Abs(
                    NormalBlockWorkingMatrix[(rowIndex * MaximumManifoldContactCount) + pivotIndex]);
                if (candidateMagnitude > pivotMagnitude) {
                    pivotMagnitude = candidateMagnitude;
                    pivotRowIndex = rowIndex;
                }
            }

            return pivotRowIndex;
        }

        /// <summary>
        /// Swaps two compressed matrix rows and their right-hand-side values without temporary allocation.
        /// </summary>
        /// <param name="firstRowIndex">First row to exchange.</param>
        /// <param name="secondRowIndex">Second row to exchange.</param>
        /// <param name="activeCount">Compressed matrix dimension.</param>
        void SwapNormalWorkingRows(int firstRowIndex, int secondRowIndex, int activeCount) {
            for (int columnIndex = 0; columnIndex < activeCount; columnIndex++) {
                int firstIndex = (firstRowIndex * MaximumManifoldContactCount) + columnIndex;
                int secondIndex = (secondRowIndex * MaximumManifoldContactCount) + columnIndex;
                PhysicsScalar value = NormalBlockWorkingMatrix[firstIndex];
                NormalBlockWorkingMatrix[firstIndex] = NormalBlockWorkingMatrix[secondIndex];
                NormalBlockWorkingMatrix[secondIndex] = value;
            }

            PhysicsScalar rightHandSide = NormalBlockWorkingRightHandSide[firstRowIndex];
            NormalBlockWorkingRightHandSide[firstRowIndex] = NormalBlockWorkingRightHandSide[secondRowIndex];
            NormalBlockWorkingRightHandSide[secondRowIndex] = rightHandSide;
        }

        /// <summary>
        /// Computes one dense normal response entry from an impulse at the column contact to velocity at the row contact.
        /// </summary>
        /// <param name="rowConstraint">Contact whose resulting relative normal velocity is measured.</param>
        /// <param name="columnConstraint">Contact at which one unit normal impulse is applied.</param>
        /// <returns>Finite scalar velocity response coefficient.</returns>
        static PhysicsScalar ComputeNormalVelocityCoupling(
            in HelPhysicsContactConstraint3D rowConstraint,
            in HelPhysicsContactConstraint3D columnConstraint) {
            PhysicsScalar linearResponse =
                (rowConstraint.InverseMassA + rowConstraint.InverseMassB) *
                PhysicsVector3.Dot(rowConstraint.Normal, columnConstraint.Normal);
            PhysicsVector3 angularImpulseA = columnConstraint.WorldInverseInertiaA.Transform(
                PhysicsVector3.Cross(columnConstraint.LeverArmA, columnConstraint.Normal));
            PhysicsVector3 angularImpulseB = columnConstraint.WorldInverseInertiaB.Transform(
                PhysicsVector3.Cross(columnConstraint.LeverArmB, columnConstraint.Normal));
            PhysicsScalar angularResponseA = PhysicsVector3.Dot(
                rowConstraint.Normal,
                PhysicsVector3.Cross(angularImpulseA, rowConstraint.LeverArmA));
            PhysicsScalar angularResponseB = PhysicsVector3.Dot(
                rowConstraint.Normal,
                PhysicsVector3.Cross(angularImpulseB, rowConstraint.LeverArmB));
            return linearResponse + angularResponseA + angularResponseB;
        }

        /// <summary>
        /// Solves both tangent axes for one contact using the normal impulse selected by its manifold block.
        /// </summary>
        /// <param name="constraintIndex">Prepared fixed constraint slot to solve.</param>
        /// <param name="bodies">Fixed body pool containing both prepared participants.</param>
        void SolveFrictionConstraint(int constraintIndex, HelPhysicsBodyPool3D bodies) {
            ref HelPhysicsContactConstraint3D constraint = ref Constraints[constraintIndex];
            ref HelPhysicsBodyState3D bodyA = ref bodies.GetRequiredStateByIndex(constraint.BodyAIndex);
            ref HelPhysicsBodyState3D bodyB = ref bodies.GetRequiredStateByIndex(constraint.BodyBIndex);
            PhysicsVector3 relativeVelocity = ComputeRelativeVelocity(
                in bodyA,
                in bodyB,
                constraint.LeverArmA,
                constraint.LeverArmB);
            PhysicsScalar candidateTangentImpulse0 =
                constraint.AccumulatedTangentImpulse0 -
                (PhysicsVector3.Dot(relativeVelocity, constraint.Tangent0) * constraint.TangentEffectiveMass0);
            PhysicsScalar candidateTangentImpulse1 =
                constraint.AccumulatedTangentImpulse1 -
                (PhysicsVector3.Dot(relativeVelocity, constraint.Tangent1) * constraint.TangentEffectiveMass1);
            PhysicsScalar staticLimit = constraint.StaticFriction * constraint.AccumulatedNormalImpulse;
            PhysicsScalar candidateLengthSquared =
                (candidateTangentImpulse0 * candidateTangentImpulse0) +
                (candidateTangentImpulse1 * candidateTangentImpulse1);
            PhysicsScalar newTangentImpulse0;
            PhysicsScalar newTangentImpulse1;
            if (candidateLengthSquared <= staticLimit * staticLimit) {
                newTangentImpulse0 = candidateTangentImpulse0;
                newTangentImpulse1 = candidateTangentImpulse1;
            } else {
                PhysicsScalar dynamicLimit = constraint.DynamicFriction * constraint.AccumulatedNormalImpulse;
                if (candidateLengthSquared > PhysicsScalar.Zero && dynamicLimit > PhysicsScalar.Zero) {
                    PhysicsScalar dynamicScale = dynamicLimit * PhysicsScalar.ReciprocalSqrt(candidateLengthSquared);
                    newTangentImpulse0 = candidateTangentImpulse0 * dynamicScale;
                    newTangentImpulse1 = candidateTangentImpulse1 * dynamicScale;
                } else {
                    newTangentImpulse0 = PhysicsScalar.Zero;
                    newTangentImpulse1 = PhysicsScalar.Zero;
                }
            }

            PhysicsScalar tangentImpulseDelta0 = newTangentImpulse0 - constraint.AccumulatedTangentImpulse0;
            PhysicsScalar tangentImpulseDelta1 = newTangentImpulse1 - constraint.AccumulatedTangentImpulse1;
            constraint.AccumulatedTangentImpulse0 = newTangentImpulse0;
            constraint.AccumulatedTangentImpulse1 = newTangentImpulse1;
            PhysicsVector3 tangentImpulse =
                (constraint.Tangent0 * tangentImpulseDelta0) +
                (constraint.Tangent1 * tangentImpulseDelta1);
            ApplyImpulse(ref constraint, tangentImpulse, bodies);
        }

        /// <summary>
        /// Copies final accumulated normal and tangent impulses back to each prepared current manifold contact.
        /// </summary>
        /// <param name="manifolds">Current manifold array originally supplied to <see cref="Prepare"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="manifolds"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when prepared manifold/contact layout changed before writeback.</exception>
        public void WriteBack(HelPhysicsContactManifold3D[] manifolds) {
            if (manifolds == null) {
                throw new ArgumentNullException(nameof(manifolds));
            }

            if (manifolds.Length != PreparedManifoldArrayLength) {
                throw new InvalidOperationException("Prepared manifold array length must remain unchanged through solved impulse writeback.");
            }

            for (int constraintIndex = 0; constraintIndex < ConstraintCount; constraintIndex++) {
                ref HelPhysicsContactConstraint3D constraint = ref Constraints[constraintIndex];
                if (constraint.ManifoldIndex < 0 || constraint.ManifoldIndex >= manifolds.Length) {
                    throw new InvalidOperationException("Prepared manifold contacts must remain present through solved impulse writeback.");
                }

                HelPhysicsContactManifold3D manifold = manifolds[constraint.ManifoldIndex];
                if (manifold.ContactCount != constraint.ManifoldContactCount ||
                    constraint.ContactIndex < 0 ||
                    constraint.ContactIndex >= manifold.ContactCount) {
                    throw new InvalidOperationException("Prepared manifold contact counts and indices must remain unchanged through solved impulse writeback.");
                }

                HelPhysicsContactPoint3D contact = manifold.GetContact(constraint.ContactIndex);
                if (contact.Feature != constraint.Feature) {
                    throw new InvalidOperationException("Prepared contact features must remain in matching order through solved impulse writeback.");
                }
            }

            for (int constraintIndex = 0; constraintIndex < ConstraintCount; constraintIndex++) {
                ref HelPhysicsContactConstraint3D constraint = ref Constraints[constraintIndex];
                HelPhysicsContactPoint3D contact =
                    manifolds[constraint.ManifoldIndex].GetContact(constraint.ContactIndex);
                contact.AccumulatedNormalImpulse = constraint.AccumulatedNormalImpulse;
                contact.AccumulatedTangentImpulse0 = constraint.AccumulatedTangentImpulse0;
                contact.AccumulatedTangentImpulse1 = constraint.AccumulatedTangentImpulse1;
                manifolds[constraint.ManifoldIndex].SetContact(constraint.ContactIndex, in contact);
            }
        }

        /// <summary>
        /// Corrects prepared penetration through a pose-only pass that leaves velocities and accumulated impulses unchanged.
        /// </summary>
        /// <param name="bodies">Fixed body pool containing every prepared pair.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="bodies"/> is <see langword="null"/>.</exception>
        public void CorrectPenetration(HelPhysicsBodyPool3D bodies) {
            PenetrationCorrector.CorrectPenetration(
                bodies,
                Constraints,
                ConstraintCount,
                PenetrationCorrectionPassIndex > 0);
            if (PenetrationCorrectionPassIndex < int.MaxValue) {
                PenetrationCorrectionPassIndex++;
            }
        }

        /// <summary>
        /// Creates a deterministic, numerically stable first tangent by crossing the normal with its least-aligned cardinal axis.
        /// </summary>
        /// <param name="normal">Unit contact normal requiring a perpendicular basis.</param>
        /// <returns>Normalized first tangent with deterministic sign and axis selection.</returns>
        static PhysicsVector3 CreateFirstTangent(PhysicsVector3 normal) {
            PhysicsScalar absoluteX = PhysicsScalar.Abs(normal.X);
            PhysicsScalar absoluteY = PhysicsScalar.Abs(normal.Y);
            PhysicsScalar absoluteZ = PhysicsScalar.Abs(normal.Z);
            if (absoluteX <= absoluteY && absoluteX <= absoluteZ) {
                return PhysicsVector3.Cross(PhysicsVector3.UnitX, normal).Normalized();
            } else if (absoluteY <= absoluteZ) {
                return PhysicsVector3.Cross(PhysicsVector3.UnitY, normal).Normalized();
            }

            return PhysicsVector3.Cross(PhysicsVector3.UnitZ, normal).Normalized();
        }

        /// <summary>
        /// Computes reciprocal scalar effective mass along one world-space constraint direction.
        /// </summary>
        /// <param name="direction">Unit normal or tangent constraint direction.</param>
        /// <param name="leverArmA">World-space body A contact lever arm.</param>
        /// <param name="leverArmB">World-space body B contact lever arm.</param>
        /// <param name="inverseMassA">Responsive inverse mass for body A.</param>
        /// <param name="inverseMassB">Responsive inverse mass for body B.</param>
        /// <param name="worldInverseInertiaA">Responsive world inverse inertia for body A.</param>
        /// <param name="worldInverseInertiaB">Responsive world inverse inertia for body B.</param>
        /// <returns>Reciprocal effective mass, or zero when neither body can respond along the direction.</returns>
        static PhysicsScalar ComputeEffectiveMass(
            PhysicsVector3 direction,
            PhysicsVector3 leverArmA,
            PhysicsVector3 leverArmB,
            PhysicsScalar inverseMassA,
            PhysicsScalar inverseMassB,
            PhysicsMatrix3x3 worldInverseInertiaA,
            PhysicsMatrix3x3 worldInverseInertiaB) {
            PhysicsVector3 angularDirectionA = PhysicsVector3.Cross(leverArmA, direction);
            PhysicsVector3 angularDirectionB = PhysicsVector3.Cross(leverArmB, direction);
            PhysicsScalar denominator =
                inverseMassA +
                inverseMassB +
                PhysicsVector3.Dot(angularDirectionA, worldInverseInertiaA.Transform(angularDirectionA)) +
                PhysicsVector3.Dot(angularDirectionB, worldInverseInertiaB.Transform(angularDirectionB));
            if (denominator <= PhysicsScalar.Zero) {
                return PhysicsScalar.Zero;
            }

            return PhysicsScalar.One / denominator;
        }

        /// <summary>
        /// Computes body B contact-point velocity relative to body A from current linear and angular velocity.
        /// </summary>
        /// <param name="bodyA">Current body A hot state.</param>
        /// <param name="bodyB">Current body B hot state.</param>
        /// <param name="leverArmA">World-space body A contact lever arm.</param>
        /// <param name="leverArmB">World-space body B contact lever arm.</param>
        /// <returns>World-space relative contact velocity directed from body A motion to body B motion.</returns>
        static PhysicsVector3 ComputeRelativeVelocity(
            in HelPhysicsBodyState3D bodyA,
            in HelPhysicsBodyState3D bodyB,
            PhysicsVector3 leverArmA,
            PhysicsVector3 leverArmB) {
            PhysicsVector3 velocityA = bodyA.LinearVelocity + PhysicsVector3.Cross(bodyA.AngularVelocity, leverArmA);
            PhysicsVector3 velocityB = bodyB.LinearVelocity + PhysicsVector3.Cross(bodyB.AngularVelocity, leverArmB);
            return velocityB - velocityA;
        }

        /// <summary>
        /// Applies one world-space contact impulse with opposite signs to responsive body A and body B state.
        /// </summary>
        /// <param name="constraint">Prepared contact containing response flags, masses, inertias, and lever arms.</param>
        /// <param name="impulse">World-space impulse directed from body A toward body B.</param>
        /// <param name="bodies">Fixed body pool containing both prepared participants.</param>
        static void ApplyImpulse(
            ref HelPhysicsContactConstraint3D constraint,
            PhysicsVector3 impulse,
            HelPhysicsBodyPool3D bodies) {
            if (constraint.RespondsA) {
                ref HelPhysicsBodyState3D bodyA = ref bodies.GetRequiredStateByIndex(constraint.BodyAIndex);
                bodyA.LinearVelocity -= impulse * constraint.InverseMassA;
                bodyA.AngularVelocity -= constraint.WorldInverseInertiaA.Transform(
                    PhysicsVector3.Cross(constraint.LeverArmA, impulse));
            }

            if (constraint.RespondsB) {
                ref HelPhysicsBodyState3D bodyB = ref bodies.GetRequiredStateByIndex(constraint.BodyBIndex);
                bodyB.LinearVelocity += impulse * constraint.InverseMassB;
                bodyB.AngularVelocity += constraint.WorldInverseInertiaB.Transform(
                    PhysicsVector3.Cross(constraint.LeverArmB, impulse));
            }
        }
    }
}
